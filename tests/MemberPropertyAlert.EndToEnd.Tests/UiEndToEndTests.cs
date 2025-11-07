using System;
using System.IO;
using System.Net;
using System.Net.Http;
using MemberPropertyAlert.UI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Xunit;

namespace MemberPropertyAlert.EndToEnd.Tests;

public sealed class UiServerFixture : IAsyncLifetime
{
    private IHost? _host;

    public string BaseAddress { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _host = UiHostBuilder.Build(Array.Empty<string>(), builder =>
        {
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        });

        await _host.StartAsync();

        var server = _host.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        BaseAddress = addresses?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("UI host did not expose a listen address.");

        if (BaseAddress.EndsWith('/'))
        {
            BaseAddress = BaseAddress.TrimEnd('/');
        }

        Console.WriteLine($"[ui-server] listening at {BaseAddress}");

    var environment = _host.Services.GetRequiredService<IWebHostEnvironment>();
    var tenantPath = Path.Combine(environment.WebRootPath ?? string.Empty, "tenant.html");
    var adminPath = Path.Combine(environment.WebRootPath ?? string.Empty, "admin.html");

    Console.WriteLine($"[ui-server] content root: {environment.ContentRootPath}");
    Console.WriteLine($"[ui-server] web root: {environment.WebRootPath}");
    Console.WriteLine($"[ui-server] tenant.html exists: {File.Exists(tenantPath)}");
    Console.WriteLine($"[ui-server] admin.html exists: {File.Exists(adminPath)}");

        if (!File.Exists(tenantPath) || !File.Exists(adminPath))
        {
            Console.WriteLine($"[ui-server] tenant.html exists: {File.Exists(tenantPath)}");
            Console.WriteLine($"[ui-server] admin.html exists: {File.Exists(adminPath)}");
            Console.WriteLine($"[ui-server] ContentRoot: {environment.ContentRootPath}");
            Console.WriteLine($"[ui-server] WebRoot: {environment.WebRootPath}");
        }

    using var probeClient = new HttpClient { BaseAddress = new Uri(BaseAddress) };
    var probeResponse = await probeClient.GetAsync("/tenant.html");
    Console.WriteLine($"[ui-server] tenant.html status {(int)probeResponse.StatusCode}");
    }

    public async Task DisposeAsync()
    {
        if (_host is null)
        {
            return;
        }

        await _host.StopAsync();
        _host.Dispose();
    }
}

public sealed class GenericBrowserFixture : IAsyncLifetime
{
    private readonly bool _headless;
    private readonly string? _channelOverride;
    private readonly int? _slowMoDelay;

    public GenericBrowserFixture()
    {
        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

        if (isCi)
        {
            _headless = true;
        }
        else
        {
            var headlessOverride = ParseBoolean(Environment.GetEnvironmentVariable("UI_TEST_HEADLESS"));
            var headfulOverride = ParseBoolean(Environment.GetEnvironmentVariable("UI_TEST_HEADFUL"));

            if (headlessOverride.HasValue)
            {
                _headless = headlessOverride.Value;
            }
            else if (headfulOverride.HasValue)
            {
                _headless = !headfulOverride.Value;
            }
            else
            {
                _headless = false; // Default to headful locally so interactions stay visible.
            }
        }

        _channelOverride = Environment.GetEnvironmentVariable("UI_TEST_BROWSER_CHANNEL");

        if (!isCi)
        {
            var slowMoEnv = Environment.GetEnvironmentVariable("UI_TEST_SLOWMO");
            if (int.TryParse(slowMoEnv, out var parsed) && parsed > 0)
            {
                _slowMoDelay = parsed;
            }
            else
            {
                _slowMoDelay = 200; // Slow the pointer noticeably for local observation.
            }
        }

    var diagnostics = $"Headless={_headless};SlowMo={_slowMoDelay ?? 0};Channel={_channelOverride ?? "chromium"};CI={isCi}";
    Console.WriteLine($"[ui-tests] {diagnostics}");
    Console.Out.Flush();

    var diagnosticsFile = Path.Combine(Path.GetTempPath(), "ui-test-launch.txt");
    File.WriteAllText(diagnosticsFile, diagnostics);
    }

    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;
    public bool IsHeadless => _headless;

    public async Task InitializeAsync()
    {
        EnsureBrowsersInstalled();

        Console.WriteLine("[ui-tests] Launching Chromium...");
        Console.Out.Flush();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _headless,
            Channel = string.IsNullOrWhiteSpace(_channelOverride) ? null : _channelOverride,
            SlowMo = _slowMoDelay
        });

        Console.WriteLine("[ui-tests] Chromium launch complete");
        Console.Out.Flush();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        Playwright?.Dispose();
    }

    public async Task<IBrowserContext> CreateContextAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });
        if (!_headless)
        {
            context.SetDefaultTimeout(60000);
        }
        return context;
    }

    private static bool _installed;

    private static void EnsureBrowsersInstalled()
    {
        if (_installed)
        {
            return;
        }

        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        _installed = true;
    }

    private static bool? ParseBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}

