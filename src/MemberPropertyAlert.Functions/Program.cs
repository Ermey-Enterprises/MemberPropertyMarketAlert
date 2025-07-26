// Member Property Alert Functions
// Version: 1.0.2 - Enhanced with MockRentCastAPI Integration
// Last updated: 2025-01-26
// Integrated with external MockRentCastAPI with resilience patterns

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;
using System.Net;
using MemberPropertyAlert.Functions.Services;
using MemberPropertyAlert.Functions.Configuration;
using MemberPropertyAlert.Core.Services;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Configuration Options
        services.Configure<ExtendedRentCastConfiguration>(
            configuration.GetSection("RentCast"));
        services.Configure<CircuitBreakerConfiguration>(
            configuration.GetSection("CircuitBreaker"));
        services.Configure<RetryPolicyConfiguration>(
            configuration.GetSection("RetryPolicy"));
        services.Configure<HealthCheckConfiguration>(
            configuration.GetSection("HealthCheck"));

        // Memory Cache
        services.AddMemoryCache();

        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();

        // SignalR Service
        services.AddSignalR();
        
        // HTTP Client for internal SignalR calls
        services.AddHttpClient<SignalRService>();

        // Resilience Policies
        ConfigureResiliencePolicies(services, configuration);

        // RentCast Service with HTTP Client
        ConfigureRentCastService(services, configuration);

        // Health Checks
        ConfigureHealthChecks(services, configuration);

        // OpenAPI/Swagger Configuration
        services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
        {
            var options = new OpenApiConfigurationOptions()
            {
                Info = new OpenApiInfo()
                {
                    Version = "1.0.2",
                    Title = "Member Property Market Alert API",
                    Description = "API for monitoring property listings and alerting financial institutions when member properties are listed for sale. Integrated with MockRentCastAPI for development and testing.",
                    Contact = new OpenApiContact()
                    {
                        Name = "Member Property Alert Support",
                        Email = "support@memberpropertyalert.com"
                    }
                },
                Servers = DefaultOpenApiConfigurationOptions.GetHostNames(),
                OpenApiVersion = OpenApiVersionType.V3,
                IncludeRequestingHostName = true,
                ForceHttps = false,
                ForceHttp = false,
            };
            return options;
        });

        // Enhanced Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddApplicationInsights();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

host.Run();

static void ConfigureResiliencePolicies(IServiceCollection services, IConfiguration configuration)
{
    var retryConfig = configuration.GetSection("RetryPolicy").Get<RetryPolicyConfiguration>() 
        ?? new RetryPolicyConfiguration();
    var circuitBreakerConfig = configuration.GetSection("CircuitBreaker").Get<CircuitBreakerConfiguration>() 
        ?? new CircuitBreakerConfiguration();

    // Retry Policy
    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && 
            (r.StatusCode == HttpStatusCode.RequestTimeout ||
             r.StatusCode == HttpStatusCode.TooManyRequests ||
             r.StatusCode >= HttpStatusCode.InternalServerError))
        .WaitAndRetryAsync(
            retryConfig.MaxRetryAttempts,
            retryAttempt => TimeSpan.FromMilliseconds(
                Math.Min(
                    retryConfig.BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1),
                    retryConfig.MaxDelay.TotalMilliseconds)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                // Log retry attempt - simplified for Azure Functions
                Console.WriteLine($"Retry {retryCount} in {timespan.TotalMilliseconds}ms. Reason: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
            });

    // Circuit Breaker Policy
    var circuitBreakerPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && r.StatusCode >= HttpStatusCode.InternalServerError)
        .CircuitBreakerAsync(
            circuitBreakerConfig.FailureThreshold,
            circuitBreakerConfig.DurationOfBreak,
            onBreak: (exception, duration) =>
            {
                // Log circuit breaker opening
            },
            onReset: () =>
            {
                // Log circuit breaker closing
            });

    services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(provider =>
        Policy.WrapAsync(retryPolicy, circuitBreakerPolicy));
}

static void ConfigureRentCastService(IServiceCollection services, IConfiguration configuration)
{
    var rentCastConfig = configuration.GetSection("RentCast").Get<ExtendedRentCastConfiguration>() 
        ?? new ExtendedRentCastConfiguration();

    // HTTP Client for RentCast API
    services.AddHttpClient<EnhancedRentCastService>(client =>
    {
        client.BaseAddress = new Uri(rentCastConfig.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(rentCastConfig.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("X-API-Key", rentCastConfig.ApiKey);
        client.DefaultRequestHeaders.Add("User-Agent", "MemberPropertyAlert/1.0.2");
    });

    // Register the service
    services.AddScoped<IRentCastService, EnhancedRentCastService>();
}

static void ConfigureHealthChecks(IServiceCollection services, IConfiguration configuration)
{
    var healthCheckBuilder = services.AddHealthChecks();

    // Add MockRentCastAPI health check
    var healthConfig = configuration.GetSection("HealthCheck").Get<HealthCheckConfiguration>();
    if (healthConfig?.MockRentCastApi?.Enabled == true)
    {
        services.AddHttpClient<MockRentCastApiHealthCheckService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(healthConfig.MockRentCastApi.TimeoutSeconds);
        });

        healthCheckBuilder.AddCheck<MockRentCastApiHealthCheckService>(
            "mockrentcastapi",
            HealthStatus.Degraded,
            new[] { "external", "api", "rentcast" });
    }

    // Add memory health check
    healthCheckBuilder.AddCheck("memory", () =>
    {
        var allocatedBytes = GC.GetTotalMemory(false);
        var data = new Dictionary<string, object>
        {
            ["allocated_bytes"] = allocatedBytes,
            ["allocated_mb"] = allocatedBytes / 1024 / 1024
        };

        var status = allocatedBytes > 100 * 1024 * 1024 // 100 MB threshold
            ? HealthStatus.Degraded
            : HealthStatus.Healthy;

        return HealthCheckResult.Healthy("Memory usage is within acceptable limits", data);
    }, new[] { "memory" });
}
