using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Services
{
    public interface ISchedulingService
    {
        Task<ScanScheduleResult> CreateScheduleAsync(string institutionId, ScanSchedule schedule);
        Task<ScanScheduleResult> UpdateScheduleAsync(string institutionId, ScanSchedule schedule);
        Task DeleteScheduleAsync(string institutionId, string scheduleId);
        Task<List<ScanSchedule>> GetSchedulesAsync(string institutionId);
        Task<List<ScheduledScanInfo>> GetDueScansAsync();
        Task<bool> ValidateCronExpressionAsync(string cronExpression);
        Task<DateTime?> GetNextRunTimeAsync(string cronExpression);
        Task<ScanLog> TriggerManualScanAsync(string institutionId, ManualScanRequest request);
        Task<ScanLog> TriggerScheduledScanAsync(string scheduleId);
    }

    public interface IPropertyScanService
    {
        Task<ScanResult> ScanAddressesAsync(List<MemberAddress> addresses, ScanContext context);
        Task<ScanResult> ScanSingleAddressAsync(MemberAddress address, ScanContext context);
        Task<List<PropertyAlert>> ProcessScanResultsAsync(List<PropertyScanResult> results, ScanContext context);
        Task<bool> ShouldGenerateAlertAsync(MemberAddress address, PropertyListingResult result);
    }

    public class ScanScheduleResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public ScanSchedule? Schedule { get; set; }
        public DateTime? NextRunTime { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    public class ScheduledScanInfo
    {
        public string ScheduleId { get; set; } = string.Empty;
        public string InstitutionId { get; set; } = string.Empty;
        public string ScheduleName { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;
        public DateTime NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        public string? AddressFilter { get; set; }
        public int EstimatedAddressCount { get; set; }
    }

    public class ManualScanRequest
    {
        public ScanType ScanType { get; set; } = ScanType.Manual;
        public List<string>? SpecificAddressIds { get; set; }
        public string? Priority { get; set; }
        public bool ForceRescan { get; set; } = false;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ScanContext
    {
        public string ScanId { get; set; } = Guid.NewGuid().ToString();
        public string InstitutionId { get; set; } = string.Empty;
        public string? ScheduleId { get; set; }
        public ScanType ScanType { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public string? InitiatedBy { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ScanResult
    {
        public bool IsSuccess { get; set; }
        public string ScanId { get; set; } = string.Empty;
        public int AddressesScanned { get; set; }
        public int AlertsGenerated { get; set; }
        public int ApiCallsMade { get; set; }
        public int ErrorsEncountered { get; set; }
        public TimeSpan Duration { get; set; }
        public List<PropertyScanResult> Results { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    public class PropertyScanResult
    {
        public string AddressId { get; set; } = string.Empty;
        public MemberAddress Address { get; set; } = new();
        public PropertyListingResult ListingResult { get; set; } = new();
        public bool StatusChanged { get; set; }
        public PropertyStatus PreviousStatus { get; set; }
        public PropertyStatus NewStatus { get; set; }
        public bool AlertGenerated { get; set; }
        public string? AlertId { get; set; }
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }

    public class SchedulingConfiguration
    {
        public int MaxConcurrentScans { get; set; } = 5;
        public int ScanTimeoutMinutes { get; set; } = 30;
        public int DefaultBatchSize { get; set; } = 50;
        public int RateLimitDelayMs { get; set; } = 1000;
        public bool EnableParallelProcessing { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public List<int> RetryDelaySeconds { get; set; } = new() { 30, 60, 120 };
        public string DefaultTimeZone { get; set; } = "UTC";
    }
}
