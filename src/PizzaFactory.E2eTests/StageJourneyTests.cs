using System.Diagnostics;
using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// The stage is the home page. Its promise: whatever someone does through Copilot, Teams or
/// SharePoint shows up on it, attributed to that channel, within a couple of seconds.
/// Runs in low quality: the test browser has no GPU.
/// </summary>
[Collection("web-app")]
public sealed class StageJourneyTests(WebAppFixture app) : IAsyncLifetime
{
    // Every open stage keeps rendering with software WebGL; one left behind slows every later test.
    private readonly List<IPage> _pages = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var page in _pages)
        {
            await page.CloseAsync();
        }
    }

    private async Task<IPage> StageAsync()
    {
        var page = await app.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 720 } });
        _pages.Add(page);
        await page.GotoAsync(app.BaseUrl + "/?quality=low");
        await Assertions.Expect(page.Locator("[data-testid=stage][data-ready=true]"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        return page;
    }

    [Fact]
    public async Task the_stage_shows_every_room_of_the_house()
    {
        var page = await StageAsync();

        await Assertions.Expect(page.Locator(".st-zones button")).ToHaveCountAsync(7);
        await Assertions.Expect(page.Locator("canvas")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    /// <summary>
    /// The stage must stay framed: a trackpad pinch over a panel used to zoom the whole page and
    /// crop every edge, and labels past the edge made the stage scrollable.
    /// </summary>
    [Fact]
    public async Task the_stage_cannot_be_zoomed_or_scrolled_out_of_frame()
    {
        var page = await StageAsync();

        var result = await page.EvaluateAsync<bool[]>(@"() => {
            const root = document.getElementById('stage-root');
            root.scrollTop = 200; root.scrollLeft = 200;
            const stage = root.querySelector('.st-stage');
            stage.scrollTop = 200; stage.scrollLeft = 200;
            const pinch = new WheelEvent('wheel', { ctrlKey: true, deltaY: -50, bubbles: true, cancelable: true });
            root.querySelector('.st-kpis').dispatchEvent(pinch);
            return [root.scrollTop === 0 && root.scrollLeft === 0 && stage.scrollTop === 0 && stage.scrollLeft === 0, pinch.defaultPrevented];
        }");

        Assert.True(result[0], "the stage scrolled");
        Assert.True(result[1], "a pinch over a panel reached the browser");
    }

    /// <summary>However far the page is pinch-zoomed, every panel stays inside what is visible.</summary>
    [Fact]
    public async Task the_panels_stay_in_view_when_the_page_is_pinch_zoomed()
    {
        var page = await StageAsync();
        var cdp = await page.Context.NewCDPSessionAsync(page);

        await cdp.SendAsync("Emulation.setPageScaleFactor", new Dictionary<string, object> { ["pageScaleFactor"] = 1.6 });
        await page.WaitForTimeoutAsync(500);

        var outside = await page.EvaluateAsync<string[]>(@"() => {
            const vv = visualViewport;
            return ['.st-top', '.st-kpis', '.st-inspector', '.st-tracker', '.st-feed'].filter(s => {
                const r = document.querySelector(s).getBoundingClientRect();
                return r.left < vv.offsetLeft - 1 || r.top < vv.offsetTop - 1
                    || r.right > vv.offsetLeft + vv.width + 1 || r.bottom > vv.offsetTop + vv.height + 1;
            });
        }");

        Assert.Empty(outside);
    }

    [Fact]
    public async Task an_action_through_copilot_appears_on_stage_within_two_seconds()
    {
        var page = await StageAsync();
        var day = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");

        var clock = Stopwatch.StartNew();
        var response = await page.APIRequest.PostAsync(app.BaseUrl + "/api/nonna/time-off", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-PizzaFactory-Channel"] = "copilot" },
            DataObject = new { name = "Rosa", date = day, slot = "Dinner", reason = "football final" },
        });
        Assert.True(response.Ok, await response.TextAsync());

        await Assertions.Expect(page.Locator("[data-banner-who]"))
            .ToContainTextAsync("via Copilot", new LocatorAssertionsToContainTextOptions { Timeout = 2_000 });
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2.5), $"took {clock.Elapsed}");
        await Assertions.Expect(page.Locator(".st-feed .st-tag.m365").First).ToBeVisibleAsync();
    }

    /// <summary>The presenter opens the house from the stage itself.</summary>
    [Fact]
    public async Task the_open_service_button_opens_the_house()
    {
        var page = await StageAsync();
        var open = page.Locator("[data-open]");
        if (!await open.IsVisibleAsync())
        {
            return;   // another test in this run already opened it
        }

        await open.ClickAsync();

        await Assertions.Expect(page.Locator("[data-service-text]"))
            .ToContainTextAsync("Service open", new LocatorAssertionsToContainTextOptions { Timeout = 10_000 });
    }

    /// <summary>What the MCP server sends after Giuseppe orders inside Copilot.</summary>
    [Fact]
    public async Task an_order_giuseppe_places_from_copilot_lands_on_stage()
    {
        var page = await StageAsync();

        var response = await page.APIRequest.PostAsync(app.BaseUrl + "/api/floor/signal", new APIRequestContextOptions
        {
            DataObject = new { kind = "order.placed", orderId = Guid.NewGuid().ToString("n"), pizza = "Diavolo", amount = 3, customer = "Retro team", channel = "copilot" },
        });
        Assert.True(response.Ok, await response.TextAsync());

        await Assertions.Expect(page.Locator("[data-banner-who]"))
            .ToContainTextAsync("Giuseppe · via Copilot", new LocatorAssertionsToContainTextOptions { Timeout = 2_000 });
        await Assertions.Expect(page.Locator("[data-banner-what]")).ToContainTextAsync("3× Diavolo");
        await Assertions.Expect(page.Locator("[data-banner-what]")).ToContainTextAsync("for Retro team");
    }

    /// <summary>
    /// Not just the order: the stage follows it through the kitchen, and the tracker pins it.
    /// </summary>
    [Fact]
    public async Task a_copilot_order_is_followed_and_pinned_to_the_tracker()
    {
        var page = await StageAsync();
        var open = page.Locator("[data-open]");
        if (await open.IsVisibleAsync())
        {
            await open.ClickAsync();
        }

        // the two-second promise is the banner test's; here the kitchen has to be running first
        await Assertions.Expect(page.Locator("[data-service-text]"))
            .ToContainTextAsync("Service open", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        var orderId = Guid.NewGuid().ToString("n");
        var response = await page.APIRequest.PostAsync(app.BaseUrl + "/api/floor/signal", new APIRequestContextOptions
        {
            DataObject = new { kind = "order.placed", orderId, pizza = "Margherita", amount = 1, customer = "Anna", channel = "copilot" },
        });
        Assert.True(response.Ok, await response.TextAsync());

        var stage = page.Locator($"[data-testid=stage][data-follow='{orderId}']");
        await Assertions.Expect(stage).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
        await Assertions.Expect(page.Locator(".st-tracker.following [data-tr-title]")).ToContainTextAsync("Following · via Copilot");
        await Assertions.Expect(page.Locator("[data-tr-sub]")).ToContainTextAsync("for Anna");
        // how soon the pizzaiolo reaches it depends on the simulated queue ahead of it, so the walk
        // through bench → oven → box → courier is not asserted here (too slow to be deterministic)
    }

    [Fact]
    public async Task the_agents_own_work_is_reported_quietly()
    {
        var page = await StageAsync();

        var response = await page.APIRequest.PostAsync(app.BaseUrl + "/api/nonna/time-off", new APIRequestContextOptions
        {
            DataObject = new { name = "Maria", date = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"), slot = "Lunch", reason = "dentist" },
        });
        Assert.True(response.Ok);

        await Assertions.Expect(page.Locator(".st-feed li", new PageLocatorOptions { HasText = "Maria asks" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await Assertions.Expect(page.Locator("[data-banner].show")).ToHaveCountAsync(0);
    }
}
