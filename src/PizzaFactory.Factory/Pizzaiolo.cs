using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Factory;

/// <summary>
/// The cook: turns Created orders into pizzas, then walks each pizza across the floor —
/// OrderAccepted -> Preparing (consuming a ready dough + stock) -> Baking -> Ready — respecting
/// prep/oven capacity and the recipe's prep/bake times.
/// </summary>
public sealed class Pizzaiolo(
    IOrderRepository orders,
    IPizzaRepository pizzas,
    IRestingDoughRepository doughs,
    IStockRepository stock,
    FactoryOptions options,
    ILogger<Pizzaiolo> logger)
{
    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await AcceptOrdersAsync(cancellationToken);
        await PreparePizzasAsync(now, cancellationToken);
        await BakePizzasAsync(now, cancellationToken);
        await FinishPizzasAsync(now, cancellationToken);
    }

    private async Task AcceptOrdersAsync(CancellationToken cancellationToken)
    {
        foreach (var order in await orders.GetByStateAsync(OrderState.Created, cancellationToken))
        {
            var batch = Enumerable.Range(0, order.Amount).Select(_ => Pizza.FromOrder(order)).ToList();
            await pizzas.AddRangeAsync(batch, cancellationToken);
            await orders.UpdateAsync(order.Start(), cancellationToken);
            logger.LogDebug("Accepted order {Id}: {Amount}x {Item}", order.Id, order.Amount, order.ItemName);
        }
    }

    private async Task PreparePizzasAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var preparing = await pizzas.GetByStateAsync(PizzaState.Preparing, int.MaxValue, cancellationToken);
        var free = options.PrepCapacity - preparing.Count;
        if (free <= 0)
        {
            return;
        }

        foreach (var pizza in await pizzas.GetByStateAsync(PizzaState.OrderAccepted, free, cancellationToken))
        {
            var readyDough = (await doughs.GetByStateAsync(DoughState.Ready, cancellationToken)).FirstOrDefault();
            if (readyDough is null)
            {
                break; // no dough ready yet — try again next tick
            }

            var recipe = RecipeCatalog.GetPizza(pizza.Name);
            var current = await stock.GetAsync(cancellationToken);
            if (!current.TryConsume(recipe.Toppings, out var updated, out var missing))
            {
                logger.LogInformation("Short on {Ingredient} for {Pizza}", missing, pizza.Name);
                continue; // procurement / crisis handles the shortfall
            }

            await stock.SaveAsync(updated, cancellationToken);
            await doughs.UpdateAsync(readyDough.MarkProcessed(), cancellationToken);
            await pizzas.UpdateAsync(pizza.BeginPreparing(now), cancellationToken);
        }
    }

    private async Task BakePizzasAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var baking = await pizzas.GetByStateAsync(PizzaState.Baking, int.MaxValue, cancellationToken);
        var free = options.OvenCapacity - baking.Count;
        if (free <= 0)
        {
            return;
        }

        foreach (var pizza in await pizzas.GetByStateAsync(PizzaState.Preparing, int.MaxValue, cancellationToken))
        {
            if (free <= 0)
            {
                break;
            }

            var recipe = RecipeCatalog.GetPizza(pizza.Name);
            if (pizza.StartedAt is { } start && now - start >= recipe.PreparingTime)
            {
                await pizzas.UpdateAsync(pizza.BeginBaking(now), cancellationToken);
                free--;
            }
        }
    }

    private async Task FinishPizzasAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var pizza in await pizzas.GetByStateAsync(PizzaState.Baking, int.MaxValue, cancellationToken))
        {
            var recipe = RecipeCatalog.GetPizza(pizza.Name);
            if (pizza.StartedAt is { } start && now - start >= recipe.BakingTime)
            {
                await pizzas.UpdateAsync(pizza.MarkReady(now), cancellationToken);
                logger.LogDebug("Pizza {Id} ({Name}) ready", pizza.Id, pizza.Name);
            }
        }
    }
}
