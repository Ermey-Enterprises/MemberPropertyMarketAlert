using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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

public sealed class CosmosListingMatchRepository : IListingMatchRepository
{
    private readonly ICosmosContainerFactory _containerFactory;
    private readonly ILogger<CosmosListingMatchRepository> _logger;

    public CosmosListingMatchRepository(ICosmosContainerFactory containerFactory, ILogger<CosmosListingMatchRepository> logger)
    {
        _containerFactory = containerFactory;
        _logger = logger;
    }

    public async Task<Result<ListingMatch>> CreateAsync(ListingMatch match, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetAlertsContainerAsync(cancellationToken);
            var institutionIds = await ResolveInstitutionIdsAsync(match.MatchedAddressIds, cancellationToken);
            var document = ListingMatchDocument.FromDomain(match, institutionIds);
            await container.CreateItemAsync(document, new PartitionKey(document.SeverityPartitionKey), cancellationToken: cancellationToken);
            return Result<ListingMatch>.Success(match);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return Result<ListingMatch>.Failure("A listing match with the same id already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create listing match {ListingMatchId}", match.Id);
            return Result<ListingMatch>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<ListingMatch>> ListRecentAsync(string? institutionId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        var container = await _containerFactory.GetAlertsContainerAsync(cancellationToken);
        var offset = (pageNumber - 1) * pageSize;
        QueryDefinition queryDefinition;

        if (!string.IsNullOrWhiteSpace(institutionId))
        {
            queryDefinition = new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(c.matchedInstitutionIds, @institutionId) ORDER BY c.detectedAtUtc DESC OFFSET @offset LIMIT @limit")
                .WithParameter("@institutionId", institutionId)
                .WithParameter("@offset", offset)
                .WithParameter("@limit", pageSize);
        }
        else
        {
            queryDefinition = new QueryDefinition("SELECT * FROM c ORDER BY c.detectedAtUtc DESC OFFSET @offset LIMIT @limit")
                .WithParameter("@offset", offset)
                .WithParameter("@limit", pageSize);
        }

        var iterator = container.GetItemQueryIterator<ListingMatchDocument>(queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = pageSize });
        var documents = new List<ListingMatchDocument>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(response.Resource);
        }

        QueryDefinition countDefinition;
        if (!string.IsNullOrWhiteSpace(institutionId))
        {
            countDefinition = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE ARRAY_CONTAINS(c.matchedInstitutionIds, @institutionId)")
                .WithParameter("@institutionId", institutionId);
        }
        else
        {
            countDefinition = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        }

        var countIterator = container.GetItemQueryIterator<int>(countDefinition);
        long total = 0;
        while (countIterator.HasMoreResults)
        {
            var response = await countIterator.ReadNextAsync(cancellationToken);
            total += response.Resource.FirstOrDefault();
        }

        var items = documents.Select(d => d.ToDomain()).ToList();
        return new PagedResult<ListingMatch>(items, total, pageNumber, pageSize);
    }

    public async Task<Result> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetAlertsContainerAsync(cancellationToken);
            var queryDefinition = new QueryDefinition("SELECT c.id, c.severity FROM c WHERE c.detectedAtUtc < @cutoff")
                .WithParameter("@cutoff", cutoffUtc);

            var iterator = container.GetItemQueryIterator<ListingMatchKeyProjection>(queryDefinition);
            var deletedCount = 0;

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                foreach (var projection in response.Resource)
                {
                    await container.DeleteItemAsync<ListingMatchDocument>(projection.Id, new PartitionKey(projection.Severity), cancellationToken: cancellationToken);
                    deletedCount++;
                }
            }

            _logger.LogInformation("Purged {Count} listing matches older than {Cutoff}", deletedCount, cutoffUtc);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge listing matches older than {Cutoff}", cutoffUtc);
            return Result.Failure(ex.Message);
        }
    }

    private async Task<IReadOnlyCollection<string>> ResolveInstitutionIdsAsync(IReadOnlyCollection<string> addressIds, CancellationToken cancellationToken)
    {
        if (addressIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        var container = await _containerFactory.GetAddressesContainerAsync(cancellationToken);
        var ids = addressIds.ToArray();

        var filterBuilder = new StringBuilder();
        for (var index = 0; index < ids.Length; index++)
        {
            if (index > 0)
            {
                filterBuilder.Append(" OR ");
            }

            filterBuilder.Append($"c.id = @id{index}");
        }

        var definition = new QueryDefinition($"SELECT c.id, c.institutionId FROM c WHERE {filterBuilder}");
        for (var index = 0; index < ids.Length; index++)
        {
            definition.WithParameter($"@id{index}", ids[index]);
        }
        var iterator = container.GetItemQueryIterator<AddressProjection>(definition);
        var institutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var projection in response.Resource)
            {
                if (!string.IsNullOrWhiteSpace(projection.InstitutionId))
                {
                    institutions.Add(projection.InstitutionId);
                }
            }
        }

        if (institutions.Count == 0)
        {
            _logger.LogWarning("Unable to resolve institution ids for addresses {Addresses}", string.Join(",", ids));
        }

        return institutions.ToArray();
    }

    private sealed class AddressProjection
    {
        public string Id { get; set; } = default!;
        public string InstitutionId { get; set; } = default!;
    }

    private sealed class ListingMatchKeyProjection
    {
        public string Id { get; set; } = default!;
        public string Severity { get; set; } = default!;
    }

    private sealed class ListingMatchDocument
    {
        public string Id { get; set; } = default!;
        public string ListingId { get; set; } = default!;
        public string ListingUrl { get; set; } = default!;
        public decimal MonthlyRent { get; set; }
        public string Severity { get; set; } = default!;
    public string SeverityPartitionKey => Severity;
        public string[] MatchedAddressIds { get; set; } = Array.Empty<string>();
        public string[] MatchedInstitutionIds { get; set; } = Array.Empty<string>();
        public DateTimeOffset DetectedAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public string? RentCastRegion { get; set; }
        public Dictionary<string, object?>? Metadata { get; set; }
        public AddressDocument ListingAddress { get; set; } = default!;

        public static ListingMatchDocument FromDomain(ListingMatch match, IReadOnlyCollection<string> institutionIds)
        {
            return new ListingMatchDocument
            {
                Id = match.Id,
                ListingId = match.ListingId,
                ListingUrl = match.ListingUrl.ToString(),
                MonthlyRent = match.MonthlyRent,
                Severity = match.Severity.ToString(),
                MatchedAddressIds = match.MatchedAddressIds.ToArray(),
                MatchedInstitutionIds = institutionIds.ToArray(),
                DetectedAtUtc = match.DetectedAtUtc,
                CreatedAtUtc = match.CreatedAtUtc,
                UpdatedAtUtc = match.UpdatedAtUtc,
                RentCastRegion = match.RentCastRegion,
                Metadata = match.Metadata.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                ListingAddress = AddressDocument.FromValueObject(match.ListingAddress)
            };
        }

        public ListingMatch ToDomain()
        {
            var address = ListingAddress.ToValueObject();
            var metadata = Metadata?.Where(kvp => kvp.Value is not null).ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value!);
            return ListingMatch.Rehydrate(
                Id,
                ListingId,
                address,
                MonthlyRent,
                new Uri(ListingUrl),
                Enum.TryParse<AlertSeverity>(Severity, true, out var severity) ? severity : AlertSeverity.Informational,
                MatchedAddressIds,
                DetectedAtUtc,
                RentCastRegion,
                metadata,
                CreatedAtUtc,
                UpdatedAtUtc);
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