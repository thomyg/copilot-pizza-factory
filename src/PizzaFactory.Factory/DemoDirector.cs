using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Factory;

/// <summary>
/// The presenter's levers. Everything the Engine Room's chaos console does goes through here so
/// it's unit-tested and UI-free: drain an ingredient to force the crisis storyline, nudge stock
/// up/down, flood the floor with a lunch rush, or reset the pantry. These are the SAME repositories
/// the autonomous floor runs on — no mocks, no smoke: sabotage is real sabotage.
/// </summary>
public sealed class DemoDirector(IOrderRepository orders, IStockRepository stock)
{
    /// <summary>Upper bound for one rush — enough to swamp the ovens, not enough to swamp the demo.</summary>
    public const int MaxRushOrders = 100;

    private static readonly string[] RushCrowd =
    [
        "Hungry Dev", "Standup Survivor", "Scrum Lord", "Merge Conflict", "Deadline Dan",
        "Backlog Betty", "Sprint Goblin", "Retro Rita", "Hotfix Hank", "Demo Gremlin",
    ];

    /// <summary>Drain an ingredient to zero. Returns the grams removed (0 if it was already empty).</summary>
    public async Task<int> SabotageAsync(Ingredient ingredient, CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);
        var grams = current.GramsOf(ingredient);
        if (grams <= 0)
        {
            return 0;
        }

        if (current.TryConsume([new IngredientQuantity(ingredient, grams)], out var drained, out _))
        {
            await stock.SaveAsync(drained, cancellationToken);
        }

        return grams;
    }

    /// <summary>Nudge an ingredient up or down by <paramref name="deltaGrams"/>, clamped at zero.</summary>
    public async Task<Stock> AdjustAsync(Ingredient ingredient, int deltaGrams, CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);

        Stock next;
        if (deltaGrams >= 0)
        {
            next = current.Refill([new IngredientQuantity(ingredient, deltaGrams)]);
        }
        else
        {
            var removable = Math.Min(current.GramsOf(ingredient), -deltaGrams);
            next = removable > 0 && current.TryConsume([new IngredientQuantity(ingredient, removable)], out var consumed, out _)
                ? consumed
                : current;
        }

        await stock.SaveAsync(next, cancellationToken);
        return next;
    }

    /// <summary>Reset the pantry to the factory's standard opening stock.</summary>
    public async Task<Stock> RestockAsync(CancellationToken cancellationToken = default)
    {
        var fresh = Stock.Initial();
        await stock.SaveAsync(fresh, cancellationToken);
        return fresh;
    }

    /// <summary>
    /// The lunch crowd descends: places <paramref name="count"/> random one-or-two-pizza orders
    /// (clamped to 1..<see cref="MaxRushOrders"/>). Returns how many orders were placed.
    /// </summary>
    public async Task<int> RushHourAsync(int count, CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(count, 1, MaxRushOrders);
        for (var i = 0; i < clamped; i++)
        {
            var pizza = RecipeCatalog.Menu[Random.Shared.Next(RecipeCatalog.Menu.Count)];
            var amount = Random.Shared.Next(1, 3);
            var name = $"{RushCrowd[Random.Shared.Next(RushCrowd.Length)]} #{i + 1}";

            await orders.AddAsync(Order.Create(pizza, amount, OrderChannel.Guest, name), cancellationToken);
        }

        return clamped;
    }
}
