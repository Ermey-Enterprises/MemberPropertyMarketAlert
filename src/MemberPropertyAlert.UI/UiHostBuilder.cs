using System.IO;
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
        var institutions = SeedData.CreateInstitutionSummaries();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddSingleton(tenantAlerts);
        builder.Services.AddSingleton(institutions);

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/tenant/alerts", (TenantAlert[] alerts) => alerts);
        app.MapGet("/api/admin/institutions", (InstitutionSummary[] summaries) => summaries);
        app.MapFallbackToFile("index.html");

        return app;
    }
}
