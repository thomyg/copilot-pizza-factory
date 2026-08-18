using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// The dinner-service journey: the floor map renders, Play opens service and guests actually
/// arrive, pre-orders can be booked and cancelled, and the Engine Room can ring in a surprise
/// online order that shows up on the Window's ticket board.
/// </summary>
[Collection("web-app")]
public class TrattoriaJourneyTests(WebAppFixture app)
{
    private static readonly LocatorAssertionsToContainTextOptions Patient = new() { Timeout = 20_000 };

    [Fact]
    public async Task the_dining_room_renders_all_seventeen_tables()
    {
        var page = await app.NewLivePageAsync("/");

        await Assertions.Expect(page.Locator(".floor .table")).ToHaveCountAsync(17, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
        await Assertions.Expect(page.Locator(".legend")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button.play")).ToContainTextAsync("Open the floor", Patient);
    }

    [Fact]
    public async Task play_opens_the_floor_and_a_bus_tour_fills_tables()
    {
        var page = await app.NewLivePageAsync("/");
        try
        {
            await page.Locator("button.play").ClickAsync();

            await Assertions.Expect(page.Locator("button.play")).ToContainTextAsync("Close service", Patient);
            await Assertions.Expect(page.Locator(".trattoria-feed")).ToContainTextAsync("Service is OPEN", Patient);

            // Random walk-ins are stochastic — the Engine Room's bus tour is not. Park the bus:
            // its parties are seated on the very next tick, deterministically.
            var engineRoom = await app.NewLivePageAsync("/engine-room");
            await engineRoom.Locator(".chaos-card button", new PageLocatorOptions { HasTextString = "Park the bus" }).ClickAsync();
            await Assertions.Expect(engineRoom.Locator(".chaos-result")).ToContainTextAsync("bus", Patient);
            await engineRoom.CloseAsync();

            await Assertions.Expect(page.Locator(".table.seated, .table.waiting, .table.eating, .table.paying").First)
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 });
        }
        finally
        {
            if ((await page.Locator("button.play").InnerTextAsync()).Contains("Close service"))
            {
                await page.Locator("button.play").ClickAsync();   // leave the floor closed for other tests
            }
        }
    }

    [Fact]
    public async Task a_pre_order_can_be_booked_and_cancelled()
    {
        var page = await app.NewLivePageAsync("/");

        await page.Locator(".preorder-form .name").FillAsync("E2E Crew");
        await page.Locator(".preorder-form .amount").FillAsync("3");
        await page.Locator(".preorder-form button.primary").ClickAsync();

        await Assertions.Expect(page.Locator(".preorder-result")).ToContainTextAsync("Booked", Patient);
        await Assertions.Expect(page.Locator(".preorders")).ToContainTextAsync("E2E Crew", Patient);

        await page.Locator(".preorders .cancel").First.ClickAsync();
        await Assertions.Expect(page.Locator(".preorders", new PageLocatorOptions { HasTextString = "E2E Crew" }))
            .ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });
    }

    [Fact]
    public async Task the_engine_room_can_ring_in_a_surprise_online_order()
    {
        var engineRoom = await app.NewLivePageAsync("/engine-room");
        await engineRoom.Locator(".chaos-card button", new PageLocatorOptions { HasTextString = "Ring it in" }).ClickAsync();
        await Assertions.Expect(engineRoom.Locator(".chaos-result")).ToContainTextAsync("ordered", Patient);
        await engineRoom.CloseAsync();

        var window = await app.NewLivePageAsync("/");
        await Assertions.Expect(window.Locator(".tickets tr").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15_000 });
    }
}
