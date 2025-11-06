using System.Text.Json.Serialization;

namespace MemberPropertyAlert.Functions.Models;

public sealed record MemberAddressImportMessage(
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("institutionId")] string InstitutionId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("blobUri")] string? BlobUri,
    [property: JsonPropertyName("csvBase64")] string? CsvBase64,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, string>? Metadata);
