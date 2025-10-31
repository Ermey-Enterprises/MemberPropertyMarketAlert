using Azure.Messaging.ServiceBus;

namespace MemberPropertyAlert.Functions.Infrastructure.Integrations;

public interface IServiceBusClientAccessor
{
    ServiceBusClient? Client { get; }
}
