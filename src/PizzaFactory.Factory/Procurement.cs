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
public sealed class Procurement(
    IStockRepository stock,
    FactoryOptions options,
    ILogger<Procurement> logger,
    IPurchaseGate? purchases = null)
{
    // A drained silo (sabotage, rush) triggers a proper replenishment, not a nibble.
    private const int EmergencyMultiplier = 4;

    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);

        var refills = new List<IngredientQuantity>();
        foreach (var ingredient in Enum.GetValues<Ingredient>())
        {
            var grams = current.GramsOf(ingredient);
            if (grams > options.RestockThresholdGrams)
            {
                continue;
            }

            var amount = grams == 0 ? options.RestockAmountGrams * EmergencyMultiplier : options.RestockAmountGrams;
            if (purchases is not null && !purchases.RequestRefill(ingredient, amount,
                    grams == 0 ? "emergency replenishment (silo empty)" : null))
            {
                logger.LogInformation(
                    "Procurement: {Ingredient} order ({Grams}g) held for approval — the back office has the pen",
                    ingredient, amount);
                continue;
            }

            refills.Add(IngredientQuantity.Of(ingredient, amount));
        }

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
