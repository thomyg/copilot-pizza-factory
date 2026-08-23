using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;
using PizzaFactory.Trattoria;

namespace PizzaFactory.Trattoria.Tests;

/// <summary>
/// A service that ran is a fact about a day. These cover the handover from "the window just
/// shut" to "last Tuesday is something the house remembers".
/// </summary>
public sealed class ServiceLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 18, 0, 0, TimeSpan.Zero);

    private sealed class Clock(DateTimeOffset at) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = at;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (ServiceCloser Closer, Bookkeeper Books, IServiceLedgerRepository Ledger, IOrderRepository Orders, ServiceWindow Window, Clock Clock) House()
    {
        var clock = new Clock(T0);
        var orders = new InMemoryOrderRepository();
        var feed = new TrattoriaFeed();
        var options = new TrattoriaOptions();
        var maitreD = new MaitreD(orders, new InMemoryPizzaRepository(), options, feed);
        var book = new PreOrderBook(orders, feed);
        var window = new ServiceWindow(new ServiceWindowOptions(), clock);
        var ledger = new InMemoryServiceLedgerRepository();
        var books = new Bookkeeper(orders, new InMemoryStockRepository(), new InMemoryRestingDoughRepository(),
            maitreD, book, window, ledger, options, clock);
        return (new ServiceCloser(books, ledger, NullLogger<ServiceCloser>.Instance), books, ledger, orders, window, clock);
    }

    [Fact]
    public async Task closing_a_service_writes_what_it_took_into_the_books()
    {
        var (closer, _, ledger, orders, window, clock) = House();
        var session = window.Open();
        await orders.AddAsync(Order.Create("Diavolo", 3, OrderChannel.Restaurant, "Table 4", clock));
        await orders.AddAsync(Order.Create("Margherita", 2, OrderChannel.Online, "Bruno", clock));
        clock.Now = T0.AddMinutes(15);
        var closed = window.Close();

        await closer.CloseAsync(closed!);

        var written = Assert.Single(await ledger.RecentAsync(10));
        Assert.Equal(session.Id, written.Id);
        Assert.Equal(2, written.Orders);
        Assert.Equal(5, written.Pizzas);
        Assert.True(written.RevenueEur > 0);
        Assert.Equal(new DateOnly(2026, 8, 21), written.Date);
    }

    [Fact]
    public async Task a_day_the_house_really_traded_replaces_the_invented_one()
    {
        var (_, books, ledger, _, _, clock) = House();
        var yesterday = DateOnly.FromDateTime(T0.ToLocalTime().Date).AddDays(-1);
        await ledger.AddAsync(new ClosedService(
            "svc-real", yesterday, T0.AddDays(-1), T0.AddDays(-1).AddMinutes(15),
            Orders: 42, Pizzas: 77, Guests: 88, RevenueEur: 909.50m, AverageStars: 4.7));

        var history = await books.HistoryAsync();

        var day = Assert.Single(history, d => d.Date == yesterday);
        Assert.Equal(42, day.Orders);
        Assert.Equal(909.50m, day.RevenueEur);

        // Every other day still comes from the backstory, so comparisons keep working.
        Assert.Equal(7, history.Count);
        Assert.All(history.Where(d => d.Date != yesterday), d => Assert.True(d.Orders > 0));
    }

    /// <summary>Several demos in one day are one day's takings, not three separate Tuesdays.</summary>
    [Fact]
    public async Task two_services_on_the_same_day_add_up()
    {
        var (_, books, ledger, _, _, _) = House();
        var yesterday = DateOnly.FromDateTime(T0.ToLocalTime().Date).AddDays(-1);
        await ledger.AddAsync(new ClosedService("a", yesterday, T0.AddDays(-1), T0.AddDays(-1).AddMinutes(15), 10, 20, 30, 100m, 4.0));
        await ledger.AddAsync(new ClosedService("b", yesterday, T0.AddDays(-1), T0.AddDays(-1).AddMinutes(40), 5, 9, 12, 50m, 5.0));

        var day = Assert.Single(await books.HistoryAsync(), d => d.Date == yesterday);

        Assert.Equal(15, day.Orders);
        Assert.Equal(150m, day.RevenueEur);
        Assert.Equal(4.5, day.Stars);
    }
}
