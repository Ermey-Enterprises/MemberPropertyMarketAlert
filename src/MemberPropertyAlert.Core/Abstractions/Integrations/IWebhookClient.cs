using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Results;

namespace MemberPropertyAlert.Core.Abstractions.Integrations;

public interface IWebhookClient
{
    Task<Result> SendAsync(string targetUrl, IReadOnlyDictionary<string, object> payload, CancellationToken cancellationToken = default);
}
