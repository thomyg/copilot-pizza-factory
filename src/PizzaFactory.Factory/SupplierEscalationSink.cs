using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Factory;

/// <summary>
/// The self-healing supply chain: when CrisisWatch raises an escalation, order the missing
/// ingredient from the external Supplier (over the A2A gateway) and apply the confirmed restock.
/// This closes demo beat A — "pineapple's out" → agent-to-agent reorder → line keeps moving.
/// </summary>
public sealed class SupplierEscalationSink(
    ISupplierGateway gateway,
    IStockRepository stock,
    FactoryOptions options,
    ILogger<SupplierEscalationSink> logger) : IEscalationSink
{
    public async Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default)
    {
        var quote = await gateway.RequestRestockAsync(escalation.Ingredient, options.RestockAmountGrams, cancellationToken);
        if (!quote.Confirmed)
        {
            logger.LogWarning("Supplier declined restock of {Ingredient}", escalation.Ingredient);
            return;
        }

        var current = await stock.GetAsync(cancellationToken);
        await stock.SaveAsync(current.Refill([IngredientQuantity.Of(escalation.Ingredient, quote.Grams)]), cancellationToken);

        logger.LogInformation("Self-heal: {Supplier} confirmed {Grams}g of {Ingredient} (ETA {Eta}s); restocked.",
            quote.Supplier, quote.Grams, escalation.Ingredient, quote.EtaSeconds);
    }
}

/// <summary>Fans an escalation out to several sinks (e.g. log + supplier self-heal + Window/Teams later).</summary>
public sealed class CompositeEscalationSink(IEnumerable<IEscalationSink> sinks) : IEscalationSink
{
    public async Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default)
    {
        foreach (var sink in sinks)
        {
            await sink.RaiseAsync(escalation, cancellationToken);
        }
    }
}
