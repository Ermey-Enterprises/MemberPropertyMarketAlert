using System;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Abstractions.Messaging;

public interface ILogStreamPublisher
{
    Task PublishAsync(LogEvent logEvent, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LogEvent> SubscribeAsync(string connectionId, CancellationToken cancellationToken = default);
}
