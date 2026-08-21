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

            var emergency = grams == 0;
            var amount = emergency ? options.RestockAmountGrams * EmergencyMultiplier : options.RestockAmountGrams;

            if (purchases is null)
            {
                refills.Add(IngredientQuantity.Of(ingredient, amount));
                continue;
            }

            if (purchases.RequestRefill(ingredient, amount, emergency ? "emergency replenishment (silo empty)" : null))
            {
                refills.Add(IngredientQuantity.Of(ingredient, amount));
                continue;
            }

            logger.LogInformation(
                "Procurement: {Ingredient} order ({Grams}g) held for approval — the back office has the pen",
                ingredient, amount);

            // The big order now waits for a signature, which is the point. But an EMPTY silo
            // stops the line, and "agents stop the bleeding" has to survive contact with the
            // approval gate: fall back to one ordinary, auto-approvable refill so the kitchen
            // keeps working while the human decides on the bulk order. Without this the
            // emergency multiplier guarantees every drained silo exceeds the auto-approve
            // limit, the refill is never granted, and the factory starves waiting on Nonna.
            if (emergency && purchases.RequestRefill(ingredient, options.RestockAmountGrams, "stop-gap while the bulk order awaits approval"))
            {
                logger.LogInformation(
                    "Procurement: {Ingredient} stop-gap refill (+{Grams}g) keeps the line moving",
                    ingredient, options.RestockAmountGrams);
                refills.Add(IngredientQuantity.Of(ingredient, options.RestockAmountGrams));
            }
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
