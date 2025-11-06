using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Options;
using MemberPropertyAlert.Core.Results;
using MemberPropertyAlert.Functions.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MemberPropertyAlert.Functions.Tests;

public class ServiceBusAlertPublisherTests
{
    [Fact]
    public void CreateServiceBusMessage_EmbedsTenantMetadata()
    {
        var match = CreateMatch();
        match.SetTenancyDetails(new[] { "tenant-1" }, new[] { "inst-1", "inst-2" });

        var message = ServiceBusAlertPublisher.CreateServiceBusMessage(match);

        Assert.Equal($"listing-match:{match.MatchedTenantIds.Single()}", message.Subject);
        Assert.Equal("tenant-1", message.ApplicationProperties["tenantId"]);
        Assert.Equal("tenant-1", message.ApplicationProperties["tenantIds"]);
        Assert.Equal("inst-1,inst-2", message.ApplicationProperties["institutionIds"]);
    }

    [Fact]
    public async Task PublishAsync_AddsTenantMetadataToWebhookPayload()
    {
        var match = CreateMatch();
        match.SetTenancyDetails(new[] { "tenant-1", "tenant-2" }, new[] { "inst-1" });
        var webhook = new CapturingWebhookClient();
        var options = Options.Create(new NotificationOptions
        {
            EnableWebhook = true,
            DefaultWebhookUrl = "https://example.com/hook"
        });
        var publisher = new ServiceBusAlertPublisher(null, webhook, options, NullLogger<ServiceBusAlertPublisher>.Instance);

        var result = await publisher.PublishAsync(new[] { match }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = webhook.Payloads.Single();
        Assert.Equal(match.MatchedTenantIds, Assert.IsAssignableFrom<IReadOnlyCollection<string>>(payload["tenantIds"]));
        Assert.Equal(match.MatchedInstitutionIds, Assert.IsAssignableFrom<IReadOnlyCollection<string>>(payload["institutionIds"]));
    }

    private static ListingMatch CreateMatch()
    {
        return ListingMatch.Create(
            Guid.NewGuid().ToString("N"),
            "listing-123",
            Address.Create("789 Pine", null, "Gotham", "NY", "10001", "US"),
            1800m,
            new Uri("https://example.com/listing"),
            AlertSeverity.Warning,
            new[] { "addr-1", "addr-2" },
            DateTimeOffset.UtcNow,
            "NY");
    }

    private sealed class CapturingWebhookClient : IWebhookClient
    {
        public List<IReadOnlyDictionary<string, object>> Payloads { get; } = new();

        public Task<Result> SendAsync(string targetUrl, IReadOnlyDictionary<string, object> payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(Result.Success());
        }
    }
}
