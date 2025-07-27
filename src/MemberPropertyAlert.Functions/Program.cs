using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MemberPropertyAlert.Functions.Services;
using MemberPropertyAlert.Core.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Basic logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Configure stub configuration classes
        services.Configure<NotificationConfiguration>(options => { });
        services.Configure<SignalRConfiguration>(options => { });
        services.Configure<SchedulingConfiguration>(options => { });

        // Register stub services for ScanController
        services.AddScoped<ICosmosService, CosmosService>();
        services.AddScoped<IPropertyScanService, PropertyScanService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<ISignalRService, SignalRService>();

        // HTTP Client for SignalR service
        services.AddHttpClient<SignalRService>();
        
        // RentCast Service with basic HTTP client
        services.AddHttpClient<EnhancedRentCastService>(client =>
        {
            client.BaseAddress = new Uri("https://api.rentcast.io");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "MemberPropertyAlert/1.0.2");
        });
        services.AddScoped<IRentCastService, EnhancedRentCastService>();
    })
    .Build();

host.Run();
