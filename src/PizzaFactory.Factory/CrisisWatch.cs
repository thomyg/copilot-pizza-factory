using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Factory;

/// <summary>
/// Watches stock and raises an escalation the moment an ingredient crosses into crisis territory
/// (at/below the crisis threshold). Edge-triggered + de-duplicated: fires once per crisis, and
/// re-arms only after the ingredient recovers. This is demo beat A — the "pineapple's out, get a
/// human" moment — feeding whatever sink is wired (log now; Window + Teams later).
/// </summary>
public sealed class CrisisWatch(
    IStockRepository stock,
    IEscalationSink sink,
    FactoryOptions options,
    ILogger<CrisisWatch> logger)
{
    private readonly HashSet<Ingredient> _active = [];

    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);

        foreach (var ingredient in Enum.GetValues<Ingredient>())
        {
            var grams = current.GramsOf(ingredient);
            if (grams <= options.CrisisThresholdGrams)
            {
                if (_active.Add(ingredient)) // just crossed into crisis
                {
                    logger.LogWarning("Crisis: {Ingredient} critically low ({Grams}g)", ingredient, grams);
                    await sink.RaiseAsync(
                        new Escalation(ingredient, grams,
                            $"Low on {ingredient} — only {grams}g left. Restock or pause affected pizzas?", now),
                        cancellationToken);
                }
            }
            else
            {
                _active.Remove(ingredient); // recovered — re-arm for next time
            }
        }
    }
}
