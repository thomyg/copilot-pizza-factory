using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class CrisisWatchTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    private sealed class SpyEscalationSink : IEscalationSink
    {
        public List<Escalation> Raised { get; } = [];

        public Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default)
        {
            Raised.Add(escalation);
            return Task.CompletedTask;
        }
    }

    private static CrisisWatch Build(InMemoryStockRepository repo, SpyEscalationSink sink) =>
        new(repo, sink, new FactoryOptions(), NullLogger<CrisisWatch>.Instance);

    [Fact]
    public async Task no_escalation_when_well_stocked()
    {
        var repo = new InMemoryStockRepository(); // seeds opening stock (pineapple 250 > crisis 150)
        var sink = new SpyEscalationSink();

        await Build(repo, sink).StepAsync(T0);

        Assert.Empty(sink.Raised);
    }

    [Fact]
    public async Task crisis_fires_once_then_dedupes_until_recovery()
    {
        var repo = new InMemoryStockRepository();
        (await repo.GetAsync()).TryConsume([IngredientQuantity.Of(Ingredient.Pineapple, 200)], out var low, out _);
        await repo.SaveAsync(low); // pineapple = 50, everything else healthy

        var sink = new SpyEscalationSink();
        var watch = Build(repo, sink);

        await watch.StepAsync(T0);
        await watch.StepAsync(T0.AddSeconds(5)); // still low -> no duplicate

        Assert.Single(sink.Raised);
        Assert.Equal(Ingredient.Pineapple, sink.Raised[0].Ingredient);
        Assert.Equal(50, sink.Raised[0].Grams);

        // Recover above the threshold, then crash again -> a fresh escalation.
        await repo.SaveAsync((await repo.GetAsync()).Refill([IngredientQuantity.Of(Ingredient.Pineapple, 500)]));
        await watch.StepAsync(T0.AddSeconds(10)); // recovered -> re-arm
        (await repo.GetAsync()).TryConsume([IngredientQuantity.Of(Ingredient.Pineapple, 500)], out var low2, out _);
        await repo.SaveAsync(low2);
        await watch.StepAsync(T0.AddSeconds(15));

        Assert.Equal(2, sink.Raised.Count);
    }
}