internal static class UiTestDiagnostics
{
    public static void Attach(IPage page)
    {
        page.Console += (_, message) => Console.WriteLine($"[browser:{message.Type}] {message.Text}");
        page.RequestFailed += (_, request) => Console.WriteLine($"[request-failed] {request.Method} {request.Url} :: {request.Failure}");
        page.PageError += (_, error) => Console.WriteLine($"[page-error] {error}");
    }
}

internal static class UiTestExperience
{
    private static readonly bool IsCi = string.Equals(
        Environment.GetEnvironmentVariable("CI"),
        "true",
        StringComparison.OrdinalIgnoreCase);

    private static readonly bool _pause = !IsCi && string.Equals(
        Environment.GetEnvironmentVariable("UI_TEST_PAUSE"),
        "1",
        StringComparison.OrdinalIgnoreCase);

    private static readonly int? _observeDelay = !IsCi ? ResolveObserveDelay() : null;

    public static Task ObserveAsync(IPage page)
    {
        if (page is null)
        {
            return Task.CompletedTask;
        }

        if (_pause)
        {
            // Launches the Playwright inspector so the user can step through interactions.
            return page.PauseAsync();
        }

        if (_observeDelay is int delay)
        {
            return page.WaitForTimeoutAsync(delay);
        }

        return Task.CompletedTask;
    }

    private static int? ResolveObserveDelay()
    {
        var value = Environment.GetEnvironmentVariable("UI_TEST_OBSERVE_MS");
        if (int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return 8000; // Default linger so local runs are clearly visible.
    }
}

public class UiEndToEndTests : IClassFixture<UiServerFixture>, IClassFixture<GenericBrowserFixture>
{
    private readonly UiServerFixture _server;
    private readonly GenericBrowserFixture _browser;

    public UiEndToEndTests(UiServerFixture server, GenericBrowserFixture browser)
    {
        _server = server;
        _browser = browser;
    }

    [Fact]
    public async Task TenantDashboard_AllowsFilteringAndRefresh()
    {
        await using var context = await _browser.CreateContextAsync();
        var page = await context.NewPageAsync();
        if (!_browser.IsHeadless)
        {
            await page.BringToFrontAsync();
        }
        UiTestDiagnostics.Attach(page);

    var tenantResponse = await page.GotoAsync($"{_server.BaseAddress}/tenant", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
    Assert.True(tenantResponse?.Ok, $"Tenant page load failed: {tenantResponse?.Status} {tenantResponse?.Url}");

        await page.WaitForSelectorAsync("[data-testid='alert-card']");
        var initialCount = await page.Locator("[data-testid='result-count']").InnerTextAsync();
        Assert.Contains("3", initialCount, StringComparison.OrdinalIgnoreCase);

        await page.SelectOptionAsync("[data-testid='status-filter']", "pending");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'alert-card\\']:not([hidden])').length === 1");

        var pendingInstitution = await page.Locator("[data-testid='alert-card']:not([hidden]) [data-testid='alert-institution']").InnerTextAsync();
        Assert.Contains("Contoso Technical College", pendingInstitution, StringComparison.OrdinalIgnoreCase);

        await page.SelectOptionAsync("[data-testid='status-filter']", "resolved");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'alert-card\\']:not([hidden])').length === 1");

        var emptyStateVisible = await page.Locator("[data-testid='empty-state']").IsVisibleAsync();
        Assert.False(emptyStateVisible);

        await page.SelectOptionAsync("[data-testid='status-filter']", "all");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'alert-card\\']:not([hidden])').length === 3");

