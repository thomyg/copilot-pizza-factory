using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// The presenter journey: open the Engine Room, flip the flight level, and pull every chaos
/// lever the way a demo would — sabotage, rush hour, restock, front door — asserting the
/// factory visibly reacts and nothing takes the page down.
/// </summary>
[Collection("web-app")]
public class EngineRoomJourneyTests(WebAppFixture app)
{
    private static readonly LocatorAssertionsToContainTextOptions Patient = new() { Timeout = 20_000 };

    [Fact]
    public async Task engine_room_renders_every_panel()
    {
        var page = await app.NewLivePageAsync("/engine-room");

        foreach (var heading in new[] { "The Line", "The Pantry", "The Chaos Console", "The Wire", "Showtime Script" })
        {
            await Assertions.Expect(page.Locator("h2", new PageLocatorOptions { HasTextString = heading }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
        }

        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task flight_level_toggle_switches_the_talk_track()
    {
        var page = await app.NewLivePageAsync("/engine-room");

        await Assertions.Expect(page.Locator(".note").First).ToContainTextAsync("The factory runs itself", Patient);

        await page.Locator(".flight-toggle button", new PageLocatorOptions { HasTextString = "Nerds" }).ClickAsync();
        await Assertions.Expect(page.Locator(".note").First).ToContainTextAsync("BackgroundService", Patient);

        await page.Locator(".flight-toggle button", new PageLocatorOptions { HasTextString = "Suits" }).ClickAsync();
        await Assertions.Expect(page.Locator(".note").First).ToContainTextAsync("The factory runs itself", Patient);
    }

    [Fact]
    public async Task pineapple_incident_reports_and_hits_the_ticker()
    {
        var page = await app.NewLivePageAsync("/engine-room");

        await page.Locator(".chaos-card button", new PageLocatorOptions { HasTextString = "Make it happen" }).ClickAsync();

        await Assertions.Expect(page.Locator(".chaos-result")).ToContainTextAsync("Pineapple", Patient);
        await Assertions.Expect(page.Locator(".ticker")).ToContainTextAsync("presenter 86'd the Pineapple", Patient);
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task rush_hour_floods_the_floor_with_orders()
    {
        var page = await app.NewLivePageAsync("/engine-room");

        await page.Locator(".chaos-card input[type=number]").FillAsync("5");
        await page.Locator(".chaos-card button", new PageLocatorOptions { HasTextString = "Unleash" }).ClickAsync();

        await Assertions.Expect(page.Locator(".chaos-result")).ToContainTextAsync("5 orders just hit the floor", Patient);
        await Assertions.Expect(page.Locator(".ticker")).ToContainTextAsync("unleashed a rush of 5 orders", Patient);
    }

    [Fact]
    public async Task nonnas_emergency_delivery_resets_the_pantry()
    {
        var page = await app.NewLivePageAsync("/engine-room");

        await page.Locator(".chaos-card button", new PageLocatorOptions { HasTextString = "Restock everything" }).ClickAsync();

        await Assertions.Expect(page.Locator(".chaos-result")).ToContainTextAsync("Pantry reset", Patient);

        // Flour's opening level is 2500g and nothing on the floor consumes it that fast.
        var flourRow = page.Locator(".pantry tr", new PageLocatorOptions { HasTextString = "Flour" });
        await Assertions.Expect(flourRow.Locator(".grams")).ToContainTextAsync("2500", Patient);
    }

    [Fact]
    public async Task the_front_door_closes_public_ordering_and_reopens()
    {
        var page = await app.NewLivePageAsync("/engine-room");
        var door = page.Locator(".chaos-card button", new PageLocatorOptions { HasTextString = "Ordering" });
        try
        {
            await door.ClickAsync();
            await Assertions.Expect(door).ToContainTextAsync("closed", Patient);

            var window = await app.NewLivePageAsync("/");
            await Assertions.Expect(window.Locator(".order .closed"))
                .ToContainTextAsync("Ordering is closed", Patient);
            await window.CloseAsync();
        }
        finally
        {
            // Leave the door open for the other journeys — shared app, be a good guest.
            if ((await door.InnerTextAsync()).Contains("closed"))
            {
                await door.ClickAsync();
            }
        }

        await Assertions.Expect(door).ToContainTextAsync("open", Patient);
    }
}
