using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Services
{
    public interface ICosmosService
    {
        // Database Initialization
        Task InitializeDatabaseAsync();

        // Institution Management
        Task<Institution> CreateInstitutionAsync(Institution institution);
        Task<Institution?> GetInstitutionAsync(string institutionId);
        Task<Institution> UpdateInstitutionAsync(Institution institution);
        Task DeleteInstitutionAsync(string institutionId);
        Task<List<Institution>> GetAllInstitutionsAsync();

        // Address Management
        Task<MemberAddress> CreateAddressAsync(MemberAddress address);
        Task<MemberAddress?> GetAddressAsync(string addressId);
        Task<MemberAddress> UpdateAddressAsync(MemberAddress address);
        Task DeleteAddressAsync(string addressId);
        Task<List<MemberAddress>> GetAddressesByInstitutionAsync(string institutionId);
        Task<List<MemberAddress>> GetActiveAddressesAsync();
        Task<List<MemberAddress>> GetAddressesByFilterAsync(string institutionId, string? priority = null, bool? isActive = null);
        Task<List<MemberAddress>> CreateBulkAddressesAsync(List<MemberAddress> addresses);

        // Alert Management
        Task<PropertyAlert> CreateAlertAsync(PropertyAlert alert);
        Task<PropertyAlert?> GetAlertAsync(string alertId);
        Task<PropertyAlert> UpdateAlertAsync(PropertyAlert alert);
        Task<List<PropertyAlert>> GetAlertsByInstitutionAsync(string institutionId, int? limit = null);
        Task<List<PropertyAlert>> GetPendingAlertsAsync();
        Task<List<PropertyAlert>> GetAlertsByStatusAsync(AlertStatus status);

        // Scan Log Management
        Task<ScanLog> CreateScanLogAsync(ScanLog scanLog);
        Task<ScanLog?> GetScanLogAsync(string scanLogId);
        Task<ScanLog> UpdateScanLogAsync(ScanLog scanLog);
        Task<List<ScanLog>> GetScanLogsByInstitutionAsync(string institutionId, int? limit = null);
        Task<List<ScanLog>> GetRecentScanLogsAsync(int limit = 50);
        Task<ScanLog?> GetActiveScanLogAsync(string? institutionId = null);

        // Analytics and Reporting
        Task<ScanStatistics> GetScanStatisticsAsync(string? institutionId = null, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<AlertSummary>> GetAlertSummaryAsync(string? institutionId = null, DateTime? fromDate = null, DateTime? toDate = null);
    }

    public class ScanStatistics
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
    }

    public class AlertSummary
    {
        public string InstitutionId { get; set; } = string.Empty;
        public string InstitutionName { get; set; } = string.Empty;
        public int TotalAlerts { get; set; }
        public int PendingAlerts { get; set; }
        public int SentAlerts { get; set; }
        public int FailedAlerts { get; set; }
        public DateTime? LastAlertAt { get; set; }
        public Dictionary<PropertyStatus, int> StatusChangeBreakdown { get; set; } = new();
    }

    public class CosmosConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "MemberPropertyAlert";
        public string InstitutionsContainer { get; set; } = "Institutions";
        public string AddressesContainer { get; set; } = "Addresses";
        public string AlertsContainer { get; set; } = "Alerts";
        public string ScanLogsContainer { get; set; } = "ScanLogs";
        public int DefaultThroughput { get; set; } = 400;
        public bool EnableAutoscale { get; set; } = true;
        public int MaxThroughput { get; set; } = 4000;
    }
}
