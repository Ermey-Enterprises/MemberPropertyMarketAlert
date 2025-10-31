using System;
using Cronos;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Scheduling;

public sealed class CronScheduleDefinition
{
    private CronScheduleDefinition(string expression, string timeZoneId, DateTimeOffset? lastRunUtc)
    {
        Expression = expression;
        TimeZoneId = timeZoneId;
        LastRunUtc = lastRunUtc;
    }

    public string Expression { get; }
    public string TimeZoneId { get; }
    public DateTimeOffset? LastRunUtc { get; private set; }

    public static Result<CronScheduleDefinition> Create(string expression, string timeZoneId, DateTimeOffset? lastRunUtc = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Result<CronScheduleDefinition>.Failure("Cron expression is required.");
        }

        try
        {
            _ = CronExpression.Parse(expression, CronFormat.IncludeSeconds);
        }
        catch (Exception ex)
        {
            return Result<CronScheduleDefinition>.Failure($"Invalid cron expression: {ex.Message}");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result<CronScheduleDefinition>.Failure($"Unsupported time zone '{timeZoneId}'.");
        }

        return Result<CronScheduleDefinition>.Success(new CronScheduleDefinition(expression, timeZoneId, lastRunUtc));
    }

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset fromUtc)
    {
        var cron = CronExpression.Parse(Expression, CronFormat.IncludeSeconds);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        return cron.GetNextOccurrence(fromUtc, timeZone);
    }

    public void RecordRun(DateTimeOffset runAtUtc)
    {
        LastRunUtc = runAtUtc;
    }
}
