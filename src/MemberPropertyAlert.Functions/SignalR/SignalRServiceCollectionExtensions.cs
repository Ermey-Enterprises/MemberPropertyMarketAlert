using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPropertyAlert.Functions.SignalR;

public static class SignalRServiceCollectionExtensions
{
    public static IServiceCollection AddSignalRServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Placeholder for future SignalR service registration.
        _ = configuration;
        return services;
    }
}
