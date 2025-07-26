using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MemberPropertyAlert.Functions.Api
{
    /// <summary>
    /// SignalR hub functions for real-time logging and notifications
    /// </summary>
    public class LogHubController
    {
        private readonly ILogger<LogHubController> _logger;

        public LogHubController(ILogger<LogHubController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// SignalR negotiate function - required for client connections
        /// </summary>
        [Function("negotiate")]
        public SignalRConnectionInfo Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [SignalRConnectionInfoInput(HubName = "loghub")] SignalRConnectionInfo connectionInfo)
        {
            _logger.LogInformation("SignalR negotiate requested");
            return connectionInfo;
        }

        /// <summary>
        /// Function to send log messages to all connected clients
        /// </summary>
        [Function("SendLogMessage")]
        [SignalROutput(HubName = "loghub")]
        public async Task<SignalRMessageAction> SendLogMessage(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "loghub/send")] HttpRequestData req)
        {
            _logger.LogInformation("SendLogMessage function executed");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var logMessage = JsonSerializer.Deserialize<LogMessageRequest>(requestBody);

                if (logMessage == null)
                {
                    throw new ArgumentException("Invalid log message format");
                }

                // Create the log entry
                var logEntry = new
                {
                    Level = logMessage.Level,
                    Message = logMessage.Message,
                    Source = logMessage.Source ?? "System",
                    Timestamp = DateTime.UtcNow,
                    Category = logMessage.Category ?? "General"
                };

                _logger.LogInformation("Log message sent to SignalR clients: {Message}", logMessage.Message);

                // Return SignalR message action
                return new SignalRMessageAction("LogMessage")
                {
                    Arguments = new[] { logEntry }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending log message via SignalR");
                throw;
            }
        }

        /// <summary>
        /// Function to send scan status updates to all connected clients
        /// </summary>
        [Function("SendScanStatusUpdate")]
        [SignalROutput(HubName = "loghub")]
        public async Task<SignalRMessageAction> SendScanStatusUpdate(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "loghub/scan-status")] HttpRequestData req)
        {
            _logger.LogInformation("SendScanStatusUpdate function executed");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var statusUpdate = JsonSerializer.Deserialize<ScanStatusRequest>(requestBody);

                if (statusUpdate == null)
                {
                    throw new ArgumentException("Invalid status update format");
                }

                // Create the status update
                var scanStatus = new
                {
                    Status = statusUpdate.Status,
                    Data = statusUpdate.Data,
                    InstitutionId = statusUpdate.InstitutionId,
                    Progress = statusUpdate.Progress,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Scan status update sent to SignalR clients: {Status}", statusUpdate.Status);

                // Return SignalR message action
                return new SignalRMessageAction("ScanStatusUpdate")
                {
                    Arguments = new[] { scanStatus }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending scan status update via SignalR");
                throw;
            }
        }

        /// <summary>
        /// Test function to send a test message
        /// </summary>
        [Function("SendTestMessage")]
        [SignalROutput(HubName = "loghub")]
        public SignalRMessageAction SendTestMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "loghub/test")] HttpRequestData req)
        {
            _logger.LogInformation("SendTestMessage function executed");

            var testMessage = new
            {
                Level = "Info",
                Message = "Test message from SignalR hub - connection working!",
                Source = "SignalR Test",
                Timestamp = DateTime.UtcNow,
                Category = "Test"
            };

            _logger.LogInformation("Test message sent to SignalR clients");

            return new SignalRMessageAction("LogMessage")
            {
                Arguments = new[] { testMessage }
            };
        }
    }

    /// <summary>
    /// Request model for log messages
    /// </summary>
    public class LogMessageRequest
    {
        public string Level { get; set; } = "Info";
        public string Message { get; set; } = "";
        public string? Source { get; set; }
        public string? Category { get; set; }
    }

    /// <summary>
    /// Request model for scan status updates
    /// </summary>
    public class ScanStatusRequest
    {
        public string Status { get; set; } = "";
        public object? Data { get; set; }
        public string? InstitutionId { get; set; }
        public int? Progress { get; set; }
    }
}
