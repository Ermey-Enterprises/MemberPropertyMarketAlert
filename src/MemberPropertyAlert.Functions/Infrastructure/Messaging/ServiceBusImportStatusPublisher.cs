using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Options;
using MemberPropertyAlert.Functions.Infrastructure.Integrations;
using MemberPropertyAlert.Core.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.Infrastructure.Messaging;

public sealed class ServiceBusImportStatusPublisher : IImportStatusPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceBusClientAccessor _clientAccessor;
    private readonly ILogStreamPublisher _logPublisher;
    private readonly MemberAddressImportOptions _options;
    private readonly ILogger<ServiceBusImportStatusPublisher> _logger;

    public ServiceBusImportStatusPublisher(
        IServiceBusClientAccessor clientAccessor,
        ILogStreamPublisher logPublisher,
        IOptions<MemberAddressImportOptions> options,
        ILogger<ServiceBusImportStatusPublisher> logger)
    {
        _clientAccessor = clientAccessor;
        _logPublisher = logPublisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(MemberAddressImportStatusEvent statusEvent, CancellationToken cancellationToken = default)
    {
        await PublishToSignalRAsync(statusEvent, cancellationToken).ConfigureAwait(false);
        await PublishToServiceBusAsync(statusEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishToSignalRAsync(MemberAddressImportStatusEvent statusEvent, CancellationToken cancellationToken)
    {
        var message = statusEvent.State switch
        {
            MemberAddressImportState.Processing => $"Processing member address import for institution {statusEvent.InstitutionId}.",
            MemberAddressImportState.Completed => $"Completed import for institution {statusEvent.InstitutionId} ({statusEvent.ProcessedCount} rows).",
            MemberAddressImportState.Failed => $"Failed import for institution {statusEvent.InstitutionId}: {statusEvent.Error}",
            _ => $"Import status changed for institution {statusEvent.InstitutionId}."
        };

        var severity = statusEvent.State == MemberAddressImportState.Failed
            ? AlertSeverity.Critical
            : AlertSeverity.Informational;

        var logEvent = new LogEvent(
            Guid.NewGuid().ToString("N"),
            message,
            severity,
            DateTimeOffset.UtcNow,
            nameof(ServiceBusImportStatusPublisher),
            statusEvent.InstitutionId,
            statusEvent.Error);

        await _logPublisher.PublishAsync(logEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishToServiceBusAsync(MemberAddressImportStatusEvent statusEvent, CancellationToken cancellationToken)
    {
        if (_clientAccessor.Client is null)
        {
            _logger.LogDebug("Service Bus client unavailable. Skipping status publish for correlation {CorrelationId}.", statusEvent.CorrelationId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.StatusQueueName))
        {
            _logger.LogDebug("Status queue name is not configured. Skipping Service Bus publish for correlation {CorrelationId}.", statusEvent.CorrelationId);
            return;
        }

        await using var sender = _clientAccessor.Client.CreateSender(_options.StatusQueueName);
        var payload = JsonSerializer.Serialize(statusEvent, SerializerOptions);
        var message = new ServiceBusMessage(payload)
        {
            ContentType = "application/json",
            Subject = "member-address-import-status",
            CorrelationId = statusEvent.CorrelationId
        };

        await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Published import status {State} for institution {InstitutionId} to Service Bus queue {Queue}.",
            statusEvent.State,
            statusEvent.InstitutionId,
            _options.StatusQueueName);
    }
}
