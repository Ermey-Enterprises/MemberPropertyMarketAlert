using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;

namespace MemberPropertyAlert.Functions.Services
{
    public class CosmosService : ICosmosService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly CosmosConfiguration _config;
        private readonly ILogger<CosmosService> _logger;
        private Database? _database;
        private Container? _institutionsContainer;
        private Container? _addressesContainer;
        private Container? _alertsContainer;
        private Container? _scanLogsContainer;

        public CosmosService(
            CosmosClient cosmosClient,
            IOptions<CosmosConfiguration> config,
            ILogger<CosmosService> logger)
        {
            _cosmosClient = cosmosClient;
            _config = config.Value;
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Create database
                var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                    _config.DatabaseName,
                    _config.EnableAutoscale ? ThroughputProperties.CreateAutoscaleThroughput(_config.MaxThroughput) : 
                                            ThroughputProperties.CreateManualThroughput(_config.DefaultThroughput));
                
                _database = databaseResponse.Database;
                _logger.LogInformation("Database {DatabaseName} initialized", _config.DatabaseName);

                // Create containers
                await CreateContainerIfNotExistsAsync(_config.InstitutionsContainer, "/id");
                await CreateContainerIfNotExistsAsync(_config.AddressesContainer, "/institutionId");
                await CreateContainerIfNotExistsAsync(_config.AlertsContainer, "/institutionId");
                await CreateContainerIfNotExistsAsync(_config.ScanLogsContainer, "/institutionId");

                // Get container references
                _institutionsContainer = _database.GetContainer(_config.InstitutionsContainer);
                _addressesContainer = _database.GetContainer(_config.AddressesContainer);
                _alertsContainer = _database.GetContainer(_config.AlertsContainer);
                _scanLogsContainer = _database.GetContainer(_config.ScanLogsContainer);

