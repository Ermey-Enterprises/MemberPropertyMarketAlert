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
using MemberPropertyAlert.Functions.Security;

namespace MemberPropertyAlert.Functions.Infrastructure.Repositories;

public sealed class CosmosInstitutionRepository : IInstitutionRepository
{
    private readonly ICosmosContainerFactory _containerFactory;
    private readonly ILogger<CosmosInstitutionRepository> _logger;
    private readonly ITenantRequestContextAccessor _tenantAccessor;

    public CosmosInstitutionRepository(
        ICosmosContainerFactory containerFactory,
        ITenantRequestContextAccessor tenantAccessor,
        ILogger<CosmosInstitutionRepository> logger)
    {
        _containerFactory = containerFactory;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<Result<Institution>> CreateAsync(Institution institution, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantContext = _tenantAccessor.Current;
            if (tenantContext is null)
            {
                return Result<Institution>.Failure("Tenant context is not available.");
            }

            if (!tenantContext.IsPlatformAdmin && !string.Equals(tenantContext.TenantId, institution.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Result<Institution>.Failure("Cannot create institutions for a different tenant.");
            }

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
            var document = response.Resource;
            if (!IsAuthorizedForTenant(document.TenantId))
            {
                _logger.LogWarning("Unauthorized access attempt for institution {InstitutionId} in tenant {TenantId}", institutionId, document.TenantId);
                return Result<Institution?>.Success(null);
            }

            return Result<Institution?>.Success(document.ToDomain());
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
        var tenantContext = _tenantAccessor.Current;
        if (tenantContext is null)
        {
            return new PagedResult<Institution>(Array.Empty<Institution>(), 0, pageNumber, pageSize);
        }

        var offset = (pageNumber - 1) * pageSize;
        var baseQuery = tenantContext.IsPlatformAdmin
            ? "SELECT * FROM c OFFSET @offset LIMIT @limit"
            : "SELECT * FROM c WHERE c.tenantId = @tenantId OFFSET @offset LIMIT @limit";

        var queryDefinition = new QueryDefinition(baseQuery)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        if (!tenantContext.IsPlatformAdmin)
        {
            queryDefinition = queryDefinition.WithParameter("@tenantId", tenantContext.TenantId);
        }

        var iterator = container.GetItemQueryIterator<InstitutionDocument>(queryDefinition, requestOptions: new QueryRequestOptions
        {
            MaxItemCount = pageSize
        });

        var documents = new List<InstitutionDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(response.Resource.Where(d => IsAuthorizedForTenant(d.TenantId)));
        }

        QueryDefinition countQuery = tenantContext.IsPlatformAdmin
            ? new QueryDefinition("SELECT VALUE COUNT(1) FROM c")
            : new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.tenantId = @tenantId")
                .WithParameter("@tenantId", tenantContext.TenantId);

        var countIterator = container.GetItemQueryIterator<int>(countQuery);
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
            var tenantContext = _tenantAccessor.Current;
            if (tenantContext is null)
            {
                return Result.Failure("Tenant context is not available.");
            }

            if (!tenantContext.IsPlatformAdmin && !string.Equals(tenantContext.TenantId, institution.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure("Not authorized to update this institution.");
            }

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
            var tenantContext = _tenantAccessor.Current;
            if (tenantContext is null)
            {
                return Result.Failure("Tenant context is not available.");
            }

            if (!tenantContext.IsPlatformAdmin)
            {
                try
                {
                    var existing = await container.ReadItemAsync<InstitutionDocument>(institutionId, new PartitionKey(institutionId), cancellationToken: cancellationToken);
                    if (!string.Equals(existing.Resource.TenantId, tenantContext.TenantId, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result.Failure("Not authorized to delete this institution.");
                    }
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return Result.Success();
                }
            }

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
            if (_tenantAccessor.Current is null)
            {
                return Result<Core.Models.InstitutionCounts>.Failure("Tenant context is not available.");
            }

            var container = await _containerFactory.GetInstitutionsContainerAsync(cancellationToken);
            var query = container.GetItemQueryIterator<InstitutionDocument>("SELECT * FROM c");
            var institutions = new List<InstitutionDocument>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                institutions.AddRange(response.Resource.Where(doc => IsAuthorizedForTenant(doc.TenantId)));
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

    private bool IsAuthorizedForTenant(string tenantId)
    {
        var context = _tenantAccessor.Current;
        if (context is null)
        {
            return false;
        }

        if (context.IsPlatformAdmin)
        {
            return true;
        }

        return string.Equals(context.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InstitutionDocument
    {
    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string PartitionKey => Id;
    public string Name { get; set; } = default!;
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
                TenantId = institution.TenantId,
                Name = institution.Name,
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
            return Institution.Rehydrate(Id, TenantId, Name, TimeZoneId, Status, PrimaryContactEmail, CreatedAtUtc, UpdatedAtUtc, addresses);
        }
    }

    private sealed class MemberAddressDocument
    {
        public string Id { get; set; } = default!;
        public string TenantId { get; set; } = default!;
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
                TenantId = address.TenantId,
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
            return MemberAddress.Rehydrate(Id, TenantId, InstitutionId, valueObject, Tags, IsActive, CreatedAtUtc, UpdatedAtUtc, LastMatchedAtUtc, LastMatchedListingId);
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
