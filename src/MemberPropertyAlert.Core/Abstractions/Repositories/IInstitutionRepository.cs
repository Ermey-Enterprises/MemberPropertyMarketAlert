using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Repositories;

public interface IInstitutionRepository
{
    Task<Result<Institution>> CreateAsync(Institution institution, CancellationToken cancellationToken = default);
    Task<Result<Institution?>> GetAsync(string institutionId, CancellationToken cancellationToken = default);
    Task<PagedResult<Institution>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<MemberPropertyAlert.Core.Models.InstitutionCounts>> GetCountsAsync(CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(Institution institution, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string institutionId, CancellationToken cancellationToken = default);
}
