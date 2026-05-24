using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Factory;

/// <summary>An ingredient's current level (for the stock gauges).</summary>
public sealed record IngredientLevel(string Ingredient, int Grams);

/// <summary>A point-in-time view of the factory floor — the data the live "Window" dashboard renders.</summary>
public sealed record FactorySnapshot(
    int Ordered,
    int Preparing,
    int Baking,
    int Ready,
    int OpenOrders,
    int DoughReady,
    IReadOnlyList<IngredientLevel> Stock,
    DateTimeOffset At);

/// <summary>
/// Builds a <see cref="FactorySnapshot"/> from the repositories. This is the live-feed source the
/// Window dashboard polls (over the Blazor Server circuit) — kept here so it's unit-tested.
/// </summary>
public sealed class FactorySnapshotProvider(
    IOrderRepository orders,
    IPizzaRepository pizzas,
    IRestingDoughRepository doughs,
    IStockRepository stock)
{
    public async Task<FactorySnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        async Task<int> Pizzas(PizzaState state) =>
            (await pizzas.GetByStateAsync(state, int.MaxValue, cancellationToken)).Count;

        var ordered = await Pizzas(PizzaState.OrderAccepted);
        var preparing = await Pizzas(PizzaState.Preparing);
        var baking = await Pizzas(PizzaState.Baking);
        var ready = await Pizzas(PizzaState.Ready);

        var created = (await orders.GetByStateAsync(OrderState.Created, cancellationToken)).Count;
        var started = (await orders.GetByStateAsync(OrderState.Started, cancellationToken)).Count;
        var doughReady = (await doughs.GetByStateAsync(DoughState.Ready, cancellationToken)).Count;

        var current = await stock.GetAsync(cancellationToken);
        var levels = Enum.GetValues<Ingredient>()
            .Select(i => new IngredientLevel(i.ToString(), current.GramsOf(i)))
            .ToList();

        return new FactorySnapshot(ordered, preparing, baking, ready, created + started, doughReady, levels, DateTimeOffset.UtcNow);
    }
}