                _logger.LogInformation("All containers initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Cosmos DB");
                throw;
            }
        }

        private async Task CreateContainerIfNotExistsAsync(string containerName, string partitionKeyPath)
        {
            if (_database == null)
                throw new InvalidOperationException("Database not initialized");

            var containerProperties = new ContainerProperties(containerName, partitionKeyPath);
            
            // Add indexing policy for better query performance
            containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            containerProperties.IndexingPolicy.Automatic = true;

            await _database.CreateContainerIfNotExistsAsync(containerProperties);
            _logger.LogInformation("Container {ContainerName} initialized", containerName);
        }

        // Institution Management
        public async Task<Institution> CreateInstitutionAsync(Institution institution)
        {
            EnsureInitialized();
            var response = await _institutionsContainer!.CreateItemAsync(institution, new PartitionKey(institution.Id));
            _logger.LogInformation("Institution created: {InstitutionId}", institution.Id);
            return response.Resource;
        }

        public async Task<Institution?> GetInstitutionAsync(string institutionId)
        {
            EnsureInitialized();
            try
            {
                var response = await _institutionsContainer!.ReadItemAsync<Institution>(institutionId, new PartitionKey(institutionId));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<Institution> UpdateInstitutionAsync(Institution institution)
        {
            EnsureInitialized();
            institution.UpdatedAt = DateTime.UtcNow;
            var response = await _institutionsContainer!.ReplaceItemAsync(institution, institution.Id, new PartitionKey(institution.Id));
            _logger.LogInformation("Institution updated: {InstitutionId}", institution.Id);
            return response.Resource;
        }

        public async Task DeleteInstitutionAsync(string institutionId)
        {
            EnsureInitialized();
            await _institutionsContainer!.DeleteItemAsync<Institution>(institutionId, new PartitionKey(institutionId));
            _logger.LogInformation("Institution deleted: {InstitutionId}", institutionId);
        }

        public async Task<List<Institution>> GetAllInstitutionsAsync()
        {
            EnsureInitialized();
            var query = new QueryDefinition("SELECT * FROM c WHERE c.isActive = true");
            var iterator = _institutionsContainer!.GetItemQueryIterator<Institution>(query);
            var results = new List<Institution>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        // Address Management
        public async Task<MemberAddress> CreateAddressAsync(MemberAddress address)
        {
            EnsureInitialized();
            var response = await _addressesContainer!.CreateItemAsync(address, new PartitionKey(address.InstitutionId));
            _logger.LogInformation("Address created: {AddressId} for institution {InstitutionId}", address.Id, address.InstitutionId);
            return response.Resource;
        }

        public async Task<MemberAddress?> GetAddressAsync(string addressId)
        {
            EnsureInitialized();
            try
            {
                // Since we don't know the partition key, we need to query
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @addressId")
                    .WithParameter("@addressId", addressId);
                
                var iterator = _addressesContainer!.GetItemQueryIterator<MemberAddress>(query);
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<MemberAddress> UpdateAddressAsync(MemberAddress address)
        {
            EnsureInitialized();
            address.UpdatedAt = DateTime.UtcNow;
            var response = await _addressesContainer!.ReplaceItemAsync(address, address.Id, new PartitionKey(address.InstitutionId));
            _logger.LogInformation("Address updated: {AddressId}", address.Id);
            return response.Resource;
        }

        public async Task DeleteAddressAsync(string addressId)
        {
            EnsureInitialized();
            // First get the address to find the partition key
            var address = await GetAddressAsync(addressId);
            if (address != null)
            {
                await _addressesContainer!.DeleteItemAsync<MemberAddress>(addressId, new PartitionKey(address.InstitutionId));
                _logger.LogInformation("Address deleted: {AddressId}", addressId);
            }
        }

        public async Task<List<MemberAddress>> GetAddressesByInstitutionAsync(string institutionId)
        {
            EnsureInitialized();
            var query = new QueryDefinition("SELECT * FROM c WHERE c.institutionId = @institutionId")
                .WithParameter("@institutionId", institutionId);
            
            return await ExecuteQueryAsync<MemberAddress>(_addressesContainer!, query);
        }

        public async Task<List<MemberAddress>> GetActiveAddressesAsync()
        {
            EnsureInitialized();
            var query = new QueryDefinition("SELECT * FROM c WHERE c.isActive = true");
            return await ExecuteQueryAsync<MemberAddress>(_addressesContainer!, query);
        }

        public async Task<List<MemberAddress>> GetAddressesByFilterAsync(string institutionId, string? priority = null, bool? isActive = null)
        {
            EnsureInitialized();
            var queryText = "SELECT * FROM c WHERE c.institutionId = @institutionId";
            var queryDef = new QueryDefinition(queryText).WithParameter("@institutionId", institutionId);

            if (!string.IsNullOrEmpty(priority))
            {
                queryText += " AND c.priority = @priority";
                queryDef = queryDef.WithParameter("@priority", priority);
            }

            if (isActive.HasValue)
            {
                queryText += " AND c.isActive = @isActive";
                queryDef = queryDef.WithParameter("@isActive", isActive.Value);
            }

            queryDef = new QueryDefinition(queryText);
            if (!string.IsNullOrEmpty(priority))
                queryDef = queryDef.WithParameter("@priority", priority);
            if (isActive.HasValue)
                queryDef = queryDef.WithParameter("@isActive", isActive.Value);
            queryDef = queryDef.WithParameter("@institutionId", institutionId);

            return await ExecuteQueryAsync<MemberAddress>(_addressesContainer!, queryDef);
        }

        public async Task<List<MemberAddress>> CreateBulkAddressesAsync(List<MemberAddress> addresses)
        {
            EnsureInitialized();
            var results = new List<MemberAddress>();

            // Process in batches to avoid overwhelming Cosmos DB
            const int batchSize = 100;
            for (int i = 0; i < addresses.Count; i += batchSize)
            {
                var batch = addresses.Skip(i).Take(batchSize);
                var tasks = batch.Select(address => CreateAddressAsync(address));
                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults);
            }

            _logger.LogInformation("Bulk created {Count} addresses", results.Count);
            return results;
        }

        // Alert Management
        public async Task<PropertyAlert> CreateAlertAsync(PropertyAlert alert)
        {
            EnsureInitialized();
            var response = await _alertsContainer!.CreateItemAsync(alert, new PartitionKey(alert.InstitutionId));
            _logger.LogInformation("Alert created: {AlertId} for institution {InstitutionId}", alert.Id, alert.InstitutionId);
            return response.Resource;
        }

        public async Task<PropertyAlert?> GetAlertAsync(string alertId)
        {
            EnsureInitialized();
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @alertId")
                    .WithParameter("@alertId", alertId);
                
                var iterator = _alertsContainer!.GetItemQueryIterator<PropertyAlert>(query);
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<PropertyAlert> UpdateAlertAsync(PropertyAlert alert)
        {
            EnsureInitialized();
            var response = await _alertsContainer!.ReplaceItemAsync(alert, alert.Id, new PartitionKey(alert.InstitutionId));
            _logger.LogInformation("Alert updated: {AlertId}", alert.Id);
            return response.Resource;
        }

        public async Task<List<PropertyAlert>> GetAlertsByInstitutionAsync(string institutionId, int? limit = null)
        {
            EnsureInitialized();
            var queryText = "SELECT * FROM c WHERE c.institutionId = @institutionId ORDER BY c.createdAt DESC";
            var query = new QueryDefinition(queryText).WithParameter("@institutionId", institutionId);

            var requestOptions = new QueryRequestOptions();
            if (limit.HasValue)
                requestOptions.MaxItemCount = limit.Value;

            return await ExecuteQueryAsync<PropertyAlert>(_alertsContainer!, query, requestOptions);
        }

        public async Task<List<PropertyAlert>> GetPendingAlertsAsync()
        {
            EnsureInitialized();
            var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", AlertStatus.Pending.ToString());
            
            return await ExecuteQueryAsync<PropertyAlert>(_alertsContainer!, query);
        }

        public async Task<List<PropertyAlert>> GetAlertsByStatusAsync(AlertStatus status)
        {
            EnsureInitialized();
            var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", status.ToString());
            
            return await ExecuteQueryAsync<PropertyAlert>(_alertsContainer!, query);
        }

        // Scan Log Management
        public async Task<ScanLog> CreateScanLogAsync(ScanLog scanLog)
        {
            EnsureInitialized();
            var partitionKey = scanLog.InstitutionId ?? "system";
            var response = await _scanLogsContainer!.CreateItemAsync(scanLog, new PartitionKey(partitionKey));
            _logger.LogInformation("Scan log created: {ScanLogId}", scanLog.Id);
            return response.Resource;
        }

        public async Task<ScanLog?> GetScanLogAsync(string scanLogId)
        {
            EnsureInitialized();
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @scanLogId")
                    .WithParameter("@scanLogId", scanLogId);
                
                var iterator = _scanLogsContainer!.GetItemQueryIterator<ScanLog>(query);
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<ScanLog> UpdateScanLogAsync(ScanLog scanLog)
        {
            EnsureInitialized();
            var partitionKey = scanLog.InstitutionId ?? "system";
            var response = await _scanLogsContainer!.ReplaceItemAsync(scanLog, scanLog.Id, new PartitionKey(partitionKey));
            _logger.LogInformation("Scan log updated: {ScanLogId}", scanLog.Id);
            return response.Resource;
        }

        public async Task<List<ScanLog>> GetScanLogsByInstitutionAsync(string institutionId, int? limit = null)
        {
            EnsureInitialized();
            var queryText = "SELECT * FROM c WHERE c.institutionId = @institutionId ORDER BY c.startedAt DESC";
            var query = new QueryDefinition(queryText).WithParameter("@institutionId", institutionId);

            var requestOptions = new QueryRequestOptions();
            if (limit.HasValue)
                requestOptions.MaxItemCount = limit.Value;

            return await ExecuteQueryAsync<ScanLog>(_scanLogsContainer!, query, requestOptions);
        }

        public async Task<List<ScanLog>> GetRecentScanLogsAsync(int limit = 50)
        {
            EnsureInitialized();
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.startedAt DESC");
            var requestOptions = new QueryRequestOptions { MaxItemCount = limit };
            
            return await ExecuteQueryAsync<ScanLog>(_scanLogsContainer!, query, requestOptions);
        }

        public async Task<ScanLog?> GetActiveScanLogAsync(string? institutionId = null)
        {
            EnsureInitialized();
            var queryText = "SELECT * FROM c WHERE c.scanStatus IN (@inProgress, @started)";
            var query = new QueryDefinition(queryText)
                .WithParameter("@inProgress", ScanStatus.InProgress.ToString())
                .WithParameter("@started", ScanStatus.Started.ToString());

            if (!string.IsNullOrEmpty(institutionId))
            {
                queryText += " AND c.institutionId = @institutionId";
                query = query.WithParameter("@institutionId", institutionId);
            }

            var results = await ExecuteQueryAsync<ScanLog>(_scanLogsContainer!, query);
            return results.FirstOrDefault();
        }

        // Analytics and Reporting
        public async Task<ScanStatistics> GetScanStatisticsAsync(string? institutionId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            EnsureInitialized();
            // This is a simplified implementation - in production you might want to use aggregation queries
            var queryText = "SELECT * FROM c WHERE 1=1";
            var query = new QueryDefinition(queryText);

            if (!string.IsNullOrEmpty(institutionId))
            {
                queryText += " AND c.institutionId = @institutionId";
                query = query.WithParameter("@institutionId", institutionId);
            }

            if (fromDate.HasValue)
            {
                queryText += " AND c.startedAt >= @fromDate";
                query = query.WithParameter("@fromDate", fromDate.Value);
            }

            if (toDate.HasValue)
            {
                queryText += " AND c.startedAt <= @toDate";
                query = query.WithParameter("@toDate", toDate.Value);
            }

            query = new QueryDefinition(queryText);
            if (!string.IsNullOrEmpty(institutionId))
                query = query.WithParameter("@institutionId", institutionId);
            if (fromDate.HasValue)
                query = query.WithParameter("@fromDate", fromDate.Value);
            if (toDate.HasValue)
                query = query.WithParameter("@toDate", toDate.Value);

            var scanLogs = await ExecuteQueryAsync<ScanLog>(_scanLogsContainer!, query);

            return new ScanStatistics
            {
                TotalScans = scanLogs.Count,
                TotalAddressesScanned = scanLogs.Sum(s => s.AddressesScanned),
                TotalAlertsGenerated = scanLogs.Sum(s => s.AlertsGenerated),
                TotalApiCalls = scanLogs.Sum(s => s.ApiCallsMade),
                TotalErrors = scanLogs.Sum(s => s.ErrorsEncountered),
                AverageScanDuration = scanLogs.Where(s => s.Duration.HasValue).Any() 
                    ? TimeSpan.FromTicks((long)scanLogs.Where(s => s.Duration.HasValue).Average(s => s.Duration!.Value.Ticks))
                    : TimeSpan.Zero,
                LastScanAt = scanLogs.Max(s => s.StartedAt),
                StatusBreakdown = scanLogs.GroupBy(s => s.ScanStatus.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                ErrorBreakdown = new Dictionary<string, int>()
            };
        }

        public async Task<List<AlertSummary>> GetAlertSummaryAsync(string? institutionId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            EnsureInitialized();
            // Simplified implementation
            var institutions = string.IsNullOrEmpty(institutionId) 
                ? await GetAllInstitutionsAsync() 
                : new List<Institution> { (await GetInstitutionAsync(institutionId))! };

            var summaries = new List<AlertSummary>();

            foreach (var institution in institutions.Where(i => i != null))
            {
                var alerts = await GetAlertsByInstitutionAsync(institution.Id);
                
                if (fromDate.HasValue)
                    alerts = alerts.Where(a => a.CreatedAt >= fromDate.Value).ToList();
                
                if (toDate.HasValue)
                    alerts = alerts.Where(a => a.CreatedAt <= toDate.Value).ToList();

                summaries.Add(new AlertSummary
                {
                    InstitutionId = institution.Id,
                    InstitutionName = institution.Name,
                    TotalAlerts = alerts.Count,
                    PendingAlerts = alerts.Count(a => a.Status == AlertStatus.Pending),
                    SentAlerts = alerts.Count(a => a.Status == AlertStatus.Sent),
                    FailedAlerts = alerts.Count(a => a.Status == AlertStatus.Failed),
                    LastAlertAt = alerts.Any() ? alerts.Max(a => a.CreatedAt) : null,
                    StatusChangeBreakdown = alerts.GroupBy(a => a.NewStatus).ToDictionary(g => g.Key, g => g.Count())
                });
            }

            return summaries;
        }

        private async Task<List<T>> ExecuteQueryAsync<T>(Container container, QueryDefinition query, QueryRequestOptions? requestOptions = null)
        {
            var iterator = container.GetItemQueryIterator<T>(query, requestOptions: requestOptions);
            var results = new List<T>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        private void EnsureInitialized()
        {
            if (_database == null || _institutionsContainer == null || _addressesContainer == null || 
                _alertsContainer == null || _scanLogsContainer == null)
            {
                throw new InvalidOperationException("CosmosService not initialized. Call InitializeDatabaseAsync first.");
            }
        }
    }
}
