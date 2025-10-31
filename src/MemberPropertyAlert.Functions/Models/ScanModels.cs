using MemberPropertyAlert.Core.Scheduling;

namespace MemberPropertyAlert.Functions.Models;

public sealed record StartScanRequest(string StateOrProvince);

public sealed record ScheduleUpdateRequest(string CronExpression, string TimeZoneId);

public sealed record ScheduleResponse(string CronExpression, string TimeZoneId, System.DateTimeOffset? LastRunUtc)
{
    public static ScheduleResponse FromDefinition(CronScheduleDefinition definition) => new(definition.Expression, definition.TimeZoneId, definition.LastRunUtc);
}
