using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Domain.Entities;

/// <summary>
/// A batch of dough resting in the fridge before it can be turned into pizzas.
/// Immutable: starting and finishing resting return new instances.
/// </summary>
public sealed record RestingDough
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required TimeSpan RestingTime { get; init; }
    public DoughState State { get; init; } = DoughState.Waiting;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishesAt { get; init; }

    public static RestingDough FromRecipe(DoughRecipe recipe) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Type = recipe.Name,
        RestingTime = recipe.RestingTime,
    };

    /// <summary>Put the dough into the fridge; computes when it will be ready.</summary>
    public RestingDough BeginResting(DateTimeOffset at) => this with
    {
        State = DoughState.Resting,
        StartedAt = at,
        FinishesAt = at + RestingTime,
    };

    /// <summary>True once the resting period has elapsed.</summary>
    public bool IsReady(DateTimeOffset now) =>
        State == DoughState.Resting && FinishesAt is { } finish && now >= finish;

    public RestingDough MarkReady() => this with { State = DoughState.Ready };

    public RestingDough MarkProcessed() => this with { State = DoughState.Processed };
}
