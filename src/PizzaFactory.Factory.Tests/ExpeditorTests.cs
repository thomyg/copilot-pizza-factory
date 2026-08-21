using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class ExpeditorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Order.CreatedAt stamps itself from a clock; the pass compares against it, so both must agree.</summary>
    private sealed class Clock(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }

    private static readonly TimeProvider AtT0 = new Clock(T0);

    [Fact]
    public async Task an_order_is_ready_only_when_every_pizza_on_the_ticket_is()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var expeditor = new Expeditor(orders, pizzas, new FactoryOptions(), NullLogger<Expeditor>.Instance);

        var order = await orders.AddAsync(Order.Create("Diavolo", 2, OrderChannel.Restaurant, "Table 9", AtT0));
        await orders.UpdateAsync(order.Start());
        var batch = new[] { Pizza.FromOrder(order), Pizza.FromOrder(order) };
        await pizzas.AddRangeAsync(batch);

        // One of two pizzas ready: the ticket stays open.
        await pizzas.UpdateAsync(batch[0].MarkReady(T0));
        await expeditor.StepAsync(T0);
        Assert.Equal(OrderState.Started, (await orders.ListAsync()).Single().State);

        // Both ready: the pass calls it.
        await pizzas.UpdateAsync(batch[1].MarkReady(T0));
        await expeditor.StepAsync(T0);
        Assert.Equal(OrderState.Ready, (await orders.ListAsync()).Single().State);
    }

    /// <summary>
    /// A restart strands plated food: the dining room forgets its parties, the store keeps the
    /// orders. Those pizzas used to sit at Ready forever and make the kitchen read as backed up.
    /// </summary>
    [Fact]
    public async Task the_pass_writes_off_food_nobody_is_coming_for()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var options = new FactoryOptions { AbandonedAfter = TimeSpan.FromMinutes(10) };
        var expeditor = new Expeditor(orders, pizzas, options, NullLogger<Expeditor>.Instance);

        var order = await orders.AddAsync(Order.Create("Margherita", 1, OrderChannel.Online, "Ghost", AtT0));
        await orders.UpdateAsync(order.Start());
        var pie = Pizza.FromOrder(order);
        await pizzas.AddRangeAsync([pie]);
        await pizzas.UpdateAsync(pie.MarkReady(T0));

        // Straight away it is simply a ticket at the pass — nothing to write off yet.
        await expeditor.StepAsync(T0);
        Assert.Equal(OrderState.Ready, (await orders.ListAsync()).Single().State);
        Assert.Single(await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue));

        // Long past the grace period, nobody came: the ticket closes and the pizza goes out.
        await expeditor.StepAsync(T0.AddHours(2));
        Assert.Equal(OrderState.Delivered, (await orders.ListAsync()).Single().State);
        Assert.Empty(await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue));
        Assert.Single(await pizzas.GetByStateAsync(PizzaState.Out, int.MaxValue));
    }

    [Fact]
    public async Task a_fresh_ticket_is_never_swept()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var expeditor = new Expeditor(orders, pizzas, new FactoryOptions(), NullLogger<Expeditor>.Instance);

        var order = await orders.AddAsync(Order.Create("Funghi", 1, OrderChannel.Restaurant, "Table 3", AtT0));
        await orders.UpdateAsync(order.Start());
        var pie = Pizza.FromOrder(order);
        await pizzas.AddRangeAsync([pie]);
        await pizzas.UpdateAsync(pie.MarkReady(T0));

        await expeditor.StepAsync(T0.AddMinutes(1));

        Assert.Equal(OrderState.Ready, (await orders.ListAsync()).Single().State);
        Assert.Single(await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue));
    }
}
