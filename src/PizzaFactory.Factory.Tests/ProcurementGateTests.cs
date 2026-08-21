using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public sealed class ProcurementGateTests
{
    private sealed class RecordingGate(bool allow) : IPurchaseGate
    {
        public List<(Ingredient Ingredient, int Grams)> Requests { get; } = [];

        public bool RequestRefill(Ingredient ingredient, int grams, string? note = null)
        {
            Requests.Add((ingredient, grams));
            return allow;
        }
    }

    /// <summary>Stands in for TrattoriaSoft: small refills pass, big ones need a signature.</summary>
    private sealed class LimitedGate(int autoApproveLimitGrams) : IPurchaseGate
    {
        public bool RequestRefill(Ingredient ingredient, int grams, string? note = null) =>
            grams <= autoApproveLimitGrams;
    }

    [Fact]
    public async Task a_denying_gate_holds_the_refill_and_the_silo_stays_low()
    {
        var stock = new InMemoryStockRepository();
        var options = new FactoryOptions();
        var current = await stock.GetAsync();
        current.TryConsume([IngredientQuantity.Of(Ingredient.Pineapple, current.GramsOf(Ingredient.Pineapple))], out var drained, out _);
        await stock.SaveAsync(drained);
        var gate = new RecordingGate(allow: false);
        var procurement = new Procurement(stock, options, NullLogger<Procurement>.Instance, gate);

        await procurement.StepAsync(DateTimeOffset.UtcNow);

        // The empty silo asks twice: the 4× emergency order, then an ordinary stop-gap
        // to keep the line moving while a human considers the big one. This gate refuses
        // both, so nothing is applied and the silo stays empty.
        var asked = gate.Requests.Where(r => r.Ingredient == Ingredient.Pineapple).ToList();
        Assert.Equal(
            [options.RestockAmountGrams * 4, options.RestockAmountGrams],
            asked.Select(r => r.Grams));
        Assert.Equal(0, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    /// <summary>
    /// The gate that matters in practice: TrattoriaSoft auto-approves small refills and
    /// parks anything bigger. Before the stop-gap existed, an empty silo always asked for
    /// 4× the restock amount, always exceeded the limit, and therefore never refilled —
    /// the factory starved itself waiting on a signature it could not give.
    /// </summary>
    [Fact]
    public async Task an_empty_silo_still_gets_a_stop_gap_when_only_the_bulk_order_is_too_big()
    {
        var stock = new InMemoryStockRepository();
        var options = new FactoryOptions();
        var current = await stock.GetAsync();
        current.TryConsume([IngredientQuantity.Of(Ingredient.Pineapple, current.GramsOf(Ingredient.Pineapple))], out var drained, out _);
        await stock.SaveAsync(drained);
        var gate = new LimitedGate(options.RestockAmountGrams);
        var procurement = new Procurement(stock, options, NullLogger<Procurement>.Instance, gate);

        await procurement.StepAsync(DateTimeOffset.UtcNow);

        Assert.Equal(options.RestockAmountGrams, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    [Fact]
    public async Task an_allowing_gate_keeps_the_factory_autonomous()
    {
        var stock = new InMemoryStockRepository();
        var options = new FactoryOptions();
        var current = await stock.GetAsync();
        var pineapple = current.GramsOf(Ingredient.Pineapple);
        current.TryConsume([IngredientQuantity.Of(Ingredient.Pineapple, pineapple - 100)], out var lowered, out _);
        await stock.SaveAsync(lowered);
        var gate = new RecordingGate(allow: true);
        var procurement = new Procurement(stock, options, NullLogger<Procurement>.Instance, gate);

        await procurement.StepAsync(DateTimeOffset.UtcNow);

        Assert.Equal(100 + options.RestockAmountGrams, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    [Fact]
    public async Task no_gate_means_the_old_fully_autonomous_behaviour()
    {
        var stock = new InMemoryStockRepository();
        var options = new FactoryOptions();
        var current = await stock.GetAsync();
        var pineapple = current.GramsOf(Ingredient.Pineapple);
        current.TryConsume([IngredientQuantity.Of(Ingredient.Pineapple, pineapple)], out var emptied, out _);
        await stock.SaveAsync(emptied);
        var procurement = new Procurement(stock, options, NullLogger<Procurement>.Instance);

        await procurement.StepAsync(DateTimeOffset.UtcNow);

        Assert.Equal(options.RestockAmountGrams * 4, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }
}
