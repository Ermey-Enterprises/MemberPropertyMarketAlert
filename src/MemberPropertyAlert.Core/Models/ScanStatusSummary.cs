using System.Collections.Generic;
using MemberPropertyAlert.Core.Domain.Enums;

namespace MemberPropertyAlert.Core.Models;

public sealed record ScanStatusSummary(
    ScanStatus CurrentStatus,
    string? ActiveScanJobId,
    IReadOnlyDictionary<string, int> InstitutionCounts,
    DateTimeOffset? LastCompletedAtUtc
);
