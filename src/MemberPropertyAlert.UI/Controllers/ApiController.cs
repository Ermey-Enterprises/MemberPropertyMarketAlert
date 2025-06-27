using Microsoft.AspNetCore.Mvc;
using MemberPropertyAlert.UI.Hubs;

namespace MemberPropertyAlert.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private readonly ILogHubService _logHubService;

        public StatusController(ILogger<StatusController> logger, ILogHubService logHubService)
        {
            _logger = logger;
            _logHubService = logHubService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "2.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                Status = "Healthy",
                Checks = new
                {
                    Database = "Healthy",
                    SignalR = "Healthy",
                    Memory = GC.GetTotalMemory(false)
                },
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("test-log")]
        public async Task<IActionResult> TestLog([FromBody] TestLogRequest request)
        {
            try
            {
                await _logHubService.SendLogMessage(
                    request.Level ?? "Info",
                    request.Message ?? "Test log message",
                    "API Test"
                );

                return Ok(new { Success = true, Message = "Log sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test log");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
    }

    public class TestLogRequest
    {
        public string? Level { get; set; }
        public string? Message { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ScanController : ControllerBase
    {
        private readonly ILogger<ScanController> _logger;
        private readonly ILogHubService _logHubService;

        public ScanController(ILogger<ScanController> logger, ILogHubService logHubService)
        {
            _logger = logger;
            _logHubService = logHubService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartScan([FromBody] StartScanRequest? request)
        {
            try
            {
                _logger.LogInformation("Scan start requested");
                
                await _logHubService.SendLogMessage("Info", "Scan started via API", "Scan Controller");
                await _logHubService.SendScanStatusUpdate("Starting", new { RequestedBy = "API" });

                // Simulate scan process
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    await _logHubService.SendLogMessage("Info", "Scan in progress...", "Scan Engine");
                    await _logHubService.SendScanStatusUpdate("Running", new { Progress = 50 });
                    
                    await Task.Delay(3000);
                    await _logHubService.SendLogMessage("Info", "Scan completed successfully", "Scan Engine");
                    await _logHubService.SendScanStatusUpdate("Completed", new { Progress = 100, Duration = "5 seconds" });
                });

                return Ok(new
                {
                    Success = true,
                    Message = "Scan started successfully",
                    ScanId = Guid.NewGuid().ToString(),
                    StartedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting scan");
                await _logHubService.SendLogMessage("Error", $"Failed to start scan: {ex.Message}", "Scan Controller");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("stop")]
        public async Task<IActionResult> StopScan()
        {
            try
            {
                _logger.LogInformation("Scan stop requested");
                
                await _logHubService.SendLogMessage("Info", "Scan stopped via API", "Scan Controller");
                await _logHubService.SendScanStatusUpdate("Stopped", new { StoppedBy = "API" });

                return Ok(new
                {
                    Success = true,
                    Message = "Scan stopped successfully",
                    StoppedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping scan");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetScanStatus()
        {
            return Ok(new
            {
                Status = "Idle",
                LastScan = DateTime.UtcNow.AddHours(-1),
                NextScan = DateTime.UtcNow.AddHours(1),
                TotalScans = 42,
                SuccessfulScans = 40,
                FailedScans = 2
            });
        }

        [HttpGet("stats")]
        public IActionResult GetScanStats()
        {
            return Ok(new
            {
                totalStates = 50,
                propertiesMonitored = 2547,
                newListings = 23,
                matches = 8,
                lastScanTime = DateTime.UtcNow.AddHours(-1),
                TotalScans = 42,
                SuccessfulScans = 40,
                FailedScans = 2,
                AverageTime = "2.5 minutes",
                NextScheduledScan = DateTime.UtcNow.AddHours(1),
                ScanFrequency = "Every 2 hours",
                ActiveInstitutions = 12
            });
        }

        [HttpGet("schedule")]
        public IActionResult GetScanSchedule()
        {
            // Return the current schedule configuration that ScanControl.js expects
            return Ok(new
            {
                enabled = true,
                frequency = "daily",
                time = "09:00",
                timezone = "UTC",
                nextRun = DateTime.UtcNow.AddHours(8)
            });
        }

        [HttpPut("schedule")]
        public IActionResult UpdateScanSchedule([FromBody] UpdateScheduleRequest request)
        {
            // In a real implementation, this would update the schedule in the database
            return Ok(new
            {
                enabled = request.Enabled,
                frequency = request.Frequency,
                time = request.Time,
                timezone = request.Timezone,
                nextRun = DateTime.UtcNow.AddHours(8) // Calculate next run based on schedule
            });
        }

        [HttpGet("schedules")]
        public IActionResult GetAllScanSchedules()
        {
            var schedules = new[]
            {
                new
                {
                    Id = 1,
                    Name = "Daily Property Scan",
                    CronExpression = "0 0 8 * * *",
                    NextRun = DateTime.UtcNow.AddHours(8),
                    LastRun = DateTime.UtcNow.AddHours(-16),
                    IsActive = true,
                    InstitutionCount = 12
                },
                new
                {
                    Id = 2,
                    Name = "Hourly Quick Scan",
                    CronExpression = "0 0 * * * *",
                    NextRun = DateTime.UtcNow.AddMinutes(45),
                    LastRun = DateTime.UtcNow.AddMinutes(-15),
                    IsActive = true,
                    InstitutionCount = 5
                },
                new
                {
                    Id = 3,
                    Name = "Weekly Deep Scan",
                    CronExpression = "0 0 2 * * 0",
                    NextRun = DateTime.UtcNow.AddDays(3),
                    LastRun = DateTime.UtcNow.AddDays(-4),
                    IsActive = false,
                    InstitutionCount = 12
                }
            };

            return Ok(schedules);
        }
    }

    public class StartScanRequest
    {
        public string? Type { get; set; }
        public string[]? Targets { get; set; }
        public bool? FullScan { get; set; }
    }

    public class UpdateScheduleRequest
    {
        public bool Enabled { get; set; }
        public string? Frequency { get; set; }
        public string? Time { get; set; }
        public string? Timezone { get; set; }
    }
}
