using Azure.Core;
using Azure.Messaging.ServiceBus;
using MemberPropertyAlert.Core.Options;
using MemberPropertyAlert.Functions.Infrastructure.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.Infrastructure.Integrations;

public sealed class ServiceBusClientAccessor : IServiceBusClientAccessor, IAsyncDisposable
{
    private readonly ServiceBusClient? _client;

    public ServiceBusClientAccessor(
        IOptions<ServiceBusOptions> options,
        ISecretProvider secretProvider,
        TokenCredential credential,
        ILogger<ServiceBusClientAccessor> logger)
    {
        var serviceBusOptions = options.Value;
        var connectionString = serviceBusOptions.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(serviceBusOptions.ConnectionStringSecretName))
        {
            connectionString = secretProvider.GetSecretAsync(serviceBusOptions.ConnectionStringSecretName).GetAwaiter().GetResult();
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _client = new ServiceBusClient(connectionString);
            logger.LogInformation("Service Bus client created using connection string.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(serviceBusOptions.FullyQualifiedNamespace))
        {
            _client = new ServiceBusClient(serviceBusOptions.FullyQualifiedNamespace, credential);
            logger.LogInformation("Service Bus client created using managed identity for namespace {Namespace}.", serviceBusOptions.FullyQualifiedNamespace);
            return;
        }

        logger.LogWarning("Service Bus configuration not provided. Messaging features will be disabled.");
    }

    public ServiceBusClient? Client => _client;

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
