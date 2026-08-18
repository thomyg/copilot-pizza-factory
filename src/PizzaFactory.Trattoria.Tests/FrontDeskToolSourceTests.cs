using Microsoft.Extensions.AI;
using PizzaFactory.Domain;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

public class FrontDeskToolSourceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static (FrontDeskToolSource Desk, PreOrderBook Book, InMemoryOrderRepository Orders) Build()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var feed = new TrattoriaFeed();
        var options = new TrattoriaOptions { RandomSeed = 5 };
        var book = new PreOrderBook(orders, feed);
        var maitreD = new MaitreD(orders, pizzas, options, feed);
        var clock = new FixedTimeProvider(T0);
        var bookkeeper = new Bookkeeper(orders, maitreD, clock);
        return (new FrontDeskToolSource(book, maitreD, bookkeeper, clock), book, orders);
    }

    [Fact]
    public async Task the_front_desk_offers_the_reservations_book_and_the_room_status()
    {
        var (desk, _, _) = Build();

        var tools = await desk.GetToolsAsync();

        Assert.Equal(
            new[] { "book_pre_order", "business_report", "dining_room_status", "list_pre_orders", "sales_history" },
            tools.Select(t => t.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task giuseppe_can_book_a_pre_order_and_read_it_back()
    {
        var (desk, book, _) = Build();
        var tools = await desk.GetToolsAsync();
        var bookTool = (AIFunction)tools.Single(t => t.Name == "book_pre_order");
        var listTool = (AIFunction)tools.Single(t => t.Name == "list_pre_orders");

        var confirmation = (await bookTool.InvokeAsync(new AIFunctionArguments
        {
            ["pizza"] = "Diavolo",
            ["amount"] = 10,
            ["when"] = "2026-08-22 18:00",
            ["forName"] = "Nonna's Bingo Club",
        }))?.ToString();

        Assert.Contains("Booked", confirmation);
        Assert.Contains("Nonna", confirmation);
        var entry = Assert.Single(book.Upcoming);
        Assert.Equal("Diavolo", entry.Pizza);
        Assert.Equal(10, entry.Amount);

        var listing = (await listTool.InvokeAsync(new AIFunctionArguments()))?.ToString();
        Assert.Contains("Diavolo", listing);
        Assert.Contains("2026-08-22 18:00", listing);
    }

    [Fact]
    public async Task the_book_politely_refuses_nonsense()
    {
        var (desk, book, _) = Build();
        var bookTool = (AIFunction)(await desk.GetToolsAsync()).Single(t => t.Name == "book_pre_order");

        var offMenu = (await bookTool.InvokeAsync(new AIFunctionArguments
        {
            ["pizza"] = "Calzone", ["amount"] = 2, ["when"] = "2026-08-22 18:00", ["forName"] = "Anna",
        }))?.ToString();
        Assert.Contains("not on the menu", offMenu);

        var badDate = (await bookTool.InvokeAsync(new AIFunctionArguments
        {
            ["pizza"] = "Diavolo", ["amount"] = 2, ["when"] = "next-ish Saturday", ["forName"] = "Anna",
        }))?.ToString();
        Assert.Contains("yyyy-MM-dd", badDate);

        Assert.Empty(book.Upcoming);
    }

    [Fact]
    public async Task the_room_status_reports_the_essentials()
    {
        var (desk, _, _) = Build();
        var statusTool = (AIFunction)(await desk.GetToolsAsync()).Single(t => t.Name == "dining_room_status");

        var status = (await statusTool.InvokeAsync(new AIFunctionArguments()))?.ToString();

        Assert.Contains("\"serviceOpen\":false", status);
        Assert.Contains("\"tablesFree\":17", status);
    }
}
