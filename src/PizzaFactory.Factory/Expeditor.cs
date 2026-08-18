using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Factory;

/// <summary>
/// The pass: watches Started orders and calls them Ready the moment every pizza on the ticket
/// is out of the oven. Until this station existed, orders never completed — pizzas piled up at
/// Ready and tickets sat open forever. Delivery/serving (order → Delivered, pizzas → Out) is the
/// waiter's or the pickup desk's job, downstream of here.
/// </summary>
public sealed class Expeditor(
    IOrderRepository orders,
    IPizzaRepository pizzas,
    ILogger<Expeditor> logger)
{
    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var started = await orders.GetByStateAsync(OrderState.Started, cancellationToken);
        if (started.Count == 0)
        {
            return;
        }

        var ready = await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue, cancellationToken);
        var readyByOrder = ready
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var order in started)
        {
            if (readyByOrder.GetValueOrDefault(order.Id) >= order.Amount)
            {
                await orders.UpdateAsync(order.MarkReady(), cancellationToken);
                logger.LogDebug("Order {Id} ({Amount}x {Item}) is ready at the pass", order.Id, order.Amount, order.ItemName);
            }
        }
    }
}
