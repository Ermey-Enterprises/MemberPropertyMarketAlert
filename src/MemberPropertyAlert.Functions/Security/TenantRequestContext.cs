using System.Collections.Generic;
using System.Security.Claims;

namespace MemberPropertyAlert.Functions.Security;

public sealed record TenantRequestContext(
    ClaimsPrincipal Principal,
    string TenantId,
    string? InstitutionId,
    bool IsPlatformAdmin,
    string? ObjectId,
    string? PreferredUsername,
    string CorrelationId,
    IReadOnlyCollection<string> Roles);
