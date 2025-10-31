using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Options;
using MemberPropertyAlert.Core.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.SignalR;

public sealed class ServiceBusAlertPublisher : IAlertPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly IWebhookClient _webhookClient;
    private readonly NotificationOptions _options;
    private readonly ILogger<ServiceBusAlertPublisher> _logger;

    public ServiceBusAlertPublisher(ServiceBusClient? serviceBusClient, IWebhookClient webhookClient, IOptions<NotificationOptions> options, ILogger<ServiceBusAlertPublisher> logger)
    {
        _serviceBusClient = serviceBusClient;
        _webhookClient = webhookClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> PublishAsync(IReadOnlyCollection<ListingMatch> matches, CancellationToken cancellationToken = default)
    {
        if (matches.Count == 0)
        {
            return Result.Success();
        }

        var failures = new List<string>();

        if (_serviceBusClient is not null)
        {
            await using var sender = _serviceBusClient.CreateSender(_options.AlertQueueName);
            foreach (var match in matches)
            {
                try
                {
                    var payload = JsonSerializer.Serialize(match, SerializerOptions);
                    var message = new ServiceBusMessage(payload)
                    {
                        ContentType = "application/json",
                        Subject = "listing-match",
                        MessageId = match.Id
                    };

                    await sender.SendMessageAsync(message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send Service Bus message for match {MatchId}", match.Id);
                    failures.Add($"ServiceBus:{match.Id}");
                }
            }
        }

        if (_options.EnableWebhook && !string.IsNullOrWhiteSpace(_options.DefaultWebhookUrl))
        {
            foreach (var match in matches)
            {
                var payload = new Dictionary<string, object>
                {
                    ["matchId"] = match.Id,
                    ["listing"] = new
                    {
                        match.ListingId,
                        address = match.ListingAddress.ToString(),
                        rent = match.MonthlyRent,
                        url = match.ListingUrl.ToString()
                    },
                    ["severity"] = match.Severity.ToString(),
                    ["detectedAtUtc"] = match.DetectedAtUtc
                };

                var result = await _webhookClient.SendAsync(_options.DefaultWebhookUrl, payload, cancellationToken);
                if (result.IsFailure)
                {
                    failures.Add($"Webhook:{match.Id}");
                }
            }
        }

        if (failures.Count > 0)
        {
            return Result.Failure($"One or more alerts failed to publish: {string.Join(", ", failures)}");
        }

        return Result.Success();
    }
}
