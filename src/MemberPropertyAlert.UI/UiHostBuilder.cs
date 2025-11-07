using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;

namespace MemberPropertyAlert.UI;

public static class UiHostBuilder
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(UiHostBuilder).Assembly.Location) ?? AppContext.BaseDirectory;
        var directoryProbe = new DirectoryInfo(assemblyDirectory);
        DirectoryInfo? solutionRoot = null;

        while (directoryProbe is not null)
        {
            if (File.Exists(Path.Combine(directoryProbe.FullName, "MemberPropertyMarketAlert.sln")))
            {
                solutionRoot = directoryProbe;
                break;
            }

            directoryProbe = directoryProbe.Parent;
        }

        string contentRootPath = assemblyDirectory;

        if (solutionRoot is not null)
        {
            contentRootPath = Path.Combine(solutionRoot.FullName, "src", "MemberPropertyAlert.UI");
        }

        var webRootPath = Path.Combine(contentRootPath, "wwwroot");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = contentRootPath,
            WebRootPath = webRootPath
        });

        var tenantHtml = Path.Combine(webRootPath, "tenant.html");
        var adminHtml = Path.Combine(webRootPath, "admin.html");
        if (!File.Exists(tenantHtml) || !File.Exists(adminHtml))
        {
            throw new InvalidOperationException($"Static assets missing. Expected tenant.html and admin.html under '{webRootPath}'.");
        }
        configureBuilder?.Invoke(builder);

        var tenantAlerts = SeedData.CreateTenantAlerts();
        var tenantSeeds = SeedData.CreateTenants();
        var institutions = tenantSeeds
            .Select(tenant => new InstitutionSummary(
                tenant.Name,
                tenant.TenantId,
                tenant.ActiveMembers,
                tenant.RegisteredAddresses,
                tenant.WebhookConfigured))
            .ToArray();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddSingleton(tenantAlerts);
        builder.Services.AddSingleton(institutions);
        builder.Services.AddSingleton(new TenantRegistry(tenantSeeds));

        var app = builder.Build();

    app.UseDefaultFiles();
    app.UseStaticFiles();

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/tenant/alerts", (TenantAlert[] alerts) => alerts);
        app.MapGet("/api/admin/institutions", (InstitutionSummary[] summaries) => summaries);
        app.MapGet("/api/admin/tenants", (TenantRegistry registry) =>
        {
            var tenants = registry.GetAll();
            Console.WriteLine($"[ui-server] returning {tenants.Count} tenants");
            return Results.Ok(tenants);
        });
        app.MapGet("/api/admin/tenants/{tenantId}", (TenantRegistry registry, string tenantId) =>
        {
            return registry.TryGet(tenantId, out var tenant)
                ? Results.Ok(tenant)
                : Results.NotFound();
        });

        app.MapPost("/api/admin/tenants", (TenantRegistry registry, TenantCreateRequest request) =>
        {
            try
            {
                var created = registry.Add(request);
                return Results.Created($"/api/admin/tenants/{created.TenantId}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        app.MapPut("/api/admin/tenants/{tenantId}", (TenantRegistry registry, string tenantId, TenantUpdateRequest request) =>
        {
            var updated = registry.Update(tenantId, request);
            return updated is not null ? Results.Ok(updated) : Results.NotFound();
        });

        app.MapDelete("/api/admin/tenants/{tenantId}", (TenantRegistry registry, string tenantId) =>
        {
            return registry.Delete(tenantId) ? Results.NoContent() : Results.NotFound();
        });

        app.MapFallbackToFile("/admin", "admin.html");
        app.MapFallbackToFile("/tenant", "tenant.html");
        app.MapFallbackToFile("index.html");

        return app;
    }
}
