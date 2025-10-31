using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Results;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Integrations;

public sealed class WebhookClient : IWebhookClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookClient> _logger;

    public WebhookClient(HttpClient httpClient, ILogger<WebhookClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result> SendAsync(string targetUrl, IReadOnlyDictionary<string, object> payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_httpClient, targetUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Webhook call to {Url} failed with {Status}: {Body}", targetUrl, response.StatusCode, body);
                return Result.Failure($"Webhook call failed with status {(int)response.StatusCode}.");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook to {Url}", targetUrl);
            return Result.Failure(ex.Message);
        }
    }
}
