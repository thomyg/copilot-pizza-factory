using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

/// <summary>
/// Orders Giuseppe places through the MCP server (Copilot, Teams) reach the house as plain
/// orders. Tracking one gives it a ticket at the counter, so it is handed over when ready
/// instead of waiting at the pass until the expeditor writes it off.
/// </summary>
public sealed class TrackedOrderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

    private static (OnlineOrderDesk Desk, InMemoryOrderRepository Orders) Build()
    {
        var orders = new InMemoryOrderRepository();
        var options = new TrattoriaOptions { RandomSeed = 3, HandoverDelay = TimeSpan.Zero };
        return (new OnlineOrderDesk(orders, new InMemoryPizzaRepository(), options, new TrattoriaFeed()), orders);
    }

    [Fact]
    public async Task a_tracked_order_is_handed_over_when_ready()
    {
        var (desk, orders) = Build();
        var order = await orders.AddAsync(Order.Create("Diavolo", 2, OrderChannel.Copilot, "Retro team"));

        desk.Track(order, FulfilmentMode.Delivery, "Retro team", T0);
        await orders.UpdateAsync(order.Start().MarkReady());
        await desk.StepAsync(T0.AddSeconds(5));

        var ticket = Assert.Single(desk.Tickets);
        Assert.Equal(TicketState.Done, ticket.State);
        Assert.Equal(FulfilmentMode.Delivery, ticket.Mode);
        Assert.Equal(OrderChannel.Copilot, ticket.Channel);
        Assert.Equal(OrderState.Delivered, (await orders.ListAsync()).Single().State);
    }

    [Fact]
    public void tracking_the_same_order_twice_keeps_one_ticket()
    {
        var (desk, _) = Build();
        var order = Order.Create("Margherita", 1, OrderChannel.Bot);

        desk.Track(order, FulfilmentMode.Takeaway, "Anna", T0);
        desk.Track(order, FulfilmentMode.Takeaway, "Anna", T0);

        Assert.Single(desk.Tickets);
    }
}
