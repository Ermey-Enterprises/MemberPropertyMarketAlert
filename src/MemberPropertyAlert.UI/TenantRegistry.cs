using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MemberPropertyAlert.UI;

public sealed class TenantRegistry
{
    private readonly List<TenantRecord> _tenants;
    private readonly object _lock = new();

    public TenantRegistry(IEnumerable<TenantRecord> seeds)
    {
        _tenants = seeds?.Select(static tenant => tenant with { }).ToList()
            ?? throw new ArgumentNullException(nameof(seeds));
    }

    public IReadOnlyList<TenantRecord> GetAll()
    {
        lock (_lock)
        {
            return _tenants
                .OrderBy(static tenant => tenant.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static tenant => tenant with { })
                .ToList();
        }
    }

    public bool TryGet(string tenantId, [NotNullWhen(true)] out TenantRecord? tenant)
    {
        if (tenantId is null)
        {
            tenant = null;
            return false;
        }

        lock (_lock)
        {
            tenant = _tenants.FirstOrDefault(t => t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
            if (tenant is null)
            {
                return false;
            }

            tenant = tenant with { };
            return true;
        }
    }

    public TenantRecord Add(TenantCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim();
        var tenantId = request.TenantId?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tenant name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID is required.", nameof(request));
        }

        if (!TenantIdRules.IsValid(tenantId))
        {
            throw new ArgumentException("Tenant ID must contain only lowercase letters, numbers, and hyphens.", nameof(request));
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? "Onboarding" : request.Status.Trim();
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            if (_tenants.Any(t => t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Tenant '{tenantId}' already exists.");
            }

            var tenant = new TenantRecord
            {
                Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name),
                TenantId = tenantId,
                Status = status,
                WebhookConfigured = request.WebhookConfigured,
                ActiveMembers = NormalizeNonNegative(request.ActiveMembers),
                RegisteredAddresses = NormalizeNonNegative(request.RegisteredAddresses),
                SsoLoginUrl = NormalizeOptional(request.SsoLoginUrl),
                CreatedAt = now,
                LastUpdated = now
            };

            _tenants.Add(tenant);
            return tenant with { };
        }
    }

    public TenantRecord? Update(string tenantId, TenantUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            var index = _tenants.FindIndex(t => t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return null;
            }

            var existing = _tenants[index];

            var updated = existing with
            {
                Name = NormalizeOptional(request.Name) ?? existing.Name,
                Status = NormalizeOptional(request.Status) ?? existing.Status,
                WebhookConfigured = request.WebhookConfigured ?? existing.WebhookConfigured,
                ActiveMembers = request.ActiveMembers is null ? existing.ActiveMembers : NormalizeNonNegative(request.ActiveMembers.Value),
                RegisteredAddresses = request.RegisteredAddresses is null ? existing.RegisteredAddresses : NormalizeNonNegative(request.RegisteredAddresses.Value),
                SsoLoginUrl = request.SsoLoginUrl is null ? existing.SsoLoginUrl : NormalizeOptional(request.SsoLoginUrl),
                LastUpdated = DateTimeOffset.UtcNow
            };

            _tenants[index] = updated;
            return updated with { };
        }
    }

    public bool Delete(string tenantId)
    {
        if (tenantId is null)
        {
            return false;
        }

        lock (_lock)
        {
            var index = _tenants.FindIndex(t => t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }

            _tenants.RemoveAt(index);
            return true;
        }
    }

    private static int NormalizeNonNegative(int? value)
    {
        if (value is null)
        {
            return 0;
        }

        return value.Value < 0 ? 0 : value.Value;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

public static class TenantIdRules
{
    public static bool IsValid(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        return tenantId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }
}

public record TenantRecord
{
    public required string Name { get; init; }
    public required string TenantId { get; init; }
    public required string Status { get; init; }
    public bool WebhookConfigured { get; init; }
    public int ActiveMembers { get; init; }
    public int RegisteredAddresses { get; init; }
    public string? SsoLoginUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}

public record TenantCreateRequest
{
    public string? Name { get; init; }
    public string? TenantId { get; init; }
    public string? Status { get; init; }
    public bool WebhookConfigured { get; init; }
    public int ActiveMembers { get; init; }
    public int RegisteredAddresses { get; init; }
    public string? SsoLoginUrl { get; init; }
}

public record TenantUpdateRequest
{
    public string? Name { get; init; }
    public string? Status { get; init; }
    public bool? WebhookConfigured { get; init; }
    public int? ActiveMembers { get; init; }
    public int? RegisteredAddresses { get; init; }
    public string? SsoLoginUrl { get; init; }
}
