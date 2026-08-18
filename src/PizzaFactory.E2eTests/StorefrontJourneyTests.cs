using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// The customer journey on the (simulated) public site: browse the menu with real prices,
/// order takeaway and watch it appear on the house's internal board, reserve ahead into the
/// same book the trattoria reads, and chat with the storefront concierge — which degrades in
/// character when the model is unreachable, same contract as the house.
/// </summary>
[Collection("web-app")]
public class StorefrontJourneyTests(WebAppFixture app)
{
    private static readonly LocatorAssertionsToContainTextOptions Patient = new() { Timeout = 20_000 };

    [Fact]
    public async Task the_menu_shows_every_pizza_with_a_price()
    {
        var page = await app.Browser.NewPageAsync();
        await page.GotoAsync(app.BaseUrl + "/storefront");

        await Assertions.Expect(page.Locator(".menu-card")).ToHaveCountAsync(6, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
        await Assertions.Expect(page.Locator(".menu-card .price").First).ToContainTextAsync("€", Patient);
        await Assertions.Expect(page.Locator(".lock-chip")).ToContainTextAsync("Entra", Patient);
    }

    [Fact]
    public async Task a_takeaway_order_reaches_the_house_board()
    {
        var page = await app.Browser.NewPageAsync();
        await page.GotoAsync(app.BaseUrl + "/storefront");
        await WaitForInteractiveAsync(page);

        await page.Locator(".order-form .name").FillAsync("E2E Emma");
        await page.Locator(".order-form .cta").ClickAsync();

        await Assertions.Expect(page.Locator("#order .result")).ToContainTextAsync("Grazie, E2E Emma", Patient);
        await Assertions.Expect(page.Locator("#order .status")).ToContainTextAsync("kitchen", Patient);

        // The same order shows up back of house, on the Window's online board.
        var house = await app.NewLivePageAsync("/");
        await Assertions.Expect(house.Locator(".tickets")).ToContainTextAsync("E2E Emma", Patient);
        await house.CloseAsync();
    }

    [Fact]
    public async Task a_reservation_lands_in_the_book_the_house_reads()
    {
        var page = await app.Browser.NewPageAsync();
        await page.GotoAsync(app.BaseUrl + "/storefront");
        await WaitForInteractiveAsync(page);

        await page.Locator(".reserve-form .name").FillAsync("E2E Bingo Club");
        await page.Locator(".reserve-form .cta").ClickAsync();
        await Assertions.Expect(page.Locator("#reserve .result")).ToContainTextAsync("Booked", Patient);

        var house = await app.NewLivePageAsync("/");
        await Assertions.Expect(house.Locator(".preorders")).ToContainTextAsync("E2E Bingo Club", Patient);
        await house.Locator(".preorders .cancel").First.ClickAsync();       // leave the book clean
        await house.CloseAsync();
    }

    [Fact]
    public async Task the_storefront_concierge_survives_a_dead_model_in_character()
    {
        var page = await app.Browser.NewPageAsync();
        await page.GotoAsync(app.BaseUrl + "/storefront");
        await WaitForInteractiveAsync(page);

        await page.Locator(".hero-actions .cta.ghost").ClickAsync();
        await Assertions.Expect(page.Locator(".chat-drawer.open")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

        await page.Locator(".chat-input input").FillAsync("What's on the menu?");
        await page.Locator(".chat-input button").ClickAsync();

        await Assertions.Expect(page.Locator(".bubble.chef").Last)
            .ToContainTextAsync("dropped a whole tray", new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    /// <summary>The storefront has no ticking clock chip — wait for the circuit via a quick interactivity probe.</summary>
    private static async Task WaitForInteractiveAsync(IPage page)
    {
        // The chat toggle flips a class synchronously once the circuit is live; poll until it reacts.
        var ghost = page.Locator(".hero-actions .cta.ghost");
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await ghost.ClickAsync();
            if (await page.Locator(".chat-drawer.open").CountAsync() > 0)
            {
                await page.Locator(".chat-drawer .close").ClickAsync();     // close it again; we just probed
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Storefront circuit did not become interactive within 20s.");
    }
}
