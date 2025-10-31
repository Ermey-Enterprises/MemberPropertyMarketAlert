using MemberPropertyAlert.Core.Domain.Enums;

namespace MemberPropertyAlert.Core.Models;

public sealed record LogEvent(
    string Id,
    string Message,
    AlertSeverity Severity,
    DateTimeOffset OccurredAtUtc,
    string? Category,
    string? InstitutionId,
    string? Exception
);
