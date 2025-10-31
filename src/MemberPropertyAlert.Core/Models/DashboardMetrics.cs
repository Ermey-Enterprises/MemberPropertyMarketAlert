namespace MemberPropertyAlert.Core.Models;

public sealed record DashboardMetrics(
    int TotalInstitutions,
    int ActiveInstitutions,
    int TotalAddresses,
    int ActiveAddresses,
    int AlertsLast24Hours,
    int AlertsLast7Days,
    int PendingScanJobs,
    decimal AverageRentDelta,
    decimal MedianRent
);
