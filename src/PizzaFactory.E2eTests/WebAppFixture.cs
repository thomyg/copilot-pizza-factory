using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// Boots the REAL Web app (Kestrel, in-memory store) as a child process and a headless Chromium
/// via Playwright. One app + one browser per test collection; each test gets a fresh page.
///
/// Giuseppe is configured with a deliberately unreachable endpoint: the agent EXISTS (chat UI
/// renders) but every model call fails — which is exactly the regression we test: a failing
/// agent must degrade in character, never kill the Blazor circuit.
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    private Process? _app;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = "";

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Playwright's browser download is cached after the first run; Main is idempotent.
        Microsoft.Playwright.Program.Main(["install", "chromium", "--only-shell"]);

        var port = FreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var webDll = FindWebDll();
        _app = Process.Start(new ProcessStartInfo("dotnet", [webDll])
        {
            WorkingDirectory = Path.GetDirectoryName(webDll)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["ASPNETCORE_URLS"] = BaseUrl,
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                // Chat UI renders, every model call fails — the graceful-degradation path under test.
                ["Giuseppe__Endpoint"] = "https://e2e-offline.invalid",
                ["Giuseppe__Deployment"] = "e2e-nonexistent",
                ["WorkIq__Mode"] = "Off",
            },
        }) ?? throw new InvalidOperationException("Failed to start the web app process.");

        await WaitUntilUpAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<IPage> NewLivePageAsync(string path)
    {
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(BaseUrl + path);
        await WaitForLiveCircuitAsync(page);
        return page;
    }

    /// <summary>
    /// Blazor Server renders static HTML first; clicks before the circuit connects vanish (we hit
    /// this by hand — machines shouldn't). The pages tick a clock chip every second, so "the
    /// timestamp changed" is a reliable "the app is interactive now" signal.
    /// </summary>
    private static async Task WaitForLiveCircuitAsync(IPage page)
    {
        var clock = page.Locator(".chip", new PageLocatorOptions { HasTextRegex = new System.Text.RegularExpressions.Regex(@"\d\d:\d\d:\d\d") });
        await Assertions.Expect(clock).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });

        var before = await clock.InnerTextAsync();
        await Assertions.Expect(clock).Not.ToHaveTextAsync(before, new LocatorAssertionsToHaveTextOptions { Timeout = 15_000 });
    }

    private async Task WaitUntilUpAsync()
    {
        using var http = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_app!.HasExited)
            {
                throw new InvalidOperationException(
                    $"Web app exited early:\n{await _app.StandardError.ReadToEndAsync()}");
            }

            try
            {
                var response = await http.GetAsync(BaseUrl + "/");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not up yet.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Web app did not become ready within 60s.");
    }

    private static string FindWebDll()
    {
        // tests run from src/PizzaFactory.E2eTests/bin/<Config>/net10.0 — walk up to src/.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PizzaFactory.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate the src directory (PizzaFactory.sln).");
        }

        var candidates = new[] { "Debug", "Release" }
            .Select(config => Path.Combine(dir.FullName, "PizzaFactory.Web", "bin", config, "net10.0", "PizzaFactory.Web.dll"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return candidates.FirstOrDefault()
            ?? throw new InvalidOperationException("PizzaFactory.Web is not built — run 'dotnet build' first.");
    }

    private static int FreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
            await _app.WaitForExitAsync();
        }

        _app?.Dispose();
    }
}

[CollectionDefinition("web-app")]
public sealed class WebAppCollection : ICollectionFixture<WebAppFixture>;
