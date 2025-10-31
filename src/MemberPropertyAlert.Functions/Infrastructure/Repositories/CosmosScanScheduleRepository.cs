using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Repositories;

public sealed class CosmosScanScheduleRepository : IScanScheduleRepository
{
    private const string DocumentId = "scan-schedule";
    private const string PartitionKeyValue = "schedule";
    private const string DefaultCronExpression = "0 */30 * * * *";
    private const string DefaultTimeZoneId = "UTC";

    private readonly ICosmosContainerFactory _containerFactory;
    private readonly ILogger<CosmosScanScheduleRepository> _logger;

    public CosmosScanScheduleRepository(ICosmosContainerFactory containerFactory, ILogger<CosmosScanScheduleRepository> logger)
    {
        _containerFactory = containerFactory;
        _logger = logger;
    }

    public async Task<Result<CronScheduleDefinition>> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
            var response = await container.ReadItemAsync<ScanScheduleDocument>(DocumentId, new PartitionKey(PartitionKeyValue), cancellationToken: cancellationToken);
            return Result<CronScheduleDefinition>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Scan schedule not found. Returning default schedule expression {Expression} {TimeZone}", DefaultCronExpression, DefaultTimeZoneId);
            var defaultResult = CronScheduleDefinition.Create(DefaultCronExpression, DefaultTimeZoneId);
            if (defaultResult.IsFailure || defaultResult.Value is null)
            {
                return Result<CronScheduleDefinition>.Failure(defaultResult.Error ?? "Unable to create default schedule definition.");
            }

            return Result<CronScheduleDefinition>.Success(defaultResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve scan schedule");
            return Result<CronScheduleDefinition>.Failure(ex.Message);
        }
    }

    public async Task<Result<CronScheduleDefinition>> UpsertAsync(CronScheduleDefinition definition, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
            var document = ScanScheduleDocument.FromDomain(definition);
            await container.UpsertItemAsync(document, new PartitionKey(PartitionKeyValue), cancellationToken: cancellationToken);
            return Result<CronScheduleDefinition>.Success(definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert scan schedule");
            return Result<CronScheduleDefinition>.Failure(ex.Message);
        }
    }

    private sealed class ScanScheduleDocument
    {
        public string Id { get; set; } = DocumentId;
        public string StateOrProvince { get; set; } = PartitionKeyValue;
        public string Expression { get; set; } = DefaultCronExpression;
        public string TimeZoneId { get; set; } = DefaultTimeZoneId;
        public DateTimeOffset? LastRunUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public static ScanScheduleDocument FromDomain(CronScheduleDefinition definition)
        {
            return new ScanScheduleDocument
            {
                StateOrProvince = PartitionKeyValue,
                Expression = definition.Expression,
                TimeZoneId = definition.TimeZoneId,
                LastRunUtc = definition.LastRunUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        public CronScheduleDefinition ToDomain()
        {
            var definitionResult = CronScheduleDefinition.Create(Expression, TimeZoneId, LastRunUtc);
            if (definitionResult.IsSuccess && definitionResult.Value is not null)
            {
                return definitionResult.Value;
            }

            var fallbackResult = CronScheduleDefinition.Create(DefaultCronExpression, DefaultTimeZoneId, LastRunUtc);
            if (fallbackResult.IsFailure || fallbackResult.Value is null)
            {
                throw new InvalidOperationException(fallbackResult.Error ?? "Unable to hydrate scan schedule definition.");
            }

            return fallbackResult.Value;
        }
    }
}
