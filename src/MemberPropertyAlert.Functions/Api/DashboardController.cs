using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MemberPropertyAlert.Functions.Api
{
    /// <summary>
    /// Dashboard API endpoints for admin UI
    /// </summary>
    public class DashboardController
    {
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ILogger<DashboardController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        [Function("GetDashboardStats")]
        public async Task<HttpResponseData> GetStats(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/stats")] HttpRequestData req)
        {
            _logger.LogInformation("Getting dashboard statistics");

            try
            {
                // TODO: Replace with actual data from database
                var stats = new
                {
                    totalInstitutions = 5,
                    totalProperties = 1250,
                    activeAlerts = 12,
                    recentMatches = 3
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(stats));
                
                _logger.LogInformation("Dashboard stats returned successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        /// <summary>
        /// Get recent activity for dashboard
        /// </summary>
        [Function("GetRecentActivity")]
        public async Task<HttpResponseData> GetRecentActivity(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/recent-activity")] HttpRequestData req)
        {
            _logger.LogInformation("Getting recent activity");

            try
            {
                // TODO: Replace with actual data from database
                var activities = new[]
                {
                    new
                    {
                        id = 1,
                        type = "alert",
                        message = "Property alert generated for member at 123 Main St",
                        timestamp = DateTime.UtcNow.AddMinutes(-5),
                        institutionName = "First National Bank",
                        status = "pending"
                    },
                    new
                    {
                        id = 2,
                        type = "scan",
                        message = "Property scan completed for downtown area",
                        timestamp = DateTime.UtcNow.AddMinutes(-15),
                        institutionName = "Community Credit Union",
                        status = "completed"
                    },
                    new
                    {
                        id = 3,
                        type = "webhook",
                        message = "Webhook notification sent successfully",
                        timestamp = DateTime.UtcNow.AddMinutes(-30),
                        institutionName = "Regional Savings Bank",
                        status = "success"
                    },
                    new
                    {
                        id = 4,
                        type = "scan",
                        message = "Scheduled property scan started",
                        timestamp = DateTime.UtcNow.AddHours(-1),
                        institutionName = "Metro Financial",
                        status = "running"
                    },
                    new
                    {
                        id = 5,
                        type = "alert",
                        message = "Property match found at 456 Oak Avenue",
                        timestamp = DateTime.UtcNow.AddHours(-2),
                        institutionName = "First National Bank",
                        status = "delivered"
                    }
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(activities));
                
                _logger.LogInformation("Recent activity returned successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activity");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        /// <summary>
        /// Get system health status
        /// </summary>
        [Function("GetSystemHealth")]
        public async Task<HttpResponseData> GetSystemHealth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/health")] HttpRequestData req)
        {
            _logger.LogInformation("Getting system health status");

            try
            {
                var health = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    services = new
                    {
                        database = new { status = "healthy", responseTime = "45ms" },
                        signalr = new { status = "healthy", connections = 2 },
                        scheduler = new { status = "healthy", lastRun = DateTime.UtcNow.AddMinutes(-10) },
                        webhooks = new { status = "healthy", successRate = "98.5%" }
                    },
                    metrics = new
                    {
                        uptime = "99.9%",
                        requestsPerMinute = 125,
                        averageResponseTime = "120ms",
                        errorRate = "0.1%"
                    }
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(health));
                
                _logger.LogInformation("System health returned successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system health");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
