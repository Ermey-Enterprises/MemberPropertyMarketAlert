using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Services;

public interface IListingMatchService
{
    Task<Result<IReadOnlyCollection<ListingMatch>>> FindMatchesAsync(
        string stateOrProvince,
        IReadOnlyCollection<Domain.ValueObjects.TenantInstitutionScope> scopes,
        CancellationToken cancellationToken = default);
    Task<Result> PublishMatchesAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default);
    Task<PagedResult<ListingMatch>> GetRecentMatchesAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
