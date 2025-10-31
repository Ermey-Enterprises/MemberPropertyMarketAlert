using System;
using System.Reflection;
using Azure.Messaging.ServiceBus;
using MemberPropertyAlert.Core.Abstractions.Integrations;
using MemberPropertyAlert.Core.Abstractions.Messaging;
using MemberPropertyAlert.Core.Abstractions.Repositories;
using MemberPropertyAlert.Core.Abstractions.Services;
using MemberPropertyAlert.Core.Options;
using MemberPropertyAlert.Core.Services;
using MemberPropertyAlert.Functions.Configuration;
using MemberPropertyAlert.Functions.Extensions;
using MemberPropertyAlert.Functions.Infrastructure.Integrations;
using MemberPropertyAlert.Functions.Infrastructure.Repositories;
using MemberPropertyAlert.Functions.Middleware;
using MemberPropertyAlert.Functions.SignalR;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
              .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        worker.UseMiddleware<ProblemDetailsMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        services.AddApplicationInsightsTelemetryWorkerService();

        services.Configure<RentCastOptions>(configuration.GetSection(RentCastOptions.SectionName));
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
        services.Configure<SignalROptions>(configuration.GetSection(SignalROptions.SectionName));

        services.AddSingleton(sp =>
        {
            var cosmosOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
            if (string.IsNullOrWhiteSpace(cosmosOptions.ConnectionString))
            {
                throw new InvalidOperationException("Cosmos connection string is not configured. Set Cosmos:ConnectionString in local.settings.json.");
            }

            return new CosmosClient(cosmosOptions.ConnectionString);
        });

        var serviceBusConnection = configuration.GetConnectionString("ServiceBus") ?? configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(serviceBusConnection))
        {
            services.AddSingleton(new ServiceBusClient(serviceBusConnection));
        }

        services.AddSingleton<ICosmosContainerFactory, CosmosContainerFactory>();
        services.AddScoped<IInstitutionRepository, CosmosInstitutionRepository>();
        services.AddScoped<IMemberAddressRepository, CosmosMemberAddressRepository>();
        services.AddScoped<IScanJobRepository, CosmosScanJobRepository>();
        services.AddScoped<IListingMatchRepository, CosmosListingMatchRepository>();
        services.AddScoped<IScanScheduleRepository, CosmosScanScheduleRepository>();

        services.AddScoped<IInstitutionService, InstitutionService>();
        services.AddScoped<IListingMatchService, ListingMatchService>();
        services.AddScoped<IScanOrchestrator, ScanOrchestrator>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IMetricsService, MetricsService>();

        services.AddSingleton<ILogStreamPublisher, SignalRLogStreamPublisher>();
        services.AddSingleton<IAlertPublisher>(sp =>
        {
            var client = sp.GetService<ServiceBusClient>();
            var webhookClient = sp.GetRequiredService<IWebhookClient>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>();
            var logger = sp.GetRequiredService<ILogger<ServiceBusAlertPublisher>>();
            return new ServiceBusAlertPublisher(client, webhookClient, options, logger);
        });
        services.AddHttpClient<IRentCastClient, RentCastClient>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        services.AddHttpClient<IWebhookClient, WebhookClient>()
            .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(200 * retryAttempt)));

        services.AddSignalRServices(configuration);
    })
    .ConfigureLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddConsole();
    })
    .Build();

await host.RunAsync();
