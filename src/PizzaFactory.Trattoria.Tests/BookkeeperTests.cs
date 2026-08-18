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

    private static (Bookkeeper Bookkeeper, InMemoryOrderRepository Orders) Build()
    {
        var orders = new InMemoryOrderRepository();
        var maitreD = new MaitreD(orders, new InMemoryPizzaRepository(), new TrattoriaOptions { RandomSeed = 3 }, new TrattoriaFeed());
        return (new Bookkeeper(orders, maitreD, new FixedTimeProvider(T0)), orders);
    }

    [Fact]
    public async Task the_report_counts_real_orders_and_prices_them()
    {
        var (bookkeeper, orders) = Build();
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
        var (bookkeeper, orders) = Build();
        var yesterday = new FixedTimeProvider(T0.AddDays(-1));

        await orders.AddAsync(Order.Create("Hawaii", 5, OrderChannel.Online, "Old news", yesterday));

        var report = await bookkeeper.ReportAsync();

        Assert.Equal(0, report.OrdersToday);
    }

    [Fact]
    public void the_backstory_is_deterministic_and_weekends_run_hotter()
    {
        var (bookkeeper, _) = Build();

        var first = bookkeeper.History();
        var second = bookkeeper.History();

        Assert.Equal(7, first.Count);
        Assert.Equal(first, second);                                     // stable across calls
        Assert.All(first, day => Assert.InRange(day.Orders, 55, 130));
        Assert.All(first, day => Assert.InRange(day.Stars, 4.1, 4.7));

        var friday = first.Single(d => d.Day == "Friday");
        Assert.True(friday.Orders >= 95, "Fridays are busy at a pizzeria — that's the whole point");
    }
}
