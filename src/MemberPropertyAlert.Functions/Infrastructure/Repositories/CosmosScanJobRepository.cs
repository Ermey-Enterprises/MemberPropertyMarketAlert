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
using MemberPropertyAlert.Core.Results;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Repositories;

public sealed class CosmosScanJobRepository : IScanJobRepository
{
    private readonly ICosmosContainerFactory _containerFactory;
    private readonly ILogger<CosmosScanJobRepository> _logger;

    public CosmosScanJobRepository(ICosmosContainerFactory containerFactory, ILogger<CosmosScanJobRepository> logger)
    {
        _containerFactory = containerFactory;
        _logger = logger;
    }

    public async Task<Result<ScanJob>> CreateAsync(ScanJob scanJob, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
            var document = ScanJobDocument.FromDomain(scanJob);
            await container.CreateItemAsync(document, new PartitionKey(document.StateOrProvince), cancellationToken: cancellationToken);
            return Result<ScanJob>.Success(scanJob);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return Result<ScanJob>.Failure("A scan job with the same id already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scan job {ScanJobId}", scanJob.Id);
            return Result<ScanJob>.Failure(ex.Message);
        }
    }

    public async Task<Result<ScanJob?>> GetAsync(string scanJobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
            var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", scanJobId);

            var iterator = container.GetItemQueryIterator<ScanJobDocument>(queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var document = response.Resource.FirstOrDefault();
                if (document is not null)
                {
                    return Result<ScanJob?>.Success(document.ToDomain());
                }
            }

            return Result<ScanJob?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve scan job {ScanJobId}", scanJobId);
            return Result<ScanJob?>.Failure(ex.Message);
        }
    }

    public async Task<Result<ScanJob?>> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
            var queryDefinition = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAtUtc DESC OFFSET 0 LIMIT 1");
            var iterator = container.GetItemQueryIterator<ScanJobDocument>(queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var document = response.Resource.FirstOrDefault();
                if (document is not null)
                {
                    return Result<ScanJob?>.Success(document.ToDomain());
                }
            }

            return Result<ScanJob?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve latest scan job");
            return Result<ScanJob?>.Failure(ex.Message);
        }
    }

    public async Task<PagedResult<ScanJob>> ListRecentAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
        var offset = (pageNumber - 1) * pageSize;

        var queryDefinition = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAtUtc DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var iterator = container.GetItemQueryIterator<ScanJobDocument>(queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = pageSize });
        var documents = new List<ScanJobDocument>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            documents.AddRange(response.Resource);
        }

        var countIterator = container.GetItemQueryIterator<int>(new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));
        long total = 0;
        while (countIterator.HasMoreResults)
        {
            var response = await countIterator.ReadNextAsync(cancellationToken);
            total += response.Resource.FirstOrDefault();
        }

        var items = documents.Select(d => d.ToDomain()).ToList();
        return new PagedResult<ScanJob>(items, total, pageNumber, pageSize);
    }

    public async Task<Result> UpdateAsync(ScanJob scanJob, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = await _containerFactory.GetScansContainerAsync(cancellationToken);
            var document = ScanJobDocument.FromDomain(scanJob);
            await container.UpsertItemAsync(document, new PartitionKey(document.StateOrProvince), cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scan job {ScanJobId}", scanJob.Id);
            return Result.Failure(ex.Message);
        }
    }

    private sealed class ScanJobDocument
    {
        public string Id { get; set; } = default!;
        public string StateOrProvince { get; set; } = default!;
        public string[] InstitutionIds { get; set; } = Array.Empty<string>();
        public ScanStatus Status { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public string? FailureReason { get; set; }

        public static ScanJobDocument FromDomain(ScanJob scanJob)
        {
            return new ScanJobDocument
            {
                Id = scanJob.Id,
                StateOrProvince = scanJob.StateOrProvince,
                InstitutionIds = scanJob.InstitutionIds.ToArray(),
                Status = scanJob.Status,
                CreatedAtUtc = scanJob.CreatedAtUtc,
                UpdatedAtUtc = scanJob.UpdatedAtUtc,
                StartedAtUtc = scanJob.StartedAtUtc,
                CompletedAtUtc = scanJob.CompletedAtUtc,
                FailureReason = scanJob.FailureReason
            };
        }

        public ScanJob ToDomain()
        {
            return ScanJob.Rehydrate(
                Id,
                StateOrProvince,
                InstitutionIds,
                Status,
                CreatedAtUtc,
                UpdatedAtUtc,
                StartedAtUtc,
                CompletedAtUtc,
                FailureReason);
        }
    }
}