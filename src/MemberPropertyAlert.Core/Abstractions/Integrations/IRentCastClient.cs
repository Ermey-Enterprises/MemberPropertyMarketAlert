using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Integrations;

public interface IRentCastClient
{
    Task<Result<IReadOnlyCollection<RentCastListing>>> GetListingsAsync(string stateOrProvince, CancellationToken cancellationToken = default);
    Task<Result<RentCastListing?>> GetListingAsync(string listingId, CancellationToken cancellationToken = default);
}
