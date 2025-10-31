using System.Threading;
using System.Threading.Tasks;

namespace MemberPropertyAlert.Functions.Infrastructure.Secrets;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
