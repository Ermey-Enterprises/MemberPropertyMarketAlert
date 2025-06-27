// Member Property Alert UI
// Version: 2.0.0 - Fixed SignalR and deployment issues
// Last updated: 2025-06-25

using Microsoft.Extensions.FileProviders;
using MemberPropertyAlert.UI.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Add SignalR services
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Register SignalR hub service
builder.Services.AddScoped<ILogHubService, LogHubService>();

// Add CORS for SignalR (needed for some deployment scenarios)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Add global exception handling
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred");
        
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("An error occurred while processing your request.");
        }
    }
});

// Enable CORS
app.UseCors();

// Configure HTTPS redirection (but be flexible for Azure)
if (!app.Environment.IsProduction() || 
    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"))) // Azure App Service
{
    app.UseHttpsRedirection();
}

// Configure static files to serve React build files
var buildPath = Path.Combine(app.Environment.ContentRootPath, "build");
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");

// Primary static files from React build directory
if (Directory.Exists(buildPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(buildPath),
        RequestPath = "",
        OnPrepareResponse = ctx =>
        {
            // Cache static assets for 1 year, but not index.html
            if (!ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
            }
        }
    });
}

// Fallback to wwwroot for any additional static files
if (Directory.Exists(wwwrootPath))
{
    app.UseStaticFiles();
}

app.UseRouting();

// Add health check endpoint
app.MapHealthChecks("/health");

// Map SignalR hub
app.MapHub<LogHub>("/api/loghub");

// Map API controllers
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action=Index}/{id?}");

// Serve React app for all non-API routes
app.MapFallback(async context =>
{
    var indexPath = Path.Combine(buildPath, "index.html");
    
    if (File.Exists(indexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(indexPath);
    }
    else
    {
        // Fallback error page if React build is missing
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(@"
<!DOCTYPE html>
<html>
<head>
    <title>Application Error</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .error { color: #d32f2f; }
        .info { color: #1976d2; margin-top: 20px; }
    </style>
</head>
<body>
    <h1 class='error'>Application Configuration Error</h1>
    <p>The React application build files are missing.</p>
    <div class='info'>
        <h3>For Developers:</h3>
        <ul>
            <li>Run <code>npm run build</code> in the src directory</li>
            <li>Ensure the build directory exists and contains index.html</li>
            <li>Check the deployment process includes React build files</li>
        </ul>
    </div>
</body>
</html>");
    }
});

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Member Property Alert UI starting...");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Build path: {BuildPath} (Exists: {BuildExists})", buildPath, Directory.Exists(buildPath));

if (Directory.Exists(buildPath))
{
    var indexExists = File.Exists(Path.Combine(buildPath, "index.html"));
    logger.LogInformation("Index.html exists: {IndexExists}", indexExists);
}

app.Run();
