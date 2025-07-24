// Member Property Alert Functions
// Version: 1.0.1 - Infrastructure deployment ready
// Last updated: 2025-06-24
// CI/CD Pipeline Test - 2025-01-24 00:02:30 UTC - Testing deployment pipeline

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Functions.Services;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using MemberPropertyAlert.Core.Application.Commands;
using MemberPropertyAlert.Core.Application.Queries;
using MemberPropertyAlert.Core.Models;
using MemberPropertyAlert.Core.Validation;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();

        // Azure Key Vault integration can be added later with additional packages
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();

        // OpenAPI/Swagger Configuration
        services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
        {
            var options = new OpenApiConfigurationOptions()
            {
                Info = new OpenApiInfo()
                {
                    Version = "1.0.0",
                    Title = "Member Property Market Alert API",
                    Description = "API for monitoring property listings and alerting financial institutions when member properties are listed for sale",
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

        // Configuration options
        services.Configure<RentCastConfiguration>(configuration.GetSection("RentCast"));
        services.Configure<CosmosConfiguration>(configuration.GetSection("CosmosDB"));
        services.Configure<NotificationConfiguration>(configuration.GetSection("Notification"));
        services.Configure<SignalRConfiguration>(configuration.GetSection("SignalR"));
        services.Configure<SchedulingConfiguration>(configuration.GetSection("Scheduling"));

        // Azure Services
        services.AddSingleton<CosmosClient>(provider =>
        {
            var cosmosConnectionString = configuration.GetConnectionString("CosmosDB") ?? 
                                       configuration["CosmosDB__ConnectionString"];
            
            if (string.IsNullOrEmpty(cosmosConnectionString))
            {
                // Return a mock client for development
                return new CosmosClient("AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
            }
            
            var options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConsistencyLevel = ConsistencyLevel.Session,
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
            };
            return new CosmosClient(cosmosConnectionString, options);
        });

        services.AddSingleton<ServiceBusClient>(provider =>
        {
            var connectionString = configuration.GetConnectionString("ServiceBus") ?? 
                                 configuration["ServiceBus__ConnectionString"];
            
            if (string.IsNullOrEmpty(connectionString))
            {
                // Return null for development - services will handle gracefully
                return null!;
            }
            
            return new ServiceBusClient(connectionString);
        });

        // HTTP Client for RentCast API
        services.AddHttpClient<IRentCastService, RentCastService>((provider, client) =>
        {
            var apiKey = configuration["RentCast__ApiKey"];
            var baseUrl = configuration["RentCast__BaseUrl"] ?? "https://api.rentcast.io/v1";
            var timeout = int.Parse(configuration["RentCast__TimeoutSeconds"] ?? "30");
            
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeout);
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
            
            client.DefaultRequestHeaders.Add("User-Agent", "MemberPropertyAlert/1.0");
        });

        // Core Services
        services.AddScoped<ICosmosService, CosmosService>();
        services.AddScoped<IRentCastService, RentCastService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISignalRService, SignalRService>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<IPropertyScanService, PropertyScanService>();

        // CQRS Command Handlers
        services.AddScoped<ICommandHandler<CreateInstitutionCommand, Institution>, CreateInstitutionCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateInstitutionCommand, Institution>, UpdateInstitutionCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteInstitutionCommand>, DeleteInstitutionCommandHandler>();

        // CQRS Query Handlers
        services.AddScoped<IQueryHandler<GetAllInstitutionsQuery, IEnumerable<Institution>>, GetAllInstitutionsQueryHandler>();
        services.AddScoped<IQueryHandler<GetInstitutionByIdQuery, Institution>, GetInstitutionByIdQueryHandler>();

        // Validators
        services.AddScoped<IValidator<CreateInstitutionCommand>, CreateInstitutionCommandValidator>();
        services.AddScoped<IValidator<UpdateInstitutionCommand>, UpdateInstitutionCommandValidator>();

        // Background Services
        services.AddHostedService<ScheduledScanService>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddApplicationInsights();
        });
    })
    .Build();

// Initialize Cosmos DB containers on startup
try
{
    using var scope = host.Services.CreateScope();
    var cosmosService = scope.ServiceProvider.GetRequiredService<ICosmosService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    await cosmosService.InitializeDatabaseAsync();
    logger.LogInformation("Cosmos DB initialized successfully");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Failed to initialize Cosmos DB - continuing with startup");
}

host.Run();
