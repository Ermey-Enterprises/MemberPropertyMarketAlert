using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;
using MemberPropertyAlert.Functions.Infrastructure.Telemetry;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Functions;

public sealed class ScanSchedulerFunction
{
    private const string SchedulerPrincipalName = "scan-scheduler";
    private const string SchedulerObjectId = "scan-scheduler";
    private const string SchedulerTenantId = "system";
    private const int InstitutionPageSize = 100;

    private readonly IScheduleService _scheduleService;
    private readonly IScanScheduleRepository _scanScheduleRepository;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IScanOrchestrator _scanOrchestrator;
    private readonly ITenantRequestContextAccessor _tenantContextAccessor;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogStreamPublisher _logStreamPublisher;
    private readonly ILogger<ScanSchedulerFunction> _logger;

    public ScanSchedulerFunction(
        IScheduleService scheduleService,
        IScanScheduleRepository scanScheduleRepository,
        IInstitutionRepository institutionRepository,
        IScanOrchestrator scanOrchestrator,
        ITenantRequestContextAccessor tenantContextAccessor,
        IAuditLogger auditLogger,
        ILogStreamPublisher logStreamPublisher,
        ILogger<ScanSchedulerFunction> logger)
    {
        _scheduleService = scheduleService;
        _scanScheduleRepository = scanScheduleRepository;
        _institutionRepository = institutionRepository;
        _scanOrchestrator = scanOrchestrator;
        _tenantContextAccessor = tenantContextAccessor;
        _auditLogger = auditLogger;
        _logStreamPublisher = logStreamPublisher;
        _logger = logger;
    }

    [Function("ScanSchedulerFunction")]
    public async Task Run([
        TimerTrigger("0 */5 * * * *", RunOnStartup = false)] TimerInfo timerInfo,
        FunctionContext context)
    {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        await ExecuteAsync(DateTimeOffset.UtcNow, cancellationToken, timerInfo);
    }

    public async Task ExecuteAsync(DateTimeOffset triggeredAtUtc, CancellationToken cancellationToken, TimerInfo? timerInfo = null)
    {
        _logger.LogInformation("Scan scheduler triggered at {TriggeredAtUtc}. Past due: {IsPastDue}", triggeredAtUtc, timerInfo?.IsPastDue ?? false);

        var scheduleResult = await _scheduleService.GetScheduleAsync(cancellationToken);
        if (scheduleResult.IsFailure || scheduleResult.Value is null)
        {
            _logger.LogError("Unable to retrieve scan schedule: {Error}", scheduleResult.Error);
            return;
        }

        var scheduleDefinition = scheduleResult.Value;
        if (!IsDue(scheduleDefinition, triggeredAtUtc))
        {
            var nextOccurrence = scheduleDefinition.LastRunUtc.HasValue
                ? scheduleDefinition.GetNextOccurrence(scheduleDefinition.LastRunUtc.Value)
                : scheduleDefinition.GetNextOccurrence(triggeredAtUtc);

            if (nextOccurrence is not null)
            {
                _logger.LogInformation(
                    "Scan schedule not due. Next occurrence at {NextOccurrenceUtc} (stored cron: {CronExpression})",
                    nextOccurrence.Value,
                    scheduleDefinition.Expression);
            }
            else
            {
                _logger.LogInformation("Scan schedule not due; cron expression did not yield a next occurrence.");
            }

            return;
        }

        var targets = await GetScanTargetsAsync(cancellationToken);
        if (targets.Count == 0)
        {
            _logger.LogInformation("No active scan targets were discovered.");
            await PersistLastRunAsync(scheduleDefinition, triggeredAtUtc, cancellationToken);
            return;
        }

        _logger.LogInformation("Discovered {TargetCount} scan targets for scheduled execution.", targets.Count);

        foreach (var target in targets)
        {
            await ProcessTargetAsync(target, triggeredAtUtc, cancellationToken);
        }

        await PersistLastRunAsync(scheduleDefinition, triggeredAtUtc, cancellationToken);
    }

