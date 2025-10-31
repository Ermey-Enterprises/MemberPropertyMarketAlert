using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Repositories;

public interface IMemberAddressRepository
{
    Task<Result<MemberAddress>> CreateAsync(MemberAddress address, CancellationToken cancellationToken = default);
    Task<Result<MemberAddress?>> GetAsync(string institutionId, string addressId, CancellationToken cancellationToken = default);
    Task<PagedResult<MemberAddress>> ListByInstitutionAsync(string institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MemberAddress>> ListByStateAsync(string stateOrProvince, CancellationToken cancellationToken = default);
    Task<Result> UpsertBulkAsync(string institutionId, IReadOnlyCollection<MemberAddress> addresses, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string institutionId, string addressId, CancellationToken cancellationToken = default);
}
