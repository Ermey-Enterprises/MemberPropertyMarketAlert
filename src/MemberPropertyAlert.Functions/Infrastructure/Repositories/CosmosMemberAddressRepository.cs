using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Results;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Repositories;

public sealed class CosmosMemberAddressRepository : IMemberAddressRepository
{
    private readonly ICosmosContainerFactory _containerFactory;
    private readonly ILogger<CosmosMemberAddressRepository> _logger;

    public CosmosMemberAddressRepository(ICosmosContainerFactory containerFactory, ILogger<CosmosMemberAddressRepository> logger)
    {
        _containerFactory = containerFactory;
        _logger = logger;
    }

    public async Task<Result<MemberAddress>> CreateAsync(MemberAddress address, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
            var document = MemberAddressDocument.FromDomain(address);
            await container.CreateItemAsync(document, new PartitionKey(document.InstitutionId), cancellationToken: cancellationToken);
            return Result<MemberAddress>.Success(address);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return Result<MemberAddress>.Failure("Address already exists for this institution.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create address {AddressId} for institution {InstitutionId}", address.Id, address.InstitutionId);
            return Result<MemberAddress>.Failure(ex.Message);
        }
    }

    public async Task<Result<MemberAddress?>> GetAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
            var response = await container.ReadItemAsync<MemberAddressDocument>(addressId, new PartitionKey(institutionId), cancellationToken: cancellationToken);
            return Result<MemberAddress?>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Result<MemberAddress?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get address {AddressId} for institution {InstitutionId}", addressId, institutionId);
            return Result<MemberAddress?>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<MemberAddress>> ListByInstitutionAsync(string institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
        var offset = (pageNumber - 1) * pageSize;

        var queryDefinition = new QueryDefinition("SELECT * FROM c OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var iterator = container.GetItemQueryIterator<MemberAddressDocument>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(institutionId),
                MaxItemCount = pageSize
            });

        var documents = new List<MemberAddressDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(response.Resource);
        }

        var countIterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"),
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(institutionId)
            });

        long total = 0;
        while (countIterator.HasMoreResults)
        {
            var response = await countIterator.ReadNextAsync(cancellationToken);
            total += response.Resource.FirstOrDefault();
        }

        var items = documents.Select(d => d.ToDomain()).ToList();
        return new PagedResult<MemberAddress>(items, total, pageNumber, pageSize);
    }

    public async Task<IReadOnlyCollection<MemberAddress>> ListByStateAsync(string stateOrProvince, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stateOrProvince))
        {
            return Array.Empty<MemberAddress>();
        }

        try
        {
            var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE STRINGEQUALS(c.address.stateOrProvince, @state, true)")
                .WithParameter("@state", stateOrProvince);

            var iterator = container.GetItemQueryIterator<MemberAddressDocument>(queryDefinition);
            var documents = new List<MemberAddressDocument>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                documents.AddRange(response.Resource);
            }

            return documents.Select(d => d.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list addresses for state {State}", stateOrProvince);
            return Array.Empty<MemberAddress>();
        }
    }

    public async Task<Result> UpsertBulkAsync(string institutionId, IReadOnlyCollection<MemberAddress> addresses, CancellationToken cancellationToken = default)
    {
        if (addresses.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
            const int batchSize = 100;
            var chunks = addresses.Select(MemberAddressDocument.FromDomain).Chunk(batchSize);

            foreach (var chunk in chunks)
            {
                var batch = container.CreateTransactionalBatch(new PartitionKey(institutionId));
                foreach (var document in chunk)
                {
                    batch.UpsertItem(document);
                }

                var response = await batch.ExecuteAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Bulk upsert for institution {InstitutionId} returned status {StatusCode}", institutionId, response.StatusCode);
                    return Result.Failure($"Bulk upsert failed with status code {response.StatusCode}");
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk upsert addresses for institution {InstitutionId}", institutionId);
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(string institutionId, string addressId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
            await container.DeleteItemAsync<MemberAddressDocument>(addressId, new PartitionKey(institutionId), cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete address {AddressId} for institution {InstitutionId}", addressId, institutionId);
            return Result.Failure(ex.Message);
        }
    }

    private sealed class MemberAddressDocument
    {
        public string Id { get; set; } = default!;
        public string InstitutionId { get; set; } = default!;
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public DateTimeOffset? LastMatchedAtUtc { get; set; }
        public string? LastMatchedListingId { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public AddressDocument Address { get; set; } = default!;

        public static MemberAddressDocument FromDomain(MemberAddress address)
        {
            return new MemberAddressDocument
            {
                Id = address.Id,
                InstitutionId = address.InstitutionId,
                IsActive = address.IsActive,
                CreatedAtUtc = address.CreatedAtUtc,
                UpdatedAtUtc = address.UpdatedAtUtc,
                LastMatchedAtUtc = address.LastMatchedAtUtc,
                LastMatchedListingId = address.LastMatchedListingId,
                Tags = address.Tags.ToArray(),
                Address = AddressDocument.FromValueObject(address.Address)
            };
        }

        public MemberAddress ToDomain()
        {
            var valueObject = Address.ToValueObject();
            return MemberAddress.Rehydrate(Id, InstitutionId, valueObject, Tags, IsActive, CreatedAtUtc, UpdatedAtUtc, LastMatchedAtUtc, LastMatchedListingId);
        }
    }

    private sealed record AddressDocument(
        string Line1,
        string? Line2,
        string City,
        string StateOrProvince,
        string PostalCode,
        string CountryCode,
        double? Latitude,
        double? Longitude)
    {
        public static AddressDocument FromValueObject(Address address)
        {
            return new AddressDocument(
                address.Line1,
                address.Line2,
                address.City,
                address.StateOrProvince,
                address.PostalCode,
                address.CountryCode,
                address.Coordinate?.Latitude,
                address.Coordinate?.Longitude);
        }

        public Address ToValueObject()
        {
            GeoCoordinate? coordinate = Latitude.HasValue && Longitude.HasValue
                ? GeoCoordinate.Create(Latitude.Value, Longitude.Value)
                : null;

            return Address.Create(Line1, Line2, City, StateOrProvince, PostalCode, CountryCode, coordinate);
        }
    }
}