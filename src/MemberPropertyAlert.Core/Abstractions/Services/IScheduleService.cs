using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;

namespace MemberPropertyAlert.Core.Abstractions.Services;

public interface IScheduleService
{
    Task<Result<CronScheduleDefinition>> GetScheduleAsync(CancellationToken cancellationToken = default);
    Task<Result<CronScheduleDefinition>> UpdateScheduleAsync(string cronExpression, string timeZoneId, CancellationToken cancellationToken = default);
}
