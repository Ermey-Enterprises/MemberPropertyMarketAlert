using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MemberPropertyAlert.Functions.Api
{
    public class HealthController
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        [Function("Health")]
        public async Task<IActionResult> GetHealth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
        {
            _logger.LogInformation("Health check requested");

            var healthStatus = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development",
                Service = "Member Property Market Alert API",
                LastUpdated = "2025-06-20T03:36:00Z" // Updated to trigger CI/CD test
            };

            return new OkObjectResult(healthStatus);
        }
    }
}
