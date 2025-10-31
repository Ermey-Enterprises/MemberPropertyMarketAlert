using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Core.Diagnostics;

public static class TelemetryExtensions
{
    public static void TrackEvent(this ILogger logger, string eventName, IReadOnlyDictionary<string, object?> properties)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        using var scope = logger.BeginScope(properties);
        logger.LogInformation("Telemetry event {@EventName}", eventName);
    }
}
