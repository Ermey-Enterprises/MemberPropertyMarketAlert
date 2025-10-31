using System.Globalization;

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

    public static InstitutionSummary[] CreateInstitutionSummaries()
    {
        return new[]
        {
            new InstitutionSummary(
                Name: "Northwind University",
                TenantId: "northwind-university",
                ActiveMembers: 128,
                RegisteredAddresses: 342,
                WebhookConfigured: true),
            new InstitutionSummary(
                Name: "Contoso Technical College",
                TenantId: "contoso-technical",
                ActiveMembers: 54,
                RegisteredAddresses: 97,
                WebhookConfigured: false),
            new InstitutionSummary(
                Name: "Fabrikam Law School",
                TenantId: "fabrikam-law",
                ActiveMembers: 35,
                RegisteredAddresses: 61,
                WebhookConfigured: true)
        };
    }
}

public record TenantAlert(string Institution, string Address, string Status, decimal MonthlyRent, DateTimeOffset LastChecked)
{
    public string MonthlyRentDisplay => MonthlyRent.ToString("C0", CultureInfo.CurrentCulture);
    public string LastCheckedDisplay => LastChecked.ToString("g");
}

public record InstitutionSummary(string Name, string TenantId, int ActiveMembers, int RegisteredAddresses, bool WebhookConfigured);
