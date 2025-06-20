using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRouting();

// In-memory storage for received webhooks
var receivedWebhooks = new List<WebhookReceived>();
var maxWebhooks = 100; // Keep only the last 100 webhooks

// Webhook endpoint
app.MapPost("/webhook", async (HttpContext context, ILogger<Program> logger) =>
{
    try
    {
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        
        var webhook = new WebhookReceived
        {
            Id = Guid.NewGuid().ToString(),
            ReceivedAt = DateTime.UtcNow,
            Method = context.Request.Method,
            Path = context.Request.Path,
            Headers = headers,
            Body = body,
            ContentType = context.Request.ContentType ?? "unknown",
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        // Add to collection (keep only the most recent)
        lock (receivedWebhooks)
        {
            receivedWebhooks.Insert(0, webhook);
            if (receivedWebhooks.Count > maxWebhooks)
            {
                receivedWebhooks.RemoveAt(receivedWebhooks.Count - 1);
            }
        }

        logger.LogInformation("Webhook received: {Id} from {UserAgent}", webhook.Id, webhook.UserAgent);
        logger.LogInformation("Body: {Body}", body);

        return Results.Ok(new { message = "Webhook received successfully", id = webhook.Id });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing webhook");
        return Results.Problem("Error processing webhook");
    }
});

// Get all received webhooks
app.MapGet("/webhooks", () =>
{
    lock (receivedWebhooks)
    {
        return Results.Ok(receivedWebhooks.ToList());
    }
});

// Get specific webhook by ID
app.MapGet("/webhooks/{id}", (string id) =>
{
    lock (receivedWebhooks)
    {
        var webhook = receivedWebhooks.FirstOrDefault(w => w.Id == id);
        return webhook != null ? Results.Ok(webhook) : Results.NotFound();
    }
});

// Clear all webhooks
app.MapDelete("/webhooks", () =>
{
    lock (receivedWebhooks)
    {
        receivedWebhooks.Clear();
    }
    return Results.Ok(new { message = "All webhooks cleared" });
});

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Serve static files for the web interface
app.UseStaticFiles();

// Default route to serve the webhook viewer
app.MapGet("/", () => Results.Content(GetWebhookViewerHtml(), "text/html"));

app.Run();

// HTML for the webhook viewer interface
static string GetWebhookViewerHtml()
{
    return """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Mock Webhook Client - Property Alert Receiver</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background-color: #f5f5f5;
            color: #333;
        }
        
        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }
        
        .header {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }
        
        .header h1 {
            color: #2563eb;
            margin-bottom: 10px;
        }
        
        .stats {
            display: flex;
            gap: 20px;
            margin-bottom: 20px;
        }
        
        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            flex: 1;
            text-align: center;
        }
        
        .stat-number {
            font-size: 2em;
            font-weight: bold;
            color: #2563eb;
        }
        
        .controls {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            margin-bottom: 20px;
            display: flex;
            gap: 10px;
            align-items: center;
        }
        
        button {
            background: #2563eb;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 14px;
        }
        
        button:hover {
            background: #1d4ed8;
        }
        
        button.danger {
            background: #dc2626;
        }
        
        button.danger:hover {
            background: #b91c1c;
        }
        
        .webhook-list {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .webhook-item {
            border-bottom: 1px solid #e5e7eb;
            padding: 20px;
            cursor: pointer;
            transition: background-color 0.2s;
        }
        
        .webhook-item:hover {
            background-color: #f9fafb;
        }
        
        .webhook-item:last-child {
            border-bottom: none;
        }
        
        .webhook-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 10px;
        }
        
        .webhook-id {
            font-family: monospace;
            background: #f3f4f6;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
        }
        
        .webhook-time {
            color: #6b7280;
            font-size: 14px;
        }
        
        .webhook-method {
            background: #10b981;
            color: white;
            padding: 2px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: bold;
        }
        
        .webhook-details {
            display: none;
            margin-top: 15px;
            padding-top: 15px;
            border-top: 1px solid #e5e7eb;
        }
        
        .webhook-details.show {
            display: block;
        }
        
        .detail-section {
            margin-bottom: 15px;
        }
        
        .detail-title {
            font-weight: bold;
            margin-bottom: 5px;
            color: #374151;
        }
        
        .detail-content {
            background: #f9fafb;
            padding: 10px;
            border-radius: 4px;
            font-family: monospace;
            font-size: 12px;
            white-space: pre-wrap;
            max-height: 200px;
            overflow-y: auto;
        }
        
        .no-webhooks {
            text-align: center;
            padding: 40px;
            color: #6b7280;
        }
        
        .status-indicator {
            display: inline-block;
            width: 8px;
            height: 8px;
            background: #10b981;
            border-radius: 50%;
            margin-right: 8px;
        }
        
        .endpoint-info {
            background: #f0f9ff;
            border: 1px solid #0ea5e9;
            border-radius: 6px;
            padding: 15px;
            margin-bottom: 20px;
        }
        
        .endpoint-url {
            font-family: monospace;
            background: white;
            padding: 8px 12px;
            border-radius: 4px;
            border: 1px solid #e5e7eb;
            margin-top: 8px;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🎯 Mock Webhook Client</h1>
            <p>Property Alert Webhook Receiver for Testing & Demonstration</p>
        </div>
        
        <div class="endpoint-info">
            <strong>Webhook Endpoint:</strong>
            <div class="endpoint-url" id="webhookUrl">Loading...</div>
            <small>Use this URL as the webhook endpoint in your institution settings</small>
        </div>
        
        <div class="stats">
            <div class="stat-card">
                <div class="stat-number" id="totalWebhooks">0</div>
                <div>Total Received</div>
            </div>
            <div class="stat-card">
                <div class="stat-number" id="recentWebhooks">0</div>
                <div>Last Hour</div>
            </div>
            <div class="stat-card">
                <div class="stat-number">
                    <span class="status-indicator"></span>
                    Online
                </div>
                <div>Status</div>
            </div>
        </div>
        
        <div class="controls">
            <button onclick="refreshWebhooks()">🔄 Refresh</button>
            <button onclick="clearWebhooks()" class="danger">🗑️ Clear All</button>
            <span style="margin-left: auto; color: #6b7280;">Auto-refresh: <span id="autoRefreshStatus">ON</span></span>
        </div>
        
        <div class="webhook-list" id="webhookList">
            <div class="no-webhooks">
                <h3>No webhooks received yet</h3>
                <p>Configure your Property Alert system to send webhooks to the endpoint above</p>
            </div>
        </div>
    </div>

    <script>
        let autoRefresh = true;
        let refreshInterval;
        
        // Set webhook URL
        document.getElementById('webhookUrl').textContent = `${window.location.origin}/webhook`;
        
        function formatDate(dateString) {
            const date = new Date(dateString);
            return date.toLocaleString();
        }
        
        function formatJson(jsonString) {
            try {
                const obj = JSON.parse(jsonString);
                return JSON.stringify(obj, null, 2);
            } catch {
                return jsonString;
            }
        }
        
        function toggleWebhookDetails(id) {
            const details = document.getElementById(`details-${id}`);
            details.classList.toggle('show');
        }
        
        async function refreshWebhooks() {
            try {
                const response = await fetch('/webhooks');
                const webhooks = await response.json();
                
                updateStats(webhooks);
                renderWebhooks(webhooks);
            } catch (error) {
                console.error('Error fetching webhooks:', error);
            }
        }
        
        function updateStats(webhooks) {
            const total = webhooks.length;
            const oneHourAgo = new Date(Date.now() - 60 * 60 * 1000);
            const recent = webhooks.filter(w => new Date(w.receivedAt) > oneHourAgo).length;
            
            document.getElementById('totalWebhooks').textContent = total;
            document.getElementById('recentWebhooks').textContent = recent;
        }
        
        function renderWebhooks(webhooks) {
            const container = document.getElementById('webhookList');
            
            if (webhooks.length === 0) {
                container.innerHTML = `
                    <div class="no-webhooks">
                        <h3>No webhooks received yet</h3>
                        <p>Configure your Property Alert system to send webhooks to the endpoint above</p>
                    </div>
                `;
                return;
            }
            
            container.innerHTML = webhooks.map(webhook => `
                <div class="webhook-item" onclick="toggleWebhookDetails('${webhook.id}')">
                    <div class="webhook-header">
                        <div>
                            <span class="webhook-method">${webhook.method}</span>
                            <span class="webhook-id">${webhook.id}</span>
                        </div>
                        <div class="webhook-time">${formatDate(webhook.receivedAt)}</div>
                    </div>
                    <div><strong>Path:</strong> ${webhook.path}</div>
                    <div><strong>Content-Type:</strong> ${webhook.contentType}</div>
                    <div><strong>User-Agent:</strong> ${webhook.userAgent}</div>
                    
                    <div class="webhook-details" id="details-${webhook.id}">
                        <div class="detail-section">
                            <div class="detail-title">Headers</div>
                            <div class="detail-content">${JSON.stringify(webhook.headers, null, 2)}</div>
                        </div>
                        <div class="detail-section">
                            <div class="detail-title">Body</div>
                            <div class="detail-content">${formatJson(webhook.body)}</div>
                        </div>
                    </div>
                </div>
            `).join('');
        }
        
        async function clearWebhooks() {
            if (confirm('Are you sure you want to clear all received webhooks?')) {
                try {
                    await fetch('/webhooks', { method: 'DELETE' });
                    refreshWebhooks();
                } catch (error) {
                    console.error('Error clearing webhooks:', error);
                }
            }
        }
        
        function toggleAutoRefresh() {
            autoRefresh = !autoRefresh;
            document.getElementById('autoRefreshStatus').textContent = autoRefresh ? 'ON' : 'OFF';
            
            if (autoRefresh) {
                startAutoRefresh();
            } else {
                clearInterval(refreshInterval);
            }
        }
        
        function startAutoRefresh() {
            refreshInterval = setInterval(refreshWebhooks, 5000); // Refresh every 5 seconds
        }
        
        // Initial load
        refreshWebhooks();
        
        // Start auto-refresh
        if (autoRefresh) {
            startAutoRefresh();
        }
        
        // Add click handler for auto-refresh toggle
        document.getElementById('autoRefreshStatus').onclick = toggleAutoRefresh;
        document.getElementById('autoRefreshStatus').style.cursor = 'pointer';
        document.getElementById('autoRefreshStatus').style.textDecoration = 'underline';
    </script>
</body>
</html>
""";
}

// Data model for received webhooks
public class WebhookReceived
{
    public string Id { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}
