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

        // The empty silo triggered a 4× emergency request — and it was held, not applied.
        var request = Assert.Single(gate.Requests, r => r.Ingredient == Ingredient.Pineapple);
        Assert.Equal(options.RestockAmountGrams * 4, request.Grams);
        Assert.Equal(0, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
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
