using MemberPropertyAlert.Core.Domain.Enums;

namespace MemberPropertyAlert.Core.Models;

public sealed record RecentActivityItem(
    string ActivityId,
    string Title,
    string Description,
    AlertSeverity Severity,
    DateTimeOffset OccurredAtUtc,
    string? InstitutionId,
    string? MetadataJson
);
