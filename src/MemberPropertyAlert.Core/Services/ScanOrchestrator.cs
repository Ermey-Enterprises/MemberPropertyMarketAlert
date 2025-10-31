using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Core.Services;

public sealed class ScanOrchestrator : IScanOrchestrator
{
    private readonly IScanJobRepository _scanJobRepository;
    private readonly IListingMatchService _listingMatchService;
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<ScanOrchestrator> _logger;

    public ScanOrchestrator(
        IScanJobRepository scanJobRepository,
        IListingMatchService listingMatchService,
        IInstitutionRepository institutionRepository,
        IScheduleService scheduleService,
        ILogger<ScanOrchestrator> logger)
    {
        _scanJobRepository = scanJobRepository;
        _listingMatchService = listingMatchService;
        _institutionRepository = institutionRepository;
        _scheduleService = scheduleService;
        _logger = logger;
    }

    public async Task<Result> StartScanAsync(string stateOrProvince, CancellationToken cancellationToken = default)
    {
        var scanJobResult = ScanJob.Create(Guid.NewGuid().ToString("N"), stateOrProvince, Array.Empty<string>());
        if (scanJobResult.IsFailure || scanJobResult.Value is null)
        {
            return Result.Failure(scanJobResult.Error ?? "Failed to create scan job.");
        }

        var scanJob = scanJobResult.Value;
        var createResult = await _scanJobRepository.CreateAsync(scanJob, cancellationToken);
        if (createResult.IsFailure)
        {
            return createResult;
        }

        scanJob.MarkRunning();
        await _scanJobRepository.UpdateAsync(scanJob, cancellationToken);

        try
        {
            var matchesResult = await _listingMatchService.FindMatchesAsync(stateOrProvince, cancellationToken);
            if (matchesResult.IsFailure)
            {
                scanJob.MarkFailed(matchesResult.Error ?? "Failed to find matches.");
                await _scanJobRepository.UpdateAsync(scanJob, cancellationToken);
                return Result.Failure(matchesResult.Error ?? "Failed to find matches.");
            }

            var publishResult = await _listingMatchService.PublishMatchesAsync(matchesResult.Value ?? Array.Empty<ListingMatch>(), cancellationToken);
            if (publishResult.IsFailure)
            {
                scanJob.MarkFailed(publishResult.Error ?? "Failed to publish alerts.");
                await _scanJobRepository.UpdateAsync(scanJob, cancellationToken);
                return publishResult;
            }

            scanJob.MarkCompleted();
            await _scanJobRepository.UpdateAsync(scanJob, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed for state {State}", stateOrProvince);
            scanJob.MarkFailed(ex.Message);
            await _scanJobRepository.UpdateAsync(scanJob, cancellationToken);
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> StopScanAsync(string scanJobId, CancellationToken cancellationToken = default)
    {
        var jobResult = await _scanJobRepository.GetAsync(scanJobId, cancellationToken);
        if (jobResult.IsFailure)
        {
            return jobResult;
        }

        var job = jobResult.Value;
        if (job is null)
        {
            return Result.Failure("Scan job not found.");
        }

        job.Cancel("Manually cancelled");
        return await _scanJobRepository.UpdateAsync(job, cancellationToken);
    }

    public Task<Result<CronScheduleDefinition>> ScheduleScanAsync(string cronExpression, string timeZoneId, CancellationToken cancellationToken = default)
        => _scheduleService.UpdateScheduleAsync(cronExpression, timeZoneId, cancellationToken);

    public async Task<Result<ScanStatusSummary>> GetScanStatusAsync(CancellationToken cancellationToken = default)
    {
        var latestResult = await _scanJobRepository.GetLatestAsync(cancellationToken);
        if (latestResult.IsFailure)
        {
            return Result<ScanStatusSummary>.Failure(latestResult.Error ?? "Failed to retrieve latest scan job.");
        }

        var countsResult = await _institutionRepository.GetCountsAsync(cancellationToken);
        if (countsResult.IsFailure)
        {
            return Result<ScanStatusSummary>.Failure(countsResult.Error ?? "Failed to retrieve institution counts.");
        }

        var latestJob = latestResult.Value;
        var counts = countsResult.Value;
        if (counts is null)
        {
            return Result<ScanStatusSummary>.Failure("No institution metrics available.");
        }

        var institutionCounts = new Dictionary<string, int>
        {
            ["total"] = counts.Total,
            ["active"] = counts.Active,
            ["addresses"] = counts.AddressCount,
            ["activeAddresses"] = counts.ActiveAddressCount
        };

        var status = latestJob?.Status ?? ScanStatus.Pending;
        var summary = new ScanStatusSummary(status, latestJob?.Id, institutionCounts, latestJob?.CompletedAtUtc);
        return Result<ScanStatusSummary>.Success(summary);
    }
}
