using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class ExpeditorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task an_order_is_ready_only_when_every_pizza_on_the_ticket_is()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var expeditor = new Expeditor(orders, pizzas, NullLogger<Expeditor>.Instance);

        var order = await orders.AddAsync(Order.Create("Diavolo", 2, OrderChannel.Restaurant, "Table 9"));
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
}
