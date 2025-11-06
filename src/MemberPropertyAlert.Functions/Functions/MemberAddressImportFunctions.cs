using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Domain.Entities;
using MemberPropertyAlert.Core.Domain.ValueObjects;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Functions.Infrastructure.Storage;
using MemberPropertyAlert.Functions.Models;
using MemberPropertyAlert.Functions.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Functions;

public sealed class MemberAddressImportFunctions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemberAddressRepository _memberAddressRepository;
    private readonly IMemberAddressImportPayloadResolver _payloadResolver;
    private readonly ITenantRequestContextAccessor _tenantContextAccessor;
    private readonly IImportStatusPublisher _statusPublisher;
    private readonly ILogger<MemberAddressImportFunctions> _logger;

    public MemberAddressImportFunctions(
        IMemberAddressRepository memberAddressRepository,
        IMemberAddressImportPayloadResolver payloadResolver,
        ITenantRequestContextAccessor tenantContextAccessor,
        IImportStatusPublisher statusPublisher,
        ILogger<MemberAddressImportFunctions> logger)
    {
        _memberAddressRepository = memberAddressRepository;
        _payloadResolver = payloadResolver;
        _tenantContextAccessor = tenantContextAccessor;
        _statusPublisher = statusPublisher;
        _logger = logger;
    }

    [Function("ProcessMemberAddressImport")]
    public async Task ProcessMemberAddressImportAsync(
        [QueueTrigger("%MemberAddressImportQueue%", Connection = "AzureWebJobsStorage")] string queueMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(queueMessage))
        {
            _logger.LogWarning("Received empty queue message for member address import.");
            return;
        }

        MemberAddressImportMessage importMessage;
        try
        {
            importMessage = JsonSerializer.Deserialize<MemberAddressImportMessage>(queueMessage, SerializerOptions)
                ?? throw new InvalidDataException("Import message payload was empty.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to deserialize import message: {Message}", queueMessage);
            throw;
        }

        var correlationId = !string.IsNullOrWhiteSpace(importMessage.CorrelationId)
            ? importMessage.CorrelationId!
            : Guid.NewGuid().ToString("N");

        await _statusPublisher.PublishAsync(
            MemberAddressImportStatusEvent.Processing(importMessage.TenantId, importMessage.InstitutionId, importMessage.FileName, correlationId),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyCollection<MemberAddress>? parsedAddresses = null;

        try
        {
            await using var payloadStream = await _payloadResolver.OpenAsync(importMessage, cancellationToken).ConfigureAwait(false);
            parsedAddresses = await ParseAddressesAsync(importMessage, payloadStream, cancellationToken).ConfigureAwait(false);

            var tenantContext = BuildTenantContext(importMessage, correlationId);
            _tenantContextAccessor.SetCurrent(tenantContext);

            try
            {
                var result = await _memberAddressRepository.UpsertBulkAsync(importMessage.InstitutionId, parsedAddresses, cancellationToken)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    throw new MemberAddressImportException(result.Error ?? "Bulk import failed.", 0, parsedAddresses.Count);
                }
            }
            finally
            {
                _tenantContextAccessor.Clear();
            }

            await _statusPublisher.PublishAsync(
                MemberAddressImportStatusEvent.Completed(
                    importMessage.TenantId,
                    importMessage.InstitutionId,
                    importMessage.FileName,
                    parsedAddresses.Count,
                    correlationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var (processed, failed) = ResolveFailureCounts(ex, parsedAddresses);

            await _statusPublisher.PublishAsync(
                MemberAddressImportStatusEvent.Failed(
                    importMessage.TenantId,
                    importMessage.InstitutionId,
                    importMessage.FileName,
                    ex.Message,
                    processed,
                    failed,
                    correlationId),
                cancellationToken).ConfigureAwait(false);

            _logger.LogError(
                ex,
                "Member address import failed for tenant {TenantId} institution {InstitutionId} (correlation {CorrelationId}).",
                importMessage.TenantId,
                importMessage.InstitutionId,
                correlationId);

            throw;
        }
    }

    private async Task<IReadOnlyCollection<MemberAddress>> ParseAddressesAsync(
        MemberAddressImportMessage importMessage,
        Stream payloadStream,
        CancellationToken cancellationToken)
    {
        if (!payloadStream.CanRead)
        {
            throw new InvalidDataException("Payload stream is not readable.");
        }

        var addresses = new List<MemberAddress>();

        try
        {
            using var reader = new StreamReader(payloadStream, leaveOpen: true);
            var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var csv = new CsvReader(reader, configuration);
            csv.Context.RegisterClassMap<MemberAddressImportCsvMap>();

            var rowIndex = 0;
            await foreach (var record in csv.GetRecordsAsync<MemberAddressImportCsvRow>(cancellationToken).ConfigureAwait(false))
            {
                rowIndex++;
                ValidateRow(record, importMessage, rowIndex);

                Address address;
                try
                {
                    address = Address.Create(
                        record.AddressLine1!,
                        record.AddressLine2,
                        record.City!,
                        record.StateOrProvince!,
                        record.PostalCode!,
                        record.CountryCode!);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException($"Row {rowIndex}: {ex.Message}", ex);
                }

                var tags = ParseTags(record.Tags);
                var aggregateResult = MemberAddress.Create(Guid.NewGuid().ToString("N"), importMessage.TenantId, importMessage.InstitutionId, address, tags);

                if (aggregateResult.IsFailure)
                {
                    throw new InvalidDataException($"Row {rowIndex}: {aggregateResult.Error}");
                }

                addresses.Add(aggregateResult.Value!);
            }
        }
        catch (HeaderValidationException ex)
        {
            throw new InvalidDataException("CSV headers are invalid or missing required columns.", ex);
        }
        catch (BadDataException ex)
        {
            throw new InvalidDataException("Encountered malformed CSV data during import.", ex);
        }

        if (addresses.Count == 0)
        {
            throw new InvalidDataException("Import payload did not contain any member addresses.");
        }

        return addresses;
    }

    private static void ValidateRow(MemberAddressImportCsvRow record, MemberAddressImportMessage message, int rowIndex)
    {
        if (string.IsNullOrWhiteSpace(record.TenantId))
        {
            throw new InvalidDataException($"Row {rowIndex}: TenantId is required.");
        }

        if (!string.Equals(record.TenantId, message.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Row {rowIndex}: TenantId '{record.TenantId}' does not match import tenant '{message.TenantId}'.");
        }

        if (string.IsNullOrWhiteSpace(record.InstitutionId))
        {
            throw new InvalidDataException($"Row {rowIndex}: InstitutionId is required.");
        }

        if (!string.Equals(record.InstitutionId, message.InstitutionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Row {rowIndex}: InstitutionId '{record.InstitutionId}' does not match import institution '{message.InstitutionId}'.");
        }

        if (string.IsNullOrWhiteSpace(record.AddressLine1) ||
            string.IsNullOrWhiteSpace(record.City) ||
            string.IsNullOrWhiteSpace(record.StateOrProvince) ||
            string.IsNullOrWhiteSpace(record.PostalCode) ||
            string.IsNullOrWhiteSpace(record.CountryCode))
        {
            throw new InvalidDataException($"Row {rowIndex}: Address fields are incomplete.");
        }
    }

    private static IEnumerable<string> ParseTags(string? rawTags)
    {
        if (string.IsNullOrWhiteSpace(rawTags))
        {
            return Array.Empty<string>();
        }

        return rawTags
            .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private TenantRequestContext BuildTenantContext(MemberAddressImportMessage importMessage, string correlationId)
    {
        var claims = new List<Claim>
        {
            new("sub", "member-address-import"),
            new("tenantId", importMessage.TenantId),
            new("role", "MemberAddressImport"),
        };

        var metadata = importMessage.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (metadata.TryGetValue("objectId", out var objectId))
        {
            claims.Add(new Claim("oid", objectId));
        }

        if (metadata.TryGetValue("preferredUsername", out var username))
        {
            claims.Add(new Claim("preferred_username", username));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "MemberAddressImport"));

        var roles = metadata.TryGetValue("roles", out var rawRoles)
            ? rawRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        var contextUsername = metadata.TryGetValue("preferredUsername", out var preferredUsername)
            ? preferredUsername
            : metadata.TryGetValue("username", out var explicitUsername) ? explicitUsername : null;

        var contextObjectId = metadata.TryGetValue("objectId", out var metadataObjectId) ? metadataObjectId : null;

        return new TenantRequestContext(
            principal,
            importMessage.TenantId,
            importMessage.InstitutionId,
            true,
            contextObjectId,
            contextUsername,
            correlationId,
            roles);
    }

    private static (int ProcessedCount, int FailedCount) ResolveFailureCounts(Exception exception, IReadOnlyCollection<MemberAddress>? parsedAddresses)
    {
        if (exception is MemberAddressImportException importException)
        {
            return (importException.ProcessedCount, importException.FailedCount);
        }

        var failed = parsedAddresses?.Count ?? 0;
        return (0, failed);
    }

    private sealed record MemberAddressImportCsvRow
    {
        public string? TenantId { get; init; }
        public string? InstitutionId { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
        public string? City { get; init; }
        public string? StateOrProvince { get; init; }
        public string? PostalCode { get; init; }
        public string? CountryCode { get; init; }
        public string? Tags { get; init; }
    }

    private sealed class MemberAddressImportCsvMap : ClassMap<MemberAddressImportCsvRow>
    {
        public MemberAddressImportCsvMap()
        {
            Map(m => m.TenantId).Name("tenantId");
            Map(m => m.InstitutionId).Name("institutionId");
            Map(m => m.AddressLine1).Name("addressLine1");
            Map(m => m.AddressLine2).Name("addressLine2").Optional();
            Map(m => m.City).Name("city");
            Map(m => m.StateOrProvince).Name("stateOrProvince");
            Map(m => m.PostalCode).Name("postalCode");
            Map(m => m.CountryCode).Name("countryCode");
            Map(m => m.Tags).Name("tags").Optional();
        }
    }

    private sealed class MemberAddressImportException : Exception
    {
        public MemberAddressImportException(string message, int processedCount, int failedCount)
            : base(message)
        {
            ProcessedCount = processedCount;
            FailedCount = failedCount;
        }

        public int ProcessedCount { get; }
        public int FailedCount { get; }
    }
}
