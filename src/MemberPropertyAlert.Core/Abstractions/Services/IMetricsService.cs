using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Services;

public interface IMetricsService
{
    Task<Result<DashboardMetrics>> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<RecentActivityItem>>> GetRecentActivityAsync(int take, CancellationToken cancellationToken = default);
}
