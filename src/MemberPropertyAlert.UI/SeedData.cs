using System.Globalization;
using System.Linq;

namespace MemberPropertyAlert.UI;

public static class SeedData
{
    public static TenantAlert[] CreateTenantAlerts()
    {
        return new[]
        {
            new TenantAlert(
                Institution: "Northwind University",
                Address: "123 Market Street, Chicago, IL",
                Status: "Active",
                MonthlyRent: 2450m,
                LastChecked: DateTimeOffset.UtcNow.AddHours(-2)),
            new TenantAlert(
                Institution: "Contoso Technical College",
                Address: "88 Innovation Way, Austin, TX",
                Status: "Pending",
                MonthlyRent: 1825m,
                LastChecked: DateTimeOffset.UtcNow.AddHours(-5)),
            new TenantAlert(
                Institution: "Fabrikam Law School",
                Address: "501 Lakeview Ave, Seattle, WA",
                Status: "Resolved",
                MonthlyRent: 2725m,
                LastChecked: DateTimeOffset.UtcNow.AddDays(-1))
        };
    }

    public static TenantRecord[] CreateTenants()
    {
        var now = DateTimeOffset.UtcNow;

        return new[]
        {
            new TenantRecord
            {
                Name = "Northwind University",
                TenantId = "northwind-university",
                Status = "Active",
                WebhookConfigured = true,
                ActiveMembers = 128,
                RegisteredAddresses = 342,
                SsoLoginUrl = "https://northwind.edu/sso",
                CreatedAt = now.AddMonths(-11),
                LastUpdated = now.AddHours(-6)
            },
            new TenantRecord
            {
                Name = "Contoso Technical College",
                TenantId = "contoso-technical",
                Status = "Onboarding",
                WebhookConfigured = false,
                ActiveMembers = 54,
                RegisteredAddresses = 97,
                SsoLoginUrl = "https://contoso.tech/login",
                CreatedAt = now.AddMonths(-2),
                LastUpdated = now.AddDays(-1)
            },
            new TenantRecord
            {
                Name = "Fabrikam Law School",
                TenantId = "fabrikam-law",
                Status = "Maintenance",
                WebhookConfigured = true,
                ActiveMembers = 35,
                RegisteredAddresses = 61,
                SsoLoginUrl = null,
                CreatedAt = now.AddMonths(-6),
                LastUpdated = now.AddHours(-12)
            }
        };
    }

    public static InstitutionSummary[] CreateInstitutionSummaries()
    {
        return CreateTenants()
            .Select(tenant => new InstitutionSummary(
                Name: tenant.Name,
                TenantId: tenant.TenantId,
                ActiveMembers: tenant.ActiveMembers,
                RegisteredAddresses: tenant.RegisteredAddresses,
                WebhookConfigured: tenant.WebhookConfigured))
            .ToArray();
    }
}

public record TenantAlert(string Institution, string Address, string Status, decimal MonthlyRent, DateTimeOffset LastChecked)
{
    public string MonthlyRentDisplay => MonthlyRent.ToString("C0", CultureInfo.CurrentCulture);
    public string LastCheckedDisplay => LastChecked.ToString("g");
}

public record InstitutionSummary(string Name, string TenantId, int ActiveMembers, int RegisteredAddresses, bool WebhookConfigured);
