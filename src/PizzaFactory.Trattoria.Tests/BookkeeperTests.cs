using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

public class BookkeeperTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 18, 19, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static (Bookkeeper Bookkeeper, InMemoryOrderRepository Orders, InMemoryStockRepository Stock, PreOrderBook Book) Build()
    {
        var orders = new InMemoryOrderRepository();
        var stock = new InMemoryStockRepository();
        var feed = new TrattoriaFeed();
        var trattoriaOptions = new TrattoriaOptions { RandomSeed = 3 };
        var book = new PreOrderBook(orders, feed);
        var maitreD = new MaitreD(orders, new InMemoryPizzaRepository(), trattoriaOptions, feed);
        return (new Bookkeeper(orders, stock, new InMemoryRestingDoughRepository(), maitreD, book, OpenWindow(new FixedTimeProvider(T0)), new InMemoryServiceLedgerRepository(), trattoriaOptions, new FixedTimeProvider(T0)), orders, stock, book);
    }

    /// <summary>A service that is open, so the books scope to it exactly as they do in the house.</summary>
    private static ServiceWindow OpenWindow(TimeProvider clock)
    {
        var window = new ServiceWindow(new ServiceWindowOptions(), clock);
        window.Open();
        return window;
    }

    [Fact]
    public async Task the_report_counts_real_orders_and_prices_them()
    {
        var (bookkeeper, orders, _, _) = Build();
        var clock = new FixedTimeProvider(T0);

        var sold = await orders.AddAsync(Order.Create("Margherita", 2, OrderChannel.Restaurant, "Table 3", clock));
        await orders.UpdateAsync(sold.Start().MarkReady().MarkDelivered());
        await orders.AddAsync(Order.Create("Diavolo", 1, OrderChannel.Phone, "Marco", clock));

        var report = await bookkeeper.ReportAsync();

        Assert.Equal(2, report.OrdersToday);
        Assert.Equal(3, report.PizzasOrderedToday);
        Assert.Equal(2, report.PizzasDeliveredToday);
        Assert.Equal(2 * 9.90m, report.RevenueDeliveredEur);
        Assert.Equal(12.90m, report.RevenueInFlightEur);
        Assert.Equal("Margherita", report.TopSeller);
        Assert.Equal(1, report.OrdersByChannel["Restaurant"]);
        Assert.Equal(1, report.OrdersByChannel["Phone"]);
        Assert.Equal(2, report.OrdersLastTenMinutes);
        Assert.Equal(12, report.ProjectedOrdersNextHour);
    }

    [Fact]
    public async Task yesterdays_orders_stay_out_of_todays_report()
    {
        var (bookkeeper, orders, _, _) = Build();
        var yesterday = new FixedTimeProvider(T0.AddDays(-1));

        await orders.AddAsync(Order.Create("Hawaii", 5, OrderChannel.Online, "Old news", yesterday));

        var report = await bookkeeper.ReportAsync();

        Assert.Equal(0, report.OrdersToday);
    }

    [Fact]
    public void the_backstory_is_deterministic_and_weekends_run_hotter()
    {
        var (bookkeeper, _, _, _) = Build();

        var first = bookkeeper.History();
        var second = bookkeeper.History();

        Assert.Equal(7, first.Count);
        Assert.Equal(first, second);                                     // stable across calls
        Assert.All(first, day => Assert.InRange(day.Orders, 55, 130));
        Assert.All(first, day => Assert.InRange(day.Stars, 4.1, 4.7));

        var friday = first.Single(d => d.Day == "Friday");
        Assert.True(friday.Orders >= 95, "Fridays are busy at a pizzeria — that's the whole point");
    }

    [Fact]
    public async Task the_crystal_ball_flags_stock_that_cannot_cover_committed_orders()
    {
        var (bookkeeper, orders, stock, _) = Build();
        await stock.SaveAsync(PizzaFactory.Domain.Entities.Stock.Empty);
        await orders.AddAsync(Order.Create("Margherita", 2, OrderChannel.Restaurant, "Table 1", new FixedTimeProvider(T0)));

        var risks = await bookkeeper.ForecastAsync();

        Assert.Contains(risks, r => r.Severity == "High" && r.Risk.Contains("run out"));
        Assert.Equal("High", risks[0].Severity);                          // worst first
    }

    [Fact]
    public async Task the_crystal_ball_spots_the_low_pineapple_and_a_big_reservation()
    {
        var (bookkeeper, _, _, book) = Build();
        Assert.Null(book.TryAdd("Diavolo", 10, T0.AddHours(2), "Nonna's Bingo Club", T0));

        var risks = await bookkeeper.ForecastAsync();

        // Opening stock has pineapple at 250g — below the 300g restock threshold from day one.
        Assert.Contains(risks, r => r.Severity == "Medium" && r.Risk.Contains("Pineapple"));
        Assert.Contains(risks, r => r.Risk.Contains("Big reservation") && r.Detail.Contains("Nonna"));
    }

    [Fact]
    public async Task an_empty_dough_buffer_with_open_orders_is_a_high_risk()
    {
        var (bookkeeper, orders, _, _) = Build();
        var clock = new FixedTimeProvider(T0);
        for (var i = 0; i < 3; i++)
        {
            await orders.AddAsync(Order.Create("Margherita", 1, OrderChannel.Online, $"Guest {i}", clock));
        }

        var risks = await bookkeeper.ForecastAsync();

        Assert.Contains(risks, r => r.Severity == "High" && r.Risk.Contains("Dough"));
    }
}