    private static bool IsDue(CronScheduleDefinition scheduleDefinition, DateTimeOffset triggeredAtUtc)
    {
        if (scheduleDefinition.LastRunUtc is null)
        {
            return true;
        }

        var nextOccurrence = scheduleDefinition.GetNextOccurrence(scheduleDefinition.LastRunUtc.Value);
        if (nextOccurrence is null)
        {
            return true;
        }

        return triggeredAtUtc >= nextOccurrence.Value;
    }

    private async Task<IReadOnlyList<ScanTarget>> GetScanTargetsAsync(CancellationToken cancellationToken)
    {
        var targets = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        var platformContext = CreatePlatformAdminContext();
        _tenantContextAccessor.SetCurrent(platformContext);

        try
        {
            var pageNumber = 1;
            while (true)
            {
                var page = await _institutionRepository.ListAsync(pageNumber, InstitutionPageSize, cancellationToken);
                if (page.Items.Count == 0)
                {
                    break;
                }

                foreach (var institution in page.Items)
                {
                    if (institution.Status != InstitutionStatus.Active)
                    {
                        continue;
                    }

                    if (!targets.TryGetValue(institution.TenantId, out var stateMap))
                    {
                        stateMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                        targets[institution.TenantId] = stateMap;
                    }

                    foreach (var state in institution.Addresses
                        .Where(address => address.IsActive)
                        .Select(address => address.Address.StateOrProvince)
                        .Where(state => !string.IsNullOrWhiteSpace(state)))
                    {
                        if (!stateMap.TryGetValue(state, out var institutions))
                        {
                            institutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            stateMap[state] = institutions;
                        }

                        institutions.Add(institution.Id);
                    }
                }

                if (page.Items.Count < InstitutionPageSize)
                {
                    break;
                }

                pageNumber++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate scan targets.");
            return Array.Empty<ScanTarget>();
        }
        finally
        {
            _tenantContextAccessor.Clear();
        }

        if (targets.Count == 0)
        {
            return Array.Empty<ScanTarget>();
        }

        return targets
            .SelectMany(tenantEntry => tenantEntry.Value.Select(stateEntry => new ScanTarget(
                tenantEntry.Key,
                stateEntry.Key,
                stateEntry.Value.ToArray())))
            .ToArray();
    }

    private async Task ProcessTargetAsync(ScanTarget target, DateTimeOffset triggeredAtUtc, CancellationToken cancellationToken)
    {
        var tenantContext = CreateTenantContext(target);
        _tenantContextAccessor.SetCurrent(tenantContext);

        var auditProperties = new Dictionary<string, string?>
        {
            ["targetTenantId"] = target.TenantId,
            ["stateOrProvince"] = target.StateOrProvince,
            ["institutions"] = string.Join(',', target.InstitutionIds),
            ["triggeredAtUtc"] = triggeredAtUtc.ToString("O")
        };

        try
        {
            _logger.LogInformation(
                "Starting scheduled scan for tenant {TenantId} across {InstitutionCount} institutions in {State}.",
                target.TenantId,
                target.InstitutionIds.Count,
                target.StateOrProvince);

            await _auditLogger.TrackEventAsync("ScheduledScanTriggered", auditProperties, cancellationToken);

            await _logStreamPublisher.PublishAsync(
                new LogEvent(
                    Guid.NewGuid().ToString("N"),
                    $"Scheduled scan triggered for {target.StateOrProvince}.",
                    AlertSeverity.Informational,
                    DateTimeOffset.UtcNow,
                    nameof(ScanSchedulerFunction),
                    target.GetRepresentativeInstitutionId(),
                    null),
                cancellationToken);

            var result = await _scanOrchestrator.StartScanAsync(target.StateOrProvince, cancellationToken);
            if (result.IsSuccess)
            {
                await _auditLogger.TrackEventAsync("ScheduledScanSucceeded", auditProperties, cancellationToken);

                await _logStreamPublisher.PublishAsync(
                    new LogEvent(
                        Guid.NewGuid().ToString("N"),
                        $"Scheduled scan started for {target.StateOrProvince}.",
                        AlertSeverity.Informational,
                        DateTimeOffset.UtcNow,
                        nameof(ScanSchedulerFunction),
                        target.GetRepresentativeInstitutionId(),
                        null),
                    cancellationToken);

                _logger.LogInformation(
                    "Scheduled scan successfully initiated for tenant {TenantId} in {State}.",
                    target.TenantId,
                    target.StateOrProvince);
            }
            else
            {
                auditProperties["error"] = result.Error;
                await _auditLogger.TrackEventAsync("ScheduledScanFailed", auditProperties, cancellationToken);

                await _logStreamPublisher.PublishAsync(
                    new LogEvent(
                        Guid.NewGuid().ToString("N"),
                        $"Failed to start scheduled scan for {target.StateOrProvince}: {result.Error}",
                        AlertSeverity.Warning,
                        DateTimeOffset.UtcNow,
                        nameof(ScanSchedulerFunction),
                        target.GetRepresentativeInstitutionId(),
                        result.Error),
                    cancellationToken);

                _logger.LogWarning(
                    "Failed to start scheduled scan for tenant {TenantId} in {State}: {Error}",
                    target.TenantId,
                    target.StateOrProvince,
                    result.Error);
            }
        }
        catch (Exception ex)
        {
            auditProperties["error"] = ex.Message;
            await _auditLogger.TrackEventAsync("ScheduledScanException", auditProperties, cancellationToken);

            await _logStreamPublisher.PublishAsync(
                new LogEvent(
                    Guid.NewGuid().ToString("N"),
                    $"Unhandled error while running scheduled scan for {target.StateOrProvince}: {ex.Message}",
                    AlertSeverity.Critical,
                    DateTimeOffset.UtcNow,
                    nameof(ScanSchedulerFunction),
                    target.GetRepresentativeInstitutionId(),
                    ex.ToString()),
                cancellationToken);

            _logger.LogError(ex, "Unhandled error during scheduled scan for tenant {TenantId} in {State}.", target.TenantId, target.StateOrProvince);
        }
        finally
        {
            _tenantContextAccessor.Clear();
        }
    }

    private async Task PersistLastRunAsync(CronScheduleDefinition scheduleDefinition, DateTimeOffset triggeredAtUtc, CancellationToken cancellationToken)
    {
        scheduleDefinition.RecordRun(triggeredAtUtc);
        var persistResult = await _scanScheduleRepository.UpsertAsync(scheduleDefinition, cancellationToken);
        if (persistResult is null)
        {
            _logger.LogError("Failed to persist scan schedule metadata: repository returned no result.");
            return;
        }

        if (persistResult.IsFailure)
        {
            _logger.LogError("Failed to persist scan schedule metadata: {Error}", persistResult.Error);
        }
    }

    private static TenantRequestContext CreatePlatformAdminContext()
    {
        var identity = new ClaimsIdentity(new[] { new Claim("roles", SchedulerPrincipalName) }, SchedulerPrincipalName);
        var principal = new ClaimsPrincipal(identity);
        return new TenantRequestContext(
            principal,
            SchedulerTenantId,
            SchedulerTenantId,
            true,
            SchedulerObjectId,
            SchedulerPrincipalName,
            Guid.NewGuid().ToString(),
            new[] { SchedulerPrincipalName });
    }

    private static TenantRequestContext CreateTenantContext(ScanTarget target)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("roles", SchedulerPrincipalName) }, SchedulerPrincipalName);
        var principal = new ClaimsPrincipal(identity);
        var representativeInstitutionId = target.GetRepresentativeInstitutionId();
        return new TenantRequestContext(
            principal,
            target.TenantId,
            representativeInstitutionId,
            false,
            SchedulerObjectId,
            SchedulerPrincipalName,
            Guid.NewGuid().ToString(),
            new[] { SchedulerPrincipalName });
    }

    private sealed record ScanTarget(string TenantId, string StateOrProvince, IReadOnlyCollection<string> InstitutionIds)
    {
        public string GetRepresentativeInstitutionId()
            => InstitutionIds.FirstOrDefault() ?? TenantId;
    }
}
