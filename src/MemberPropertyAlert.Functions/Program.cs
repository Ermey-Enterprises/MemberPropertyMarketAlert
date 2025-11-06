using System;
using System.Reflection;
using Azure.Core;
using Azure.Identity;
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
using MemberPropertyAlert.Functions.Infrastructure.Messaging;
using MemberPropertyAlert.Functions.Infrastructure.Repositories;
using MemberPropertyAlert.Functions.Infrastructure.Secrets;
using MemberPropertyAlert.Functions.Infrastructure.Storage;
using MemberPropertyAlert.Functions.Infrastructure.Telemetry;
using MemberPropertyAlert.Functions.Middleware;
using MemberPropertyAlert.Functions.Security;
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
        worker.UseMiddleware<TenantAuthenticationMiddleware>();
        worker.UseMiddleware<ProblemDetailsMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        services.AddApplicationInsightsTelemetryWorkerService();

        services.Configure<RentCastOptions>(configuration.GetSection(RentCastOptions.SectionName));
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<ServiceBusOptions>(configuration.GetSection(ServiceBusOptions.SectionName));
        services.Configure<MemberAddressImportOptions>(configuration.GetSection(MemberAddressImportOptions.SectionName));
        services.Configure<TenantAuthenticationOptions>(configuration.GetSection(TenantAuthenticationOptions.SectionName));
        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));
        services.Configure<SignalROptions>(configuration.GetSection(SignalROptions.SectionName));

        services.AddSingleton<TokenCredential>(_ =>
        {
            var options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            };

            return new DefaultAzureCredential(options);
        });

        services.AddSingleton<ITenantRequestContextAccessor, TenantRequestContextAccessor>();
        services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();
    services.AddSingleton<IAuditLogger, AuditLogger>();

        services.AddSingleton(sp =>
        {
            var cosmosOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
            var secretProvider = sp.GetRequiredService<ISecretProvider>();
            var credential = sp.GetRequiredService<TokenCredential>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("CosmosClientFactory");

            var connectionString = cosmosOptions.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(cosmosOptions.ConnectionStringSecretName))
            {
                connectionString = secretProvider.GetSecretAsync(cosmosOptions.ConnectionStringSecretName).GetAwaiter().GetResult();
            }

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return new CosmosClient(connectionString);
            }

            if (!string.IsNullOrWhiteSpace(cosmosOptions.AccountEndpoint))
            {
                logger.LogInformation("Creating CosmosClient using managed identity for endpoint {Endpoint}", cosmosOptions.AccountEndpoint);
                return new CosmosClient(cosmosOptions.AccountEndpoint, credential);
            }

            throw new InvalidOperationException("Cosmos configuration requires either a connection string or account endpoint.");
        });

        services.AddSingleton<IServiceBusClientAccessor, ServiceBusClientAccessor>();

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
        services.AddSingleton<IImportStatusPublisher, ServiceBusImportStatusPublisher>();
        services.AddSingleton<IMemberAddressImportPayloadResolver, MemberAddressImportPayloadResolver>();
        services.AddHttpClient("member-address-import");
        services.AddSingleton<IAlertPublisher>(sp =>
        {
            var clientAccessor = sp.GetRequiredService<IServiceBusClientAccessor>();
            var client = clientAccessor.Client;
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
