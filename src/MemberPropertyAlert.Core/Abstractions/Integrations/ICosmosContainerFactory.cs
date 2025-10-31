using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using MemberPropertyAlert.Core.Options;

namespace MemberPropertyAlert.Core.Abstractions.Integrations;

public interface ICosmosContainerFactory
{
    Task<Container> GetInstitutionsContainerAsync(CancellationToken cancellationToken = default);
    Task<Container> GetAddressesContainerAsync(CancellationToken cancellationToken = default);
    Task<Container> GetScansContainerAsync(CancellationToken cancellationToken = default);
    Task<Container> GetAlertsContainerAsync(CancellationToken cancellationToken = default);
    CosmosOptions Options { get; }
}
