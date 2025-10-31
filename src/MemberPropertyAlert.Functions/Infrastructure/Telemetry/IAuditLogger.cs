using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MemberPropertyAlert.Functions.Infrastructure.Telemetry;

public interface IAuditLogger
{
    Task TrackEventAsync(string actionName, IReadOnlyDictionary<string, string?>? customProperties = null, CancellationToken cancellationToken = default);
}
