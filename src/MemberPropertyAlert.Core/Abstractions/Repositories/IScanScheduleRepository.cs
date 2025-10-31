using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;

namespace MemberPropertyAlert.Core.Abstractions.Repositories;

public interface IScanScheduleRepository
{
    Task<Result<CronScheduleDefinition>> GetAsync(CancellationToken cancellationToken = default);
    Task<Result<CronScheduleDefinition>> UpsertAsync(CronScheduleDefinition definition, CancellationToken cancellationToken = default);
}
