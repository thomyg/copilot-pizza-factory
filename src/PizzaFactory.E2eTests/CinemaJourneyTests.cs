using Microsoft.Playwright;

namespace PizzaFactory.E2eTests;

/// <summary>
/// Cinema mode — the perpetuum mobile, visible. The stage must stand on its own:
/// stations, pantry silos, the legend, and the always-moving rails.
/// </summary>
[Collection("web-app")]
public sealed class CinemaJourneyTests(WebAppFixture fixture)
{
    [Fact]
    public async Task the_stage_presents_the_whole_factory()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/cinema");

        await Assertions.Expect(page.Locator(".stage")).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(page.Locator(".wordmark")).ToContainTextAsync("COPILOT PIZZA FACTORY");

        // The five stations of the line plus the supplier.
        foreach (var label in new[] { "dough resting", "preparing", "in the fire", "at the pass", "tables", "supplier · A2A" })
        {
            await Assertions.Expect(page.Locator(".node .label", new() { HasTextString = label })).ToBeVisibleAsync();
        }

        // Every ingredient gets a silo — the pantry hides nothing.
        await Assertions.Expect(page.Locator(".silo")).ToHaveCountAsync(11, new() { Timeout = 15000 });

        // The one sentence the room needs.
        await Assertions.Expect(page.Locator(".legend .hint")).ToContainTextAsync("nobody is clicking anything");
    }
}
