using System;
using System.Collections.Generic;
using System.Linq;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Domain.Entities;

public sealed class ScanJob : AggregateRoot
{
    private readonly List<string> _institutionIds = new();

    public string StateOrProvince { get; private set; }
    public IReadOnlyCollection<string> InstitutionIds => _institutionIds.AsReadOnly();
    public ScanStatus Status { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    private ScanJob(
        string id,
        string stateOrProvince,
        IEnumerable<string> institutionIds,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? updatedAtUtc = null,
        ScanStatus? status = null,
        DateTimeOffset? startedAtUtc = null,
        DateTimeOffset? completedAtUtc = null,
        string? failureReason = null)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        StateOrProvince = stateOrProvince;
        _institutionIds.AddRange(institutionIds);
        Status = status ?? ScanStatus.Pending;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        FailureReason = failureReason;
    }

    public static Result<ScanJob> Create(string id, string stateOrProvince, IEnumerable<string>? institutionIds)
    {
        if (string.IsNullOrWhiteSpace(stateOrProvince) || stateOrProvince.Length > 5)
        {
            return Result<ScanJob>.Failure("State or province must be provided using a short code.");
        }

        var ids = institutionIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? new List<string>();

        return Result<ScanJob>.Success(new ScanJob(id, stateOrProvince.Trim().ToUpperInvariant(), ids));
    }

    public static ScanJob Rehydrate(
        string id,
        string stateOrProvince,
        IEnumerable<string> institutionIds,
        ScanStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        string? failureReason)
        => new(id, stateOrProvince, institutionIds, createdAtUtc, updatedAtUtc, status, startedAtUtc, completedAtUtc, failureReason);

    public void MarkRunning()
    {
        Status = ScanStatus.Running;
        StartedAtUtc = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkCompleted()
    {
        Status = ScanStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkFailed(string reason)
    {
        Status = ScanStatus.Failed;
        FailureReason = reason;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel(string reason)
    {
        Status = ScanStatus.Cancelled;
        FailureReason = reason;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Touch();
    }
}
