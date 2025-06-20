using System.ComponentModel.DataAnnotations;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Services;

namespace MemberPropertyAlert.Functions.Models
{
    // Address API Models
    public class CreateAddressRequest
    {
        [Required]
        public string AnonymousMemberId { get; set; } = string.Empty;
        
        [Required]
        public string StreetAddress { get; set; } = string.Empty;
        
        [Required]
        public string City { get; set; } = string.Empty;
        
        [Required]
        public string State { get; set; } = string.Empty;
        
        [Required]
        public string ZipCode { get; set; } = string.Empty;
        
        public string? Priority { get; set; }
        
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class UpdateAddressRequest
    {
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Priority { get; set; }
        public bool? IsActive { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class BulkCreateAddressRequest
    {
        [Required]
        public List<CreateAddressRequest> Addresses { get; set; } = new();
    }

    public class AddressResponse
    {
        public string Id { get; set; }
        public string InstitutionId { get; set; }
        public string AnonymousMemberId { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string FullAddress { get; set; }
        public PropertyStatus LastKnownStatus { get; set; }
        public DateTime? LastCheckedAt { get; set; }
        public DateTime? LastStatusChangeAt { get; set; }
        public string? Priority { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        public AddressResponse(MemberAddress address)
        {
            Id = address.Id;
            InstitutionId = address.InstitutionId;
            AnonymousMemberId = address.AnonymousMemberId;
            StreetAddress = address.StreetAddress;
            City = address.City;
            State = address.State;
            ZipCode = address.ZipCode;
            FullAddress = address.FullAddress;
            LastKnownStatus = address.LastKnownStatus;
            LastCheckedAt = address.LastCheckedAt;
            LastStatusChangeAt = address.LastStatusChangeAt;
            Priority = address.Priority;
            IsActive = address.IsActive;
            CreatedAt = address.CreatedAt;
            UpdatedAt = address.UpdatedAt;
            Metadata = address.Metadata;
        }
    }

    // Scan API Models
    public class ManualScanRequest
    {
        public List<string>? SpecificAddressIds { get; set; }
        public string? Priority { get; set; }
        public bool ForceRescan { get; set; } = false;
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class ScanResponse
    {
        public string ScanId { get; set; }
        public string InstitutionId { get; set; }
        public ScanType ScanType { get; set; }
        public ScanStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public int AddressesScanned { get; set; }
        public int AlertsGenerated { get; set; }
        public int ApiCallsMade { get; set; }
        public int ErrorsEncountered { get; set; }
        public string? ErrorMessage { get; set; }

        public ScanResponse(ScanLog scanLog)
        {
            ScanId = scanLog.Id;
            InstitutionId = scanLog.InstitutionId ?? string.Empty;
            ScanType = scanLog.ScanType;
            Status = scanLog.ScanStatus;
            StartedAt = scanLog.StartedAt;
            CompletedAt = scanLog.CompletedAt;
            Duration = scanLog.Duration;
            AddressesScanned = scanLog.AddressesScanned;
            AlertsGenerated = scanLog.AlertsGenerated;
            ApiCallsMade = scanLog.ApiCallsMade;
            ErrorsEncountered = scanLog.ErrorsEncountered;
            ErrorMessage = scanLog.ErrorMessage;
        }
    }

    // Alert API Models
    public class AlertResponse
    {
        public string Id { get; set; }
        public string InstitutionId { get; set; }
        public string AddressId { get; set; }
        public string AnonymousMemberId { get; set; }
        public string FullAddress { get; set; }
        public PropertyStatus PreviousStatus { get; set; }
        public PropertyStatus NewStatus { get; set; }
        public PropertyListingDetails? ListingDetails { get; set; }
        public AlertStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? WebhookSentAt { get; set; }
        public int WebhookAttempts { get; set; }
        public string? WebhookResponse { get; set; }
        public string? ErrorMessage { get; set; }

        public AlertResponse(PropertyAlert alert)
        {
            Id = alert.Id;
            InstitutionId = alert.InstitutionId;
            AddressId = alert.AddressId;
            AnonymousMemberId = alert.AnonymousMemberId;
            FullAddress = alert.FullAddress;
            PreviousStatus = alert.PreviousStatus;
            NewStatus = alert.NewStatus;
            ListingDetails = alert.ListingDetails;
            Status = alert.Status;
            CreatedAt = alert.CreatedAt;
            ProcessedAt = alert.ProcessedAt;
            WebhookSentAt = alert.WebhookSentAt;
            WebhookAttempts = alert.WebhookAttempts;
            WebhookResponse = alert.WebhookResponse;
            ErrorMessage = alert.ErrorMessage;
        }
    }

    // Institution API Models
    public class CreateInstitutionRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string ContactEmail { get; set; } = string.Empty;
        
        public string? WebhookUrl { get; set; }
        
        public string? WebhookAuthHeader { get; set; }
        
        public WebhookRetryPolicy? RetryPolicy { get; set; }
    }

    public class UpdateInstitutionRequest
    {
        public string? Name { get; set; }
        public string? ContactEmail { get; set; }
        public string? WebhookUrl { get; set; }
        public string? WebhookAuthHeader { get; set; }
        public WebhookRetryPolicy? RetryPolicy { get; set; }
        public bool? IsActive { get; set; }
    }

    public class InstitutionResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ContactEmail { get; set; }
        public string? WebhookUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ScanSchedule> ScanSchedules { get; set; }

        public InstitutionResponse(Institution institution)
        {
            Id = institution.Id;
            Name = institution.Name;
            ContactEmail = institution.ContactEmail;
            WebhookUrl = institution.WebhookUrl;
            IsActive = institution.IsActive;
            CreatedAt = institution.CreatedAt;
            UpdatedAt = institution.UpdatedAt;
            ScanSchedules = institution.ScanSchedules;
        }
    }

    // Schedule API Models
    public class CreateScheduleRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string CronExpression { get; set; } = string.Empty;
        
        public string? AddressFilter { get; set; }
    }

    public class UpdateScheduleRequest
    {
        public string? Name { get; set; }
        public string? CronExpression { get; set; }
        public string? AddressFilter { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ScheduleResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CronExpression { get; set; }
        public string? AddressFilter { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }

        public ScheduleResponse(ScanSchedule schedule, DateTime? nextRunTime = null, DateTime? lastRunTime = null)
        {
            Id = schedule.Id;
            Name = schedule.Name;
            CronExpression = schedule.CronExpression;
            AddressFilter = schedule.AddressFilter;
            IsActive = schedule.IsActive;
            CreatedAt = schedule.CreatedAt;
            NextRunTime = nextRunTime;
            LastRunTime = lastRunTime;
        }
    }

    // Statistics API Models
    public class StatisticsResponse
    {
        public int TotalScans { get; set; }
        public int TotalAddressesScanned { get; set; }
        public int TotalAlertsGenerated { get; set; }
        public int TotalApiCalls { get; set; }
        public int TotalErrors { get; set; }
        public TimeSpan AverageScanDuration { get; set; }
        public DateTime? LastScanAt { get; set; }
        public Dictionary<string, int> StatusBreakdown { get; set; } = new();
        public Dictionary<string, int> ErrorBreakdown { get; set; } = new();

        public StatisticsResponse(ScanStatistics stats)
        {
            TotalScans = stats.TotalScans;
            TotalAddressesScanned = stats.TotalAddressesScanned;
            TotalAlertsGenerated = stats.TotalAlertsGenerated;
            TotalApiCalls = stats.TotalApiCalls;
            TotalErrors = stats.TotalErrors;
            AverageScanDuration = stats.AverageScanDuration;
            LastScanAt = stats.LastScanAt;
            StatusBreakdown = stats.StatusBreakdown;
            ErrorBreakdown = stats.ErrorBreakdown;
        }
    }
}
