using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Messaging;

public interface IAlertPublisher
{
    Task<Result> PublishAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default);
}
