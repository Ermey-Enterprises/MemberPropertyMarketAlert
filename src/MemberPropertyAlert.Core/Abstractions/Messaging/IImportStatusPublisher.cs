using MemberPropertyAlert.Core.Models;

namespace MemberPropertyAlert.Core.Abstractions.Messaging;

public interface IImportStatusPublisher
{
    Task PublishAsync(MemberAddressImportStatusEvent statusEvent, CancellationToken cancellationToken = default);
}
