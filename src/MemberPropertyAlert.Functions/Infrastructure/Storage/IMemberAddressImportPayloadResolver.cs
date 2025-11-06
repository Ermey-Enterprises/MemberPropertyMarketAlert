using MemberPropertyAlert.Functions.Models;

namespace MemberPropertyAlert.Functions.Infrastructure.Storage;

public interface IMemberAddressImportPayloadResolver
{
    Task<Stream> OpenAsync(MemberAddressImportMessage message, CancellationToken cancellationToken);
}
