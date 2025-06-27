using Microsoft.AspNetCore.SignalR;

namespace MemberPropertyAlert.UI.Hubs
{
    /// <summary>
    /// SignalR hub for real-time log streaming and scan status updates
    /// </summary>
    public class LogHub : Hub
    {
        private readonly ILogger<LogHub> _logger;

        public LogHub(ILogger<LogHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected to LogHub: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected from LogHub: {ConnectionId}", Context.ConnectionId);
            if (exception != null)
            {
                _logger.LogError(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join a specific group for targeted log streaming
        /// </summary>
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
        }

        /// <summary>
        /// Leave a specific group
        /// </summary>
        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
        }
    }

    /// <summary>
    /// Service for sending log messages and scan updates to connected clients
    /// </summary>
    public interface ILogHubService
    {
        Task SendLogMessage(string level, string message, string? source = null);
        Task SendScanStatusUpdate(string status, object? data = null);
    }

    public class LogHubService : ILogHubService
    {
        private readonly IHubContext<LogHub> _hubContext;
        private readonly ILogger<LogHubService> _logger;

        public LogHubService(IHubContext<LogHub> hubContext, ILogger<LogHubService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendLogMessage(string level, string message, string? source = null)
        {
            try
            {
                var logEntry = new
                {
                    Level = level,
                    Message = message,
                    Source = source ?? "System",
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("LogMessage", logEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending log message via SignalR");
            }
        }

        public async Task SendScanStatusUpdate(string status, object? data = null)
        {
            try
            {
                var statusUpdate = new
                {
                    Status = status,
                    Data = data,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("ScanStatusUpdate", statusUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending scan status update via SignalR");
            }
        }
    }
}
