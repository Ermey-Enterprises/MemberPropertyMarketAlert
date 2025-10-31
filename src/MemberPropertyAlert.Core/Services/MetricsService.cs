using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Services;

public sealed class MetricsService : IMetricsService
{
    private readonly IInstitutionRepository _institutionRepository;
    private readonly IListingMatchRepository _listingMatchRepository;

    public MetricsService(IInstitutionRepository institutionRepository, IListingMatchRepository listingMatchRepository)
    {
        _institutionRepository = institutionRepository;
        _listingMatchRepository = listingMatchRepository;
    }

    public async Task<Result<DashboardMetrics>> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        var countsResult = await _institutionRepository.GetCountsAsync(cancellationToken);
        if (countsResult.IsFailure || countsResult.Value is null)
        {
            return Result<DashboardMetrics>.Failure(countsResult.Error ?? "Failed to retrieve counts.");
        }

        var recentAlerts = await _listingMatchRepository.ListRecentAsync(null, 1, 100, cancellationToken);
        var alerts24h = recentAlerts.Items.Count(m => m.DetectedAtUtc >= DateTimeOffset.UtcNow.AddHours(-24));
        var alerts7d = recentAlerts.Items.Count(m => m.DetectedAtUtc >= DateTimeOffset.UtcNow.AddDays(-7));
        var medianRent = CalculateMedian(recentAlerts.Items.Select(m => m.MonthlyRent).ToArray());
        var averageDelta = 0m; // Placeholder until pricing history available

        var metrics = new DashboardMetrics(
            countsResult.Value.Total,
            countsResult.Value.Active,
            countsResult.Value.AddressCount,
            countsResult.Value.ActiveAddressCount,
            alerts24h,
            alerts7d,
            0,
            averageDelta,
            medianRent);

        return Result<DashboardMetrics>.Success(metrics);
    }

    public async Task<Result<IReadOnlyCollection<RecentActivityItem>>> GetRecentActivityAsync(int take, CancellationToken cancellationToken = default)
    {
        var matches = await _listingMatchRepository.ListRecentAsync(null, 1, take, cancellationToken);
        var activities = matches.Items
            .Take(take)
            .Select(m => new RecentActivityItem(
                m.Id,
                $"Listing matched: {m.ListingAddress.City}",
                $"Rent ${m.MonthlyRent}",
                m.Severity,
                m.DetectedAtUtc,
                null,
                null))
            .ToList();

        return Result<IReadOnlyCollection<RecentActivityItem>>.Success(activities);
    }

    private static decimal CalculateMedian(decimal[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        Array.Sort(values);
        var mid = values.Length / 2;
        if (values.Length % 2 == 0)
        {
            return (values[mid - 1] + values[mid]) / 2;
        }

        return values[mid];
    }
}
