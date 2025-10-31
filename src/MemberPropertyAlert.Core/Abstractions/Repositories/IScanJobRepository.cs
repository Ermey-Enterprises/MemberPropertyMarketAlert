using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Repositories;

public interface IScanJobRepository
{
    Task<Result<ScanJob>> CreateAsync(ScanJob scanJob, CancellationToken cancellationToken = default);
    Task<Result<ScanJob?>> GetAsync(string scanJobId, CancellationToken cancellationToken = default);
    Task<Result<ScanJob?>> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<ScanJob>> ListRecentAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(ScanJob scanJob, CancellationToken cancellationToken = default);
}
