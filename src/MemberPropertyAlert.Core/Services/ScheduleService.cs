using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;

namespace MemberPropertyAlert.Core.Services;

public sealed class ScheduleService : IScheduleService
{
    private readonly IScanScheduleRepository _repository;

    public ScheduleService(IScanScheduleRepository repository)
    {
        _repository = repository;
    }

    public Task<Result<CronScheduleDefinition>> GetScheduleAsync(CancellationToken cancellationToken = default)
        => _repository.GetAsync(cancellationToken);

    public async Task<Result<CronScheduleDefinition>> UpdateScheduleAsync(string cronExpression, string timeZoneId, CancellationToken cancellationToken = default)
    {
        var definitionResult = CronScheduleDefinition.Create(cronExpression, timeZoneId);
        if (definitionResult.IsFailure || definitionResult.Value is null)
        {
            return Result<CronScheduleDefinition>.Failure(definitionResult.Error ?? "Invalid schedule definition.");
        }

        return await _repository.UpsertAsync(definitionResult.Value, cancellationToken);
    }
}
