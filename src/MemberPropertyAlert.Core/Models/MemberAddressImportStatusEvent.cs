using MemberPropertyAlert.Core.Domain.Enums;

namespace MemberPropertyAlert.Core.Models;

public enum MemberAddressImportState
{
    Processing = 0,
    Completed = 1,
    Failed = 2
}

public sealed record MemberAddressImportStatusEvent(
    string TenantId,
    string InstitutionId,
    string FileName,
    MemberAddressImportState State,
    int ProcessedCount,
    int FailedCount,
    string? Error,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc,
    AlertSeverity Severity)
{
    public static MemberAddressImportStatusEvent Processing(
        string tenantId,
        string institutionId,
        string fileName,
        string correlationId)
        => new(
            tenantId,
            institutionId,
            fileName,
            MemberAddressImportState.Processing,
            0,
            0,
            null,
            correlationId,
            DateTimeOffset.UtcNow,
            AlertSeverity.Informational);

    public static MemberAddressImportStatusEvent Completed(
        string tenantId,
        string institutionId,
        string fileName,
        int processedCount,
        string correlationId)
        => new(
            tenantId,
            institutionId,
            fileName,
            MemberAddressImportState.Completed,
            processedCount,
            0,
            null,
            correlationId,
            DateTimeOffset.UtcNow,
            AlertSeverity.Informational);

    public static MemberAddressImportStatusEvent Failed(
        string tenantId,
        string institutionId,
        string fileName,
        string error,
        int processedCount,
        int failedCount,
        string correlationId)
        => new(
            tenantId,
            institutionId,
            fileName,
            MemberAddressImportState.Failed,
            processedCount,
            failedCount,
            error,
            correlationId,
            DateTimeOffset.UtcNow,
            AlertSeverity.Critical);
}
