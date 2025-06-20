using MemberPropertyAlert.Core.Common;
using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Services.Notifications
{
    /// <summary>
    /// Service for webhook-based notifications following Single Responsibility Principle
    /// </summary>
    public interface IWebhookNotificationService
    {
        Task<Result<NotificationResult>> SendAsync(PropertyAlert alert, WebhookSettings settings);
        Task<Result<NotificationResult>> SendBulkAsync(IEnumerable<PropertyAlert> alerts, WebhookSettings settings);
        Task<Result<NotificationResult>> RetryAsync(PropertyAlert alert, WebhookSettings settings, int attemptNumber);
        Task<Result<bool>> ValidateEndpointAsync(string webhookUrl, string? authHeader = null);
    }

    /// <summary>
    /// Service for email-based notifications
    /// </summary>
    public interface IEmailNotificationService
    {
        Task<Result<NotificationResult>> SendAsync(PropertyAlert alert, EmailSettings settings);
        Task<Result<NotificationResult>> SendBulkAsync(IEnumerable<PropertyAlert> alerts, EmailSettings settings);
        Task<Result<NotificationResult>> RetryAsync(PropertyAlert alert, EmailSettings settings, int attemptNumber);
        Task<Result<bool>> ValidateConfigurationAsync(EmailSettings settings);
    }

    /// <summary>
    /// Service for CSV-based notifications
    /// </summary>
    public interface ICsvNotificationService
    {
        Task<Result<NotificationResult>> GenerateAndDeliverAsync(IEnumerable<PropertyAlert> alerts, CsvSettings settings);
        Task<Result<string>> GenerateCsvContentAsync(IEnumerable<PropertyAlert> alerts, CsvSettings settings);
        Task<Result<byte[]>> GenerateCsvBytesAsync(IEnumerable<PropertyAlert> alerts, CsvSettings settings);
        Task<Result<NotificationResult>> DeliverCsvAsync(string csvContent, string fileName, CsvSettings settings);
    }

    /// <summary>
    /// Orchestrator service that coordinates multiple notification types (Facade Pattern)
    /// </summary>
    public interface INotificationOrchestrator
    {
        Task<Result<IEnumerable<NotificationResult>>> ProcessNotificationAsync(PropertyAlert alert, Institution institution);
        Task<Result<IEnumerable<NotificationResult>>> ProcessBulkNotificationAsync(IEnumerable<PropertyAlert> alerts, Institution institution);
        Task<Result<IEnumerable<NotificationResult>>> RetryFailedNotificationAsync(string notificationId, Institution institution);
    }

    /// <summary>
    /// Factory for creating notification services (Factory Pattern)
    /// </summary>
    public interface INotificationServiceFactory
    {
        IWebhookNotificationService CreateWebhookService();
        IEmailNotificationService CreateEmailService();
        ICsvNotificationService CreateCsvService();
        INotificationOrchestrator CreateOrchestrator();
    }
}
