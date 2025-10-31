using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemberPropertyAlert.Functions.Security;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace MemberPropertyAlert.Functions.Infrastructure.Telemetry;

public sealed class AuditLogger : IAuditLogger
{
    private readonly ITenantRequestContextAccessor _tenantAccessor;
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        ITenantRequestContextAccessor tenantAccessor,
        TelemetryClient telemetryClient,
        ILogger<AuditLogger> logger)
    {
        _tenantAccessor = tenantAccessor;
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public Task TrackEventAsync(string actionName, IReadOnlyDictionary<string, string?>? customProperties = null, CancellationToken cancellationToken = default)
    {
        var tenantContext = _tenantAccessor.Current;

        var properties = new Dictionary<string, string?>
        {
            ["tenantId"] = tenantContext?.TenantId,
            ["institutionId"] = tenantContext?.InstitutionId,
            ["userObjectId"] = tenantContext?.ObjectId,
            ["username"] = tenantContext?.PreferredUsername,
            ["correlationId"] = tenantContext?.CorrelationId,
            ["isPlatformAdmin"] = tenantContext?.IsPlatformAdmin.ToString()?.ToLowerInvariant()
        };

        if (tenantContext?.Roles is { Count: > 0 })
        {
            properties["roles"] = string.Join(',', tenantContext.Roles);
        }

        if (customProperties is not null)
        {
            foreach (var pair in customProperties.Where(pair => !string.IsNullOrWhiteSpace(pair.Key)))
            {
                properties[pair.Key] = pair.Value;
            }
        }

        _telemetryClient.TrackEvent(actionName, properties);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            using var scope = _logger.BeginScope(properties!);
            _logger.LogInformation("Audit event {Action}", actionName);
        }

        return Task.CompletedTask;
    }
}
