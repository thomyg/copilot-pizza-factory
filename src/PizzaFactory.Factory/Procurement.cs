using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Factory;

/// <summary>
/// Watches stock and auto-restocks any ingredient at/below the threshold. This is the baseline
/// keep-the-line-running behaviour; the Pineapple Crisis (separate bead) layers escalation /
/// A2A-to-Supplier on top instead of silently refilling.
/// </summary>
public sealed class Procurement(IStockRepository stock, FactoryOptions options, ILogger<Procurement> logger)
{
    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);

        var refills = Enum.GetValues<Ingredient>()
            .Where(i => current.GramsOf(i) <= options.RestockThresholdGrams)
            .Select(i => IngredientQuantity.Of(i, options.RestockAmountGrams))
            .ToList();

        if (refills.Count == 0)
        {
            return;
        }

        foreach (var refill in refills)
        {
            logger.LogInformation("Procurement: restocking {Ingredient} (+{Grams}g)", refill.Ingredient, refill.Grams);
        }

        await stock.SaveAsync(current.Refill(refills), cancellationToken);
    }
}
