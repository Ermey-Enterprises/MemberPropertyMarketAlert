using System;

namespace MemberPropertyAlert.Functions.Configuration;

public sealed class TenantAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Authority { get; set; } = string.Empty;
    public string[] Audiences { get; set; } = Array.Empty<string>();
    public string[]? AllowedTenants { get; set; }
    public string TenantIdClaim { get; set; } = "tid";
    public string? InstitutionIdClaim { get; set; } = "extension_institutionId";
    public string ObjectIdClaim { get; set; } = "oid";
    public string RoleClaimType { get; set; } = "roles";
    public string[] PlatformAdminRoles { get; set; } = Array.Empty<string>();
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool EnforceInstitutionClaim { get; set; }
    public string? PreferredUsernameClaim { get; set; } = "preferred_username";
}
