using System;

namespace MemberPropertyAlert.Core.Domain.ValueObjects;

public sealed record TenantInstitutionScope
{
    public string TenantId { get; }
    public string InstitutionId { get; }

    public TenantInstitutionScope(string tenantId, string institutionId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant id must be provided.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(institutionId))
        {
            throw new ArgumentException("Institution id must be provided.", nameof(institutionId));
        }

        TenantId = tenantId.Trim();
        InstitutionId = institutionId.Trim();
    }
}
