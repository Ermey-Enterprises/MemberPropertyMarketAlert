using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MemberPropertyMarketAlert.Core.Services;

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

        // Add Cosmos DB
        services.AddSingleton(serviceProvider =>
        {
            var connectionString = configuration.GetConnectionString("CosmosDb");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("CosmosDb connection string is not configured");
            }
            return new CosmosClient(connectionString);
        });

        // Add HTTP client
        services.AddHttpClient();

        // Add application services
        services.AddSingleton<ICosmosDbService, InMemoryCosmosDbService>();
        services.AddScoped<IPropertyMatchingService, PropertyMatchingService>();

        // Add Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
