using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Services;

public interface IInstitutionService
{
    Task<PagedResult<Institution>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<Institution?>> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<Institution>> CreateAsync(string tenantId, string name, string timeZoneId, string? primaryContactEmail, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(string id, string name, string? primaryContactEmail, Domain.Enums.InstitutionStatus status, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<MemberAddress>> AddAddressAsync(string institutionId, Domain.ValueObjects.Address address, IEnumerable<string>? tags, CancellationToken cancellationToken = default);
    Task<Result> RemoveAddressAsync(string institutionId, string addressId, CancellationToken cancellationToken = default);
    Task<Result> UpsertAddressesBulkAsync(string institutionId, IEnumerable<MemberAddress> addresses, CancellationToken cancellationToken = default);
}
