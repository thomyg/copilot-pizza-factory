using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Factory;

/// <summary>
/// The pass: watches Started orders and calls them Ready the moment every pizza on the ticket
/// is out of the oven. Until this station existed, orders never completed — pizzas piled up at
/// Ready and tickets sat open forever. Delivery/serving (order → Delivered, pizzas → Out) is the
/// waiter's or the pickup desk's job, downstream of here.
///
/// It also keeps the pass clean. The dining room and the pickup desk only serve tickets they
/// still hold in memory, while orders and pizzas live in the store — so a restart leaves plated
/// food nobody remembers ordering. Those orphans used to sit at Ready forever and make the
/// kitchen look permanently backed up. Now they get written off, the way a real pass gets
/// scraped at the end of a shift.
/// </summary>
public sealed class Expeditor(
    IOrderRepository orders,
    IPizzaRepository pizzas,
    FactoryOptions options,
    ILogger<Expeditor> logger)
{
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await CallReadyAsync(cancellationToken);

        // The sweep reads the whole order book, so it runs on its own slower clock
        // rather than on every tick of the floor.
        if (now - _lastSweep >= options.PassSweepInterval)
        {
            _lastSweep = now;
            await ScrapeThePassAsync(now, cancellationToken);
        }
    }

    /// <summary>A ticket is ready when every pizza on it is out of the oven.</summary>
    private async Task CallReadyAsync(CancellationToken cancellationToken)
    {
        var started = await orders.GetByStateAsync(OrderState.Started, cancellationToken);
        if (started.Count == 0)
        {
            return;
        }

        var readyByOrder = (await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue, cancellationToken))
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

    /// <summary>
    /// Retires food nobody is coming for: pizzas whose ticket is already closed or gone, and
    /// tickets that have stood at the pass past <see cref="FactoryOptions.AbandonedAfter"/>.
    /// Deliberately one-way — this only ever moves work forward, never re-opens it.
    /// </summary>
    private async Task ScrapeThePassAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ready = await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue, cancellationToken);
        if (ready.Count == 0)
        {
            return;
        }

        var book = (await orders.ListAsync(cancellationToken)).ToDictionary(o => o.Id);

        // Tickets that have waited too long: nobody is left who remembers ordering them.
        var abandoned = book.Values
            .Where(o => o.State is OrderState.Ready or OrderState.Started)
            .Where(o => now - o.CreatedAt >= options.AbandonedAfter)
            .ToList();

        foreach (var order in abandoned)
        {
            await orders.UpdateAsync(order.MarkDelivered(), cancellationToken);
        }

        var closed = abandoned.Select(o => o.Id).ToHashSet();
        var scraped = 0;

        foreach (var pie in ready)
        {
            var orphaned = !book.TryGetValue(pie.OrderId, out var order) || order.State == OrderState.Delivered;
            if (orphaned || closed.Contains(pie.OrderId))
            {
                await pizzas.UpdateAsync(pie.SendOut(), cancellationToken);
                scraped++;
            }
        }

        if (scraped > 0)
        {
            logger.LogInformation(
                "Pass swept: {Pizzas} pizza(s) written off, {Orders} abandoned ticket(s) closed",
                scraped, abandoned.Count);
        }
    }
}
