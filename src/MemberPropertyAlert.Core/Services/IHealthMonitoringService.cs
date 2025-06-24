namespace MemberPropertyAlert.Core.Services
{
    /// <summary>
    /// Service for monitoring application health and status
    /// </summary>
    public interface IHealthMonitoringService
    {
        /// <summary>
        /// Gets the current application status
        /// </summary>
        Task<HealthStatus> GetHealthStatusAsync();

        /// <summary>
        /// Gets detailed system information
        /// </summary>
        Task<SystemInfo> GetSystemInfoAsync();
    }

    /// <summary>
    /// Represents the health status of the application
    /// </summary>
    public class HealthStatus
    {
        public string Status { get; set; } = "Unknown";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0.1";
        public bool IsHealthy { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Represents system information
    /// </summary>
    public class SystemInfo
    {
        public string Environment { get; set; } = string.Empty;
        public bool HealthMonitoringEnabled { get; set; }
        public DateTime StartTime { get; set; }
        public Dictionary<string, string> Configuration { get; set; } = new();
    }
}
