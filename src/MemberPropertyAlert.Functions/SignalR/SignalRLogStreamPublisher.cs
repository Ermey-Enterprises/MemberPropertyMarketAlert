using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Models;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.SignalR;

public sealed class SignalRLogStreamPublisher : ILogStreamPublisher
{
    private readonly ConcurrentDictionary<string, Channel<LogEvent>> _subscriptions = new();
    private readonly ILogger<SignalRLogStreamPublisher> _logger;

    public SignalRLogStreamPublisher(ILogger<SignalRLogStreamPublisher> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        foreach (var channel in _subscriptions.Values)
        {
            if (!await channel.Writer.WaitToWriteAsync(cancellationToken))
            {
                continue;
            }

            await channel.Writer.WriteAsync(logEvent, cancellationToken);
        }
    }

    public async IAsyncEnumerable<LogEvent> SubscribeAsync(string connectionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        if (!_subscriptions.TryAdd(connectionId, channel))
        {
            throw new InvalidOperationException($"Connection {connectionId} already subscribed.");
        }

        _logger.LogInformation("Connection {ConnectionId} subscribed to log stream", connectionId);

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (channel.Reader.TryRead(out var logEvent))
                {
                    yield return logEvent;
                }
            }
        }
        finally
        {
            _subscriptions.TryRemove(connectionId, out _);
            channel.Writer.Complete();
            _logger.LogInformation("Connection {ConnectionId} unsubscribed from log stream", connectionId);
        }
    }
}
