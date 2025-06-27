using Microsoft.AspNetCore.Mvc;
using MemberPropertyAlert.UI.Hubs;

namespace MemberPropertyAlert.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ILogger<DashboardController> _logger;
        private readonly ILogHubService _logHubService;

        public DashboardController(ILogger<DashboardController> logger, ILogHubService logHubService)
        {
            _logger = logger;
            _logHubService = logHubService;
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            return Ok(new
            {
                totalInstitutions = 12,
                totalProperties = 2547,
                activeAlerts = 156,
                recentMatches = 8,
                SystemHealth = "Healthy",
                LastUpdate = DateTime.UtcNow,
                Performance = new
                {
                    CpuUsage = 45.2,
                    MemoryUsage = 62.8,
                    DiskUsage = 34.1
                }
            });
        }

        [HttpGet("recent-activity")]
        public IActionResult GetRecentActivity()
        {
            var activities = new[]
            {
                new
                {
                    id = 1,
                    type = "scan",
                    message = "Property scan completed for Institution ABC",
                    timestamp = DateTime.UtcNow.AddMinutes(-5),
                    status = "Success"
                },
                new
                {
                    id = 2,
                    type = "match",
                    message = "New property alert triggered",
                    timestamp = DateTime.UtcNow.AddMinutes(-12),
                    status = "Info"
                },
                new
                {
                    id = 3,
                    type = "system",
                    message = "Database backup completed",
                    timestamp = DateTime.UtcNow.AddMinutes(-25),
                    status = "Success"
                },
                new
                {
                    id = 4,
                    type = "error",
                    message = "Failed to connect to external API",
                    timestamp = DateTime.UtcNow.AddMinutes(-45),
                    status = "Error"
                },
                new
                {
                    id = 5,
                    type = "scan",
                    message = "Scheduled scan started",
                    timestamp = DateTime.UtcNow.AddHours(-1),
                    status = "Running"
                }
            };

            return Ok(activities);
        }

        [HttpGet("charts/scan-history")]
        public IActionResult GetScanHistory()
        {
            var history = new[]
            {
                new { Date = DateTime.UtcNow.AddDays(-6).ToString("yyyy-MM-dd"), Scans = 15, Alerts = 3 },
                new { Date = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd"), Scans = 18, Alerts = 5 },
                new { Date = DateTime.UtcNow.AddDays(-4).ToString("yyyy-MM-dd"), Scans = 12, Alerts = 2 },
                new { Date = DateTime.UtcNow.AddDays(-3).ToString("yyyy-MM-dd"), Scans = 22, Alerts = 8 },
                new { Date = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd"), Scans = 19, Alerts = 4 },
                new { Date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"), Scans = 25, Alerts = 6 },
                new { Date = DateTime.UtcNow.ToString("yyyy-MM-dd"), Scans = 14, Alerts = 3 }
            };

            return Ok(history);
        }

        [HttpGet("alerts/summary")]
        public IActionResult GetAlertsSummary()
        {
            return Ok(new
            {
                Total = 156,
                Today = 8,
                ThisWeek = 42,
                ThisMonth = 156,
                ByType = new
                {
                    PropertyMatch = 89,
                    PriceAlert = 34,
                    LocationAlert = 23,
                    SystemAlert = 10
                },
                ByStatus = new
                {
                    Active = 12,
                    Resolved = 134,
                    Pending = 10
                }
            });
        }
    }
}
