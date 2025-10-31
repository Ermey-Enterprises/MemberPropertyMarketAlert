using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace MemberPropertyAlert.Functions.Infrastructure.Integrations;

public sealed class CosmosContainerFactory : ICosmosContainerFactory
{
    private readonly CosmosClient _client;
    private readonly CosmosOptions _options;
    private readonly ConcurrentDictionary<string, Task<Container>> _cache = new();

    public CosmosContainerFactory(CosmosClient client, IOptions<CosmosOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public CosmosOptions Options => _options;

    public Task<Container> GetInstitutionsContainerAsync(CancellationToken cancellationToken = default)
        => GetContainerAsync(_options.InstitutionsContainerName, "/id", cancellationToken);

    public Task<Container> GetAddressesContainerAsync(CancellationToken cancellationToken = default)
        => GetContainerAsync(_options.AddressesContainerName, "/institutionId", cancellationToken);

    public Task<Container> GetScansContainerAsync(CancellationToken cancellationToken = default)
        => GetContainerAsync(_options.ScansContainerName, "/stateOrProvince", cancellationToken);

    public Task<Container> GetAlertsContainerAsync(CancellationToken cancellationToken = default)
        => GetContainerAsync(_options.AlertsContainerName, "/severity", cancellationToken);

    private Task<Container> GetContainerAsync(string containerName, string partitionKeyPath, CancellationToken cancellationToken)
    {
        return _cache.GetOrAdd(containerName, _ => CreateContainerAsync(containerName, partitionKeyPath, cancellationToken));
    }

    private async Task<Container> CreateContainerAsync(string containerName, string partitionKeyPath, CancellationToken cancellationToken)
    {
        var database = await _client.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: cancellationToken);
        var response = await database.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(containerName, partitionKeyPath), cancellationToken: cancellationToken);
        return response.Container;
    }
}
