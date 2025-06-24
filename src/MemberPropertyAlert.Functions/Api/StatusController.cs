using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Api
{
    /// <summary>
    /// Status and monitoring endpoints for deployment testing
    /// </summary>
    public class StatusController
    {
        private readonly ILogger<StatusController> _logger;

        public StatusController(ILogger<StatusController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get deployment status and version information
        /// </summary>
        [Function("GetStatus")]
        public IActionResult GetStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status")] HttpRequest req)
        {
            _logger.LogInformation("Status endpoint called");

            var status = new
            {
                Version = "1.0.1",
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                HealthMonitoring = Environment.GetEnvironmentVariable("ENABLE_HEALTH_CHECK_MONITORING") ?? "false"
            };

            return new OkObjectResult(status);
        }
    }
}
