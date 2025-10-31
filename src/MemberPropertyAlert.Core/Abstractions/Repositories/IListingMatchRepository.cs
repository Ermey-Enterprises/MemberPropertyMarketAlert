using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Repositories;

public interface IListingMatchRepository
{
    Task<Result<ListingMatch>> CreateAsync(ListingMatch match, CancellationToken cancellationToken = default);
    Task<PagedResult<ListingMatch>> ListRecentAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Result> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}