        await page.ClickAsync("[data-testid='refresh-alerts']");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=\\'refresh-alerts\\']').dataset.loading === 'true'");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\\'refresh-alerts\\']').dataset.loading");

        var healthText = await page.Locator("[data-testid='tenant-health']").InnerTextAsync();
        Assert.Equal("Connected", healthText);

        await UiTestExperience.ObserveAsync(page);
    }

    [Fact]
    public async Task AdminConsole_SupportsTenantLifecycle()
    {
        await using var context = await _browser.CreateContextAsync();
        var page = await context.NewPageAsync();
        if (!_browser.IsHeadless)
        {
            await page.BringToFrontAsync();
        }
        UiTestDiagnostics.Attach(page);
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

    var adminResponse = await page.GotoAsync($"{_server.BaseAddress}/admin", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
    Assert.True(adminResponse?.Ok, $"Admin page load failed: {adminResponse?.Status} {adminResponse?.Url}");

        await page.WaitForFunctionAsync("() => window.mpaAdmin && window.mpaAdmin.ready === true");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'tenant-row\\']').length === 3");

        await page.FillAsync("[data-testid='search-input']", "contoso");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'tenant-row\\']:not([hidden])').length === 1");

        var statusText = await page.Locator("[data-testid='tenant-row']:not([hidden]) [data-testid='institution-status']").InnerTextAsync();
        Assert.Contains("Onboarding", statusText, StringComparison.OrdinalIgnoreCase);

        await page.CheckAsync("[data-testid='webhook-filter']");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'tenant-row\\']:not([hidden])').length === 0");

        var emptyVisible = await page.Locator("[data-testid='admin-empty-state']").IsVisibleAsync();
        Assert.True(emptyVisible);

        await page.UncheckAsync("[data-testid='webhook-filter']");
        await page.FillAsync("[data-testid='search-input']", string.Empty);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'tenant-row\\']').length === 3");

        await page.ClickAsync("[data-testid='add-tenant-button']");
        await page.FillAsync("[data-testid='tenant-name-input']", "Tailwind Academy");
        await page.FillAsync("[data-testid='tenant-id-input']", "tailwind-academy");
        await page.FillAsync("[data-testid='tenant-members-input']", "24");
        await page.FillAsync("[data-testid='tenant-addresses-input']", "52");
        await page.FillAsync("[data-testid='tenant-sso-input']", "https://tailwind.academy/sso");
        await page.CheckAsync("[data-testid='tenant-webhook-input']");
        await page.ClickAsync("[data-testid='tenant-save-button']");

        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=\\'tenant-row\\'][data-tenant-id=\\'tailwind-academy\\']') !== null");

        await page.ClickAsync("[data-testid='tenant-row'][data-tenant-id='tailwind-academy']");
        await page.SelectOptionAsync("[data-testid='tenant-status-select']", "Active");
        await page.FillAsync("[data-testid='tenant-members-input']", "48");
        await page.ClickAsync("[data-testid='tenant-save-button']");

    await page.WaitForFunctionAsync("() => { const cell = document.querySelector('[data-testid=\\'tenant-row\\'][data-tenant-id=\\'tailwind-academy\\'] [data-testid=\\'institution-status\\']'); return !!cell && !!cell.textContent && cell.textContent.toLowerCase().includes('active'); }");

        await page.ClickAsync("[data-testid='tenant-delete-button']");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\\'tenant-row\\'][data-tenant-id=\\'tailwind-academy\\']')");

        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=\\'tenant-row\\']').length === 3");

        await UiTestExperience.ObserveAsync(page);
    }

    [Fact]
    public async Task LandingPage_AllowsNavigationBetweenExperiences()
    {
        await using var context = await _browser.CreateContextAsync();
        var page = await context.NewPageAsync();
        if (!_browser.IsHeadless)
        {
            await page.BringToFrontAsync();
        }
        UiTestDiagnostics.Attach(page);

        var homeResponse = await page.GotoAsync($"{_server.BaseAddress}/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        Assert.True(homeResponse?.Ok, $"Home page load failed: {homeResponse?.Status} {homeResponse?.Url}");

    await page.ClickAsync("[data-testid='admin-entry']");
    await page.WaitForURLAsync("**/admin", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("text=Administrator Console");

        await page.ClickAsync("[data-testid='back-link']");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        Assert.Equal("/", new Uri(page.Url).AbsolutePath);

    await page.ClickAsync("[data-testid='tenant-entry']");
    await page.WaitForURLAsync("**/tenant", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForSelectorAsync("text=Tenant Alert Dashboard");

        await UiTestExperience.ObserveAsync(page);
    }

    [Theory]
    [InlineData("/tenant", "Tenant Alert Dashboard")]
    [InlineData("/admin", "Administrator Console")]
    public async Task FriendlyRoutes_RenderStaticExperiences(string path, string expectedHeading)
    {
        await using var context = await _browser.CreateContextAsync();
        var page = await context.NewPageAsync();
        if (!_browser.IsHeadless)
        {
            await page.BringToFrontAsync();
        }
        UiTestDiagnostics.Attach(page);

        var response = await page.GotoAsync($"{_server.BaseAddress}{path}", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        Assert.True(response?.Ok, $"Navigation to {path} failed: {response?.Status} {response?.Url}");

        var heading = await page.Locator("main h1").InnerTextAsync();
        Assert.Contains(expectedHeading, heading, StringComparison.OrdinalIgnoreCase);

        await UiTestExperience.ObserveAsync(page);
    }
}
