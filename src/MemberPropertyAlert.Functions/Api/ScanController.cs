using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using MemberPropertyAlert.Functions.Services;

namespace MemberPropertyAlert.Functions.Api;

public class ScanController
{
    private readonly ILogger<ScanController> _logger;
    private readonly CosmosService _cosmosService;
    private readonly PropertyScanService _propertyScanService;
    private readonly NotificationService _notificationService;

    public ScanController(
        ILogger<ScanController> logger,
        CosmosService cosmosService,
        PropertyScanService propertyScanService,
        NotificationService notificationService)
    {
        _logger = logger;
        _cosmosService = cosmosService;
        _propertyScanService = propertyScanService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Start a manual scan for a specific institution
    /// </summary>
    [Function("StartManualScan")]
    public async Task<HttpResponseData> StartManualScan(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scan/start")] HttpRequestData req)
    {
        _logger.LogInformation("StartManualScan function executed");

        try
        {
            // Get institution ID from query parameters or headers
            string? institutionId = req.Query["institutionId"];
            if (string.IsNullOrEmpty(institutionId))
            {
                _logger.LogWarning("Institution ID not provided in request");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Institution ID is required" }));
                return badRequestResponse;
            }

            // Parse request body if provided
            var scanRequest = new { Priority = "Normal", ForceRescan = false };
            if (req.Body.Length > 0)
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (!string.IsNullOrEmpty(requestBody))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<dynamic>(requestBody);
                        // Use parsed values if needed
                    }
                    catch
                    {
                        // Use defaults if parsing fails
                    }
                }
            }

            _logger.LogInformation("Starting manual scan for institution {InstitutionId}", institutionId);

            // Check if there's already an active scan
            var activeScan = await _cosmosService.GetActiveScanLogAsync(institutionId);
            if (activeScan != null)
            {
                _logger.LogWarning("Active scan already exists for institution {InstitutionId}: {ScanId}", 
                    institutionId, activeScan.Id);
                
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                    error = "An active scan is already running for this institution",
                    activeScanId = activeScan.Id
                }));
                return conflictResponse;
            }

            // Create a manual scan request object
            var manualScanRequest = new MemberPropertyAlert.Functions.Models.ManualScanRequest
            {
                Priority = scanRequest.Priority,
                ForceRescan = scanRequest.ForceRescan
            };

            // Trigger the manual scan using the stub service
            var scanLog = await _propertyScanService.TriggerManualScanAsync(institutionId, manualScanRequest);

            _logger.LogInformation("Manual scan started successfully: {ScanId}", scanLog.Id);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponse = new
            {
                ScanId = scanLog.Id,
                InstitutionId = scanLog.InstitutionId,
                ScanType = scanLog.ScanType.ToString(),
                Status = scanLog.ScanStatus.ToString(),
                StartedAt = scanLog.StartedAt,
                CompletedAt = scanLog.CompletedAt,
                AddressesScanned = scanLog.AddressesScanned,
                AlertsGenerated = scanLog.AlertsGenerated,
                ApiCallsMade = scanLog.ApiCallsMade,
                ErrorsEncountered = scanLog.ErrorsEncountered,
                ErrorMessage = scanLog.ErrorMessage
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting manual scan");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while starting scan",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Get the status of a specific scan
    /// </summary>
    [Function("GetScanStatus")]
    public async Task<HttpResponseData> GetScanStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "scan/{scanId}/status")] HttpRequestData req,
        string scanId)
    {
        _logger.LogInformation("GetScanStatus function executed for scan {ScanId}", scanId);

        try
        {
            var scanLog = await _cosmosService.GetScanLogAsync(scanId);
            if (scanLog == null)
            {
                _logger.LogWarning("Scan not found: {ScanId}", scanId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Scan not found" }));
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponse = new
            {
                ScanId = scanLog.Id,
                InstitutionId = scanLog.InstitutionId,
                ScanType = scanLog.ScanType.ToString(),
                Status = scanLog.ScanStatus.ToString(),
                StartedAt = scanLog.StartedAt,
                CompletedAt = scanLog.CompletedAt,
                AddressesScanned = scanLog.AddressesScanned,
                AlertsGenerated = scanLog.AlertsGenerated,
                ApiCallsMade = scanLog.ApiCallsMade,
                ErrorsEncountered = scanLog.ErrorsEncountered,
                ErrorMessage = scanLog.ErrorMessage
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan status for {ScanId}", scanId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while getting scan status",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Get scan history for an institution
    /// </summary>
    [Function("GetScanHistory")]
    public async Task<HttpResponseData> GetScanHistory(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "scan/history")] HttpRequestData req)
    {
        _logger.LogInformation("GetScanHistory function executed");

        try
        {
            string? institutionId = req.Query["institutionId"];
            string? limitStr = req.Query["limit"];
            
            int limit = 50; // default
            if (!string.IsNullOrEmpty(limitStr) && int.TryParse(limitStr, out int parsedLimit))
            {
                limit = Math.Min(parsedLimit, 100); // cap at 100
            }

            var scanLogs = !string.IsNullOrEmpty(institutionId)
                ? await _cosmosService.GetScanLogsByInstitutionAsync(institutionId, limit)
                : await _cosmosService.GetRecentScanLogsAsync(limit);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponses = scanLogs.Select(log => new
            {
                ScanId = log.Id,
                InstitutionId = log.InstitutionId,
                ScanType = log.ScanType.ToString(),
                Status = log.ScanStatus.ToString(),
                StartedAt = log.StartedAt,
                CompletedAt = log.CompletedAt,
                AddressesScanned = log.AddressesScanned,
                AlertsGenerated = log.AlertsGenerated,
                ApiCallsMade = log.ApiCallsMade,
                ErrorsEncountered = log.ErrorsEncountered,
                ErrorMessage = log.ErrorMessage
            }).ToList();
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponses));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan history");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while getting scan history",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Stop an active scan
    /// </summary>
    [Function("StopScan")]
    public async Task<HttpResponseData> StopScan(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scan/{scanId}/stop")] HttpRequestData req,
        string scanId)
    {
        _logger.LogInformation("StopScan function executed for scan {ScanId}", scanId);

        try
        {
            var scanLog = await _cosmosService.GetScanLogAsync(scanId);
            if (scanLog == null)
            {
                _logger.LogWarning("Scan not found: {ScanId}", scanId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Scan not found" }));
                return notFoundResponse;
            }

            if (scanLog.ScanStatus != MemberPropertyAlert.Core.Models.ScanStatus.Started && 
                scanLog.ScanStatus != MemberPropertyAlert.Core.Models.ScanStatus.InProgress)
            {
                _logger.LogWarning("Cannot stop scan {ScanId} - current status: {Status}", scanId, scanLog.ScanStatus);
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                    error = "Scan cannot be stopped in current state",
                    currentStatus = scanLog.ScanStatus.ToString()
                }));
                return badRequestResponse;
            }

            // Update scan status to completed (since there's no Stopped status)
            scanLog.ScanStatus = MemberPropertyAlert.Core.Models.ScanStatus.Completed;
            scanLog.CompletedAt = DateTime.UtcNow;
            scanLog.ErrorMessage = "Scan stopped by user request";

            await _cosmosService.UpdateScanLogAsync(scanLog);

            _logger.LogInformation("Scan stopped successfully: {ScanId}", scanId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var scanResponse = new
            {
                ScanId = scanLog.Id,
                InstitutionId = scanLog.InstitutionId,
                ScanType = scanLog.ScanType.ToString(),
                Status = scanLog.ScanStatus.ToString(),
                StartedAt = scanLog.StartedAt,
                CompletedAt = scanLog.CompletedAt,
                AddressesScanned = scanLog.AddressesScanned,
                AlertsGenerated = scanLog.AlertsGenerated,
                ApiCallsMade = scanLog.ApiCallsMade,
                ErrorsEncountered = scanLog.ErrorsEncountered,
                ErrorMessage = scanLog.ErrorMessage
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(scanResponse));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping scan {ScanId}", scanId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { 
                error = "Internal server error occurred while stopping scan",
                details = ex.Message
            }));
            return errorResponse;
        }
    }
}
