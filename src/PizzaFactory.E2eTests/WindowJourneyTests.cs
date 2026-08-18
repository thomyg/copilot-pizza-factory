using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// The audience journey: load the Window, watch it live, order a pizza, talk to Giuseppe.
/// The chat runs against an unreachable model on purpose — a failing agent must apologize
/// in character, never kill the circuit (the bug that prompted this suite).
/// </summary>
[Collection("web-app")]
public class WindowJourneyTests(WebAppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Patient = new() { Timeout = 15_000 };

    [Fact]
    public async Task window_loads_with_a_live_ticking_line()
    {
        var page = await app.NewLivePageAsync("/");

        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Window into the Factory");
        Assert.Equal(4, await page.Locator(".station .count").CountAsync());
        await Assertions.Expect(page.Locator(".pantry tr")).ToHaveCountAsync(11);
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task guest_can_order_and_the_open_orders_count_rises()
    {
        var page = await app.NewLivePageAsync("/");
        var openBefore = await ReadOpenOrdersAsync(page);

        await page.Locator(".order-form button.primary").ClickAsync();

        await Assertions.Expect(page.Locator(".order-result"))
            .ToContainTextAsync("Order placed as", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        // The chip polls every second; the new order must show up as open (or already be baking —
        // then a station count is non-zero. Either way the floor acknowledged it.)
        await Assertions.Expect(page.Locator(".chip", new PageLocatorOptions { HasTextString = "open orders" }))
            .Not.ToContainTextAsync($"{openBefore} open orders", new LocatorAssertionsToContainTextOptions { Timeout = 20_000 });
    }

    [Fact]
    public async Task giuseppe_failure_degrades_in_character_and_never_kills_the_circuit()
    {
        var page = await app.NewLivePageAsync("/");

        await OpenChatDrawerAsync(page);
        await page.Locator(".chat-input input").FillAsync("What pizzas can I order?");
        await page.Locator(".chat-input button").ClickAsync();

        await Assertions.Expect(page.Locator(".bubble.you")).ToContainTextAsync("What pizzas can I order?");
        await Assertions.Expect(page.Locator(".bubble.chef").Last)
            .ToContainTextAsync("dropped a whole tray", new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });

        // The regression that started all this: the circuit must survive the agent failure.
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        // And the page must still be alive — the input takes another message.
        await Assertions.Expect(page.Locator(".chat-input input")).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 15_000 });
    }

    [Fact]
    public async Task pressing_enter_sends_the_chat_message()
    {
        var page = await app.NewLivePageAsync("/");

        await OpenChatDrawerAsync(page);
        await page.Locator(".chat-input input").FillAsync("Ciao Giuseppe!");
        await page.Locator(".chat-input input").PressAsync("Enter");

        // Regression: with onchange binding, Enter fired before the bind and the text vanished.
        await Assertions.Expect(page.Locator(".bubble.you").Last)
            .ToContainTextAsync("Ciao Giuseppe!", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task window_links_to_the_engine_room_and_back()
    {
        var page = await app.NewLivePageAsync("/");

        await page.Locator("a.engine-room-link", new PageLocatorOptions { HasTextString = "Engine" }).ClickAsync();
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("The Engine Room", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });

        await page.Locator(".tagline a").ClickAsync();
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Window into the Factory", new LocatorAssertionsToContainTextOptions { Timeout = 15_000 });
    }

    /// <summary>Giuseppe lives in a VS Code-style left drawer now — open it before chatting.</summary>
    private static async Task OpenChatDrawerAsync(IPage page)
    {
        await page.Locator(".chat-toggle").ClickAsync();
        await Assertions.Expect(page.Locator(".chat-drawer.open"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });
    }

    private static async Task<int> ReadOpenOrdersAsync(IPage page)
    {
        var text = await page.Locator(".chip", new PageLocatorOptions { HasTextString = "open orders" }).InnerTextAsync();
        return int.Parse(Regex.Match(text, @"\d+").Value);
    }
}
