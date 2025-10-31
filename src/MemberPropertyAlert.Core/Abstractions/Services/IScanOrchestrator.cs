using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Core.Scheduling;

namespace MemberPropertyAlert.Core.Abstractions.Services;

public interface IScanOrchestrator
{
    Task<Result> StartScanAsync(string stateOrProvince, CancellationToken cancellationToken = default);
    Task<Result> StopScanAsync(string scanJobId, CancellationToken cancellationToken = default);
    Task<Result<MemberPropertyAlert.Core.Scheduling.CronScheduleDefinition>> ScheduleScanAsync(string cronExpression, string timeZoneId, CancellationToken cancellationToken = default);
    Task<Result<ScanStatusSummary>> GetScanStatusAsync(CancellationToken cancellationToken = default);
}
