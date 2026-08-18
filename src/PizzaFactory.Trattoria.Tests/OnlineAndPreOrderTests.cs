using PizzaFactory.Domain;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

public class OnlineAndPreOrderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    private static (OnlineOrderDesk Desk, PreOrderBook Book, InMemoryOrderRepository Orders, InMemoryPizzaRepository Pizzas)
        Build(Action<TrattoriaOptions>? configure = null)
    {
        var options = new TrattoriaOptions { RandomSeed = 7, HandoverDelay = TimeSpan.Zero };
        configure?.Invoke(options);

        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var feed = new TrattoriaFeed();
        return (new OnlineOrderDesk(orders, pizzas, options, feed), new PreOrderBook(orders, feed), orders, pizzas);
    }

    [Fact]
    public async Task an_online_order_is_a_real_factory_order_on_a_named_channel()
    {
        var (desk, _, orders, _) = Build();

        var ticket = await desk.PlaceRandomOrderAsync(T0);

        var order = Assert.Single(await orders.ListAsync());
        Assert.Equal(ticket.OrderId, order.Id);
        Assert.Contains(order.Channel, new[] { OrderChannel.Online, OrderChannel.Bot, OrderChannel.Copilot, OrderChannel.Phone });
        Assert.Equal(TicketState.Cooking, ticket.State);
    }

    [Fact]
    public async Task ready_online_orders_are_handed_over_and_completed()
    {
        var (desk, _, orders, _) = Build();
        var ticket = await desk.PlaceRandomOrderAsync(T0);

        var order = (await orders.ListAsync()).Single();
        await orders.UpdateAsync(order.Start().MarkReady());

        await desk.StepAsync(T0.AddSeconds(5));

        Assert.Equal(OrderState.Delivered, (await orders.ListAsync()).Single().State);
        Assert.Equal(TicketState.Done, desk.Tickets.Single(t => t.OrderId == ticket.OrderId).State);
    }

    [Fact]
    public void pre_orders_validate_at_the_boundary()
    {
        var (_, book, _, _) = Build();

        Assert.NotNull(book.TryAdd("Calzone", 2, T0.AddDays(1), "Anna", T0));          // not on the menu
        Assert.NotNull(book.TryAdd("Diavolo", 999, T0.AddDays(1), "Anna", T0));        // silly amount
        Assert.NotNull(book.TryAdd("Diavolo", 2, T0.AddHours(-1), "Anna", T0));        // in the past
        Assert.NotNull(book.TryAdd("Diavolo", 2, T0.AddDays(1), "  ", T0));            // nameless
        Assert.Null(book.TryAdd("diavolo", 10, T0.AddDays(1), "Nonna's Bingo Club", T0));

        var entry = Assert.Single(book.Upcoming);
        Assert.Equal("Diavolo", entry.Pizza);                                           // normalized casing
        Assert.Equal(10, entry.Amount);
    }

    [Fact]
    public async Task a_due_pre_order_fires_as_a_planned_factory_order()
    {
        var (_, book, orders, _) = Build();
        Assert.Null(book.TryAdd("Diavolo", 10, T0.AddMinutes(30), "Nonna's Bingo Club", T0));

        await book.StepAsync(T0.AddMinutes(10));
        Assert.Empty(await orders.ListAsync());                                         // not due yet

        await book.StepAsync(T0.AddMinutes(31));
        var order = Assert.Single(await orders.ListAsync());
        Assert.Equal(OrderChannel.Planned, order.Channel);
        Assert.Equal(10, order.Amount);
        Assert.Contains("Nonna", order.CustomerName);
        Assert.Empty(book.Upcoming);
    }
}
