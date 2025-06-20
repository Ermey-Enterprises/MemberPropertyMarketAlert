using System.ComponentModel.DataAnnotations;

namespace MemberPropertyAlert.Core.Models
{
    public class ScanLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string? InstitutionId { get; set; }
        
        public string? AddressId { get; set; }
        
        public string? ScheduleId { get; set; }
        
        public ScanType ScanType { get; set; }
        
        public ScanStatus ScanStatus { get; set; } = ScanStatus.Started;
        
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
        
        public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);
        
        public int AddressesScanned { get; set; } = 0;
        
        public int AlertsGenerated { get; set; } = 0;
        
        public int ApiCallsMade { get; set; } = 0;
        
        public int ErrorsEncountered { get; set; } = 0;
        
        public string? ErrorMessage { get; set; }
        
        public List<ScanLogEntry> Entries { get; set; } = new();
        
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ScanLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public LogLevel Level { get; set; }
        
        public string Message { get; set; } = string.Empty;
        
        public string? AddressId { get; set; }
        
        public string? FullAddress { get; set; }
        
        public PropertyStatus? PreviousStatus { get; set; }
        
        public PropertyStatus? NewStatus { get; set; }
        
        public string? Exception { get; set; }
        
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public enum ScanType
    {
        Scheduled,
        Manual,
        OnDemand,
        Retry
    }

    public enum ScanStatus
    {
        Started,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }
}
