using Microsoft.Extensions.AI;
using PizzaFactory.Domain;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

public class StorefrontTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 18, 17, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static (StorefrontToolSource Tools, OnlineOrderDesk Desk, PreOrderBook Book, InMemoryOrderRepository Orders) Build()
    {
        var orders = new InMemoryOrderRepository();
        var feed = new TrattoriaFeed();
        var options = new TrattoriaOptions { RandomSeed = 11, HandoverDelay = TimeSpan.Zero };
        var desk = new OnlineOrderDesk(orders, new InMemoryPizzaRepository(), options, feed);
        var book = new PreOrderBook(orders, feed);
        return (new StorefrontToolSource(desk, book, new FixedTimeProvider(T0)), desk, book, orders);
    }

    [Fact]
    public async Task the_storefront_belt_holds_customer_tools_and_nothing_else()
    {
        var (tools, _, _, _) = Build();

        var belt = await tools.GetToolsAsync();

        Assert.Equal(
            new[] { "book_reservation", "browse_menu", "check_order_status", "place_online_order" },
            belt.Select(t => t.Name).OrderBy(n => n).ToArray());

        // The security property in one assertion: no business, forecast, or factory tools here.
        Assert.DoesNotContain(belt, t => t.Name.Contains("business") || t.Name.Contains("forecast") || t.Name.Contains("stock"));
    }

    [Fact]
    public async Task the_menu_quotes_real_prices_and_toppings()
    {
        var (tools, _, _, _) = Build();
        var menu = (AIFunction)(await tools.GetToolsAsync()).Single(t => t.Name == "browse_menu");

        var json = (await menu.InvokeAsync(new AIFunctionArguments()))?.ToString();

        Assert.Contains("Margherita", json);
        Assert.Contains("9.90", json);
        Assert.Contains("Mozzarella", json);
    }

    [Fact]
    public async Task a_storefront_order_lands_as_a_real_online_order_with_a_ticket()
    {
        var (_, desk, _, orders) = Build();

        var (ticket, error) = await desk.PlaceAsync("hawaii", 2, FulfilmentMode.Delivery, "Sofia", T0);

        Assert.Null(error);
        Assert.Equal("Hawaii", ticket!.Pizza);                            // normalized casing
        var order = Assert.Single(await orders.ListAsync());
        Assert.Equal(OrderChannel.Online, order.Channel);
        Assert.Contains("Sofia", order.CustomerName);
        Assert.Contains(desk.Tickets, t => t.OrderId == order.Id);
    }

    [Fact]
    public async Task the_desk_refuses_nonsense_at_the_boundary()
    {
        var (_, desk, _, orders) = Build();

        Assert.NotNull((await desk.PlaceAsync("Calzone", 1, FulfilmentMode.Takeaway, "Anna", T0)).Error);
        Assert.NotNull((await desk.PlaceAsync("Diavolo", 99, FulfilmentMode.Takeaway, "Anna", T0)).Error);
        Assert.NotNull((await desk.PlaceAsync("Diavolo", 1, FulfilmentMode.Takeaway, "  ", T0)).Error);
        Assert.Empty(await orders.ListAsync());
    }

    [Fact]
    public async Task the_concierge_can_check_an_order_status_by_name()
    {
        var (tools, desk, _, _) = Build();
        await desk.PlaceAsync("Funghi", 1, FulfilmentMode.Takeaway, "Night-Owl Nadia", T0);
        var status = (AIFunction)(await tools.GetToolsAsync()).Single(t => t.Name == "check_order_status");

        var reply = (await status.InvokeAsync(new AIFunctionArguments { ["nameOrId"] = "nadia" }))?.ToString();

        Assert.Contains("Funghi", reply);
        Assert.Contains("kitchen", reply);
    }

    [Fact]
    public async Task the_concierge_books_reservations_into_the_same_book_the_house_reads()
    {
        var (tools, _, book, _) = Build();
        var reserve = (AIFunction)(await tools.GetToolsAsync()).Single(t => t.Name == "book_reservation");

        var reply = (await reserve.InvokeAsync(new AIFunctionArguments
        {
            ["pizza"] = "Diavolo", ["amount"] = 10, ["when"] = "2026-08-22 18:00", ["forName"] = "Nonna's Bingo Club",
        }))?.ToString();

        Assert.Contains("Reservation booked", reply);
        Assert.Single(book.Upcoming);
    }
}
