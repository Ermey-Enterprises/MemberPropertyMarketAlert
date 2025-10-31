using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.Enums;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Results;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Repositories;

public sealed class CosmosInstitutionRepository : IInstitutionRepository
{
    private readonly ICosmosContainerFactory _containerFactory;
    private readonly ILogger<CosmosInstitutionRepository> _logger;

    public CosmosInstitutionRepository(ICosmosContainerFactory containerFactory, ILogger<CosmosInstitutionRepository> logger)
    {
        _containerFactory = containerFactory;
        _logger = logger;
    }

    public async Task<Result<Institution>> CreateAsync(Institution institution, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
            var document = InstitutionDocument.FromDomain(institution);
            await container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), cancellationToken: cancellationToken);
            return Result<Institution>.Success(institution);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return Result<Institution>.Failure("An institution with the same id already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create institution {InstitutionId}", institution.Id);
            return Result<Institution>.Failure(ex.Message);
        }
    }

    public async Task<Result<Institution?>> GetAsync(string institutionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
            var response = await container.ReadItemAsync<InstitutionDocument>(institutionId, new PartitionKey(institutionId), cancellationToken: cancellationToken);
            return Result<Institution?>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Result<Institution?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get institution {InstitutionId}", institutionId);
            return Result<Institution?>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<Institution>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
        var queryDefinition = new QueryDefinition("SELECT * FROM c")
            .WithParameter("@offset", (pageNumber - 1) * pageSize)
            .WithParameter("@limit", pageSize);

        var query = container.GetItemQueryIterator<InstitutionDocument>("SELECT * FROM c OFFSET @offset LIMIT @limit", requestOptions: new QueryRequestOptions
        {
            MaxItemCount = pageSize
        });

        var documents = new List<InstitutionDocument>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync(cancellationToken);
            documents.AddRange(response.Resource);
        }

        var countIterator = container.GetItemQueryIterator<int>("SELECT VALUE COUNT(1) FROM c");
        var total = 0;
        while (countIterator.HasMoreResults)
        {
            var countResponse = await countIterator.ReadNextAsync(cancellationToken);
            total += countResponse.Resource.FirstOrDefault();
        }

        var institutions = documents.Select(d => d.ToDomain()).ToList();
        return new PagedResult<Institution>(institutions, total, pageNumber, pageSize);
    }

    public async Task<Result> UpdateAsync(Institution institution, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
            var document = InstitutionDocument.FromDomain(institution);
            await container.UpsertItemAsync(document, new PartitionKey(document.PartitionKey), cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update institution {InstitutionId}", institution.Id);
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(string institutionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
            await container.DeleteItemAsync<InstitutionDocument>(institutionId, new PartitionKey(institutionId), cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete institution {InstitutionId}", institutionId);
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<Core.Models.InstitutionCounts>> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
            var query = container.GetItemQueryIterator<InstitutionDocument>("SELECT * FROM c");
            var institutions = new List<InstitutionDocument>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                institutions.AddRange(response.Resource);
            }

            var total = institutions.Count;
            var active = institutions.Count(i => i.Status == InstitutionStatus.Active);
            var suspended = institutions.Count(i => i.Status == InstitutionStatus.Suspended);
            var disabled = institutions.Count(i => i.Status == InstitutionStatus.Disabled);
            var addressCount = institutions.Sum(i => i.Addresses?.Count ?? 0);
            var activeAddresses = institutions.Sum(i => i.Addresses?.Count(a => a.IsActive) ?? 0);

            var counts = new Core.Models.InstitutionCounts(total, active, suspended, disabled, addressCount, activeAddresses);
            return Result<Core.Models.InstitutionCounts>.Success(counts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate institution counts");
            return Result<Core.Models.InstitutionCounts>.Failure(ex.Message);
        }
    }

    private sealed class InstitutionDocument
    {
        public string Id { get; set; } = default!;
        public string PartitionKey => Id;
        public string Name { get; set; } = default!;
        public string ApiKeyHash { get; set; } = default!;
        public string TimeZoneId { get; set; } = default!;
        public InstitutionStatus Status { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public List<MemberAddressDocument>? Addresses { get; set; }

        public static InstitutionDocument FromDomain(Institution institution)
        {
            return new InstitutionDocument
            {
                Id = institution.Id,
                Name = institution.Name,
                ApiKeyHash = institution.ApiKeyHash,
                TimeZoneId = institution.TimeZoneId,
                Status = institution.Status,
                PrimaryContactEmail = institution.PrimaryContactEmail,
                CreatedAtUtc = institution.CreatedAtUtc,
                UpdatedAtUtc = institution.UpdatedAtUtc,
                Addresses = institution.Addresses.Select(MemberAddressDocument.FromDomain).ToList()
            };
        }

        public Institution ToDomain()
        {
            var addresses = Addresses?.Select(a => a.ToDomain()).ToList();
            return Institution.Rehydrate(Id, Name, ApiKeyHash, TimeZoneId, Status, PrimaryContactEmail, CreatedAtUtc, UpdatedAtUtc, addresses);
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
            return new AddressDocument(address.Line1, address.Line2, address.City, address.StateOrProvince, address.PostalCode, address.CountryCode, address.Coordinate?.Latitude, address.Coordinate?.Longitude);
        }

        public Address ToValueObject()
        {
            GeoCoordinate? coordinate = Latitude.HasValue && Longitude.HasValue ? GeoCoordinate.Create(Latitude.Value, Longitude.Value) : null;
            return Address.Create(Line1, Line2, City, StateOrProvince, PostalCode, CountryCode, coordinate);
        }
    }
}
