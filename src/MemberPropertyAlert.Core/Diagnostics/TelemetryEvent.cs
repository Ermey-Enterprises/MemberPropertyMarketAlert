using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Diagnostics;

public sealed record TelemetryEvent(
    string Name,
    IReadOnlyDictionary<string, object?> Properties
);
