using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Api
{
    public class SimpleHealthController
    {
        private readonly ILogger<SimpleHealthController> _logger;

        public SimpleHealthController(ILogger<SimpleHealthController> logger)
        {
            _logger = logger;
        }

        [Function("SimpleHealth")]
        public IActionResult GetSimpleHealth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "simple-health")] HttpRequest req)
        {
            _logger.LogInformation("Simple health check requested");

            var healthStatus = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Service = "Member Property Market Alert API - Simple"
            };

            return new OkObjectResult(healthStatus);
        }
    }
}
