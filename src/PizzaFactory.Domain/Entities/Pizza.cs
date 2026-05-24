namespace PizzaFactory.Domain.Entities;

/// <summary>
/// A single pizza moving across the factory floor. Immutable: each station transition
/// returns a new <see cref="Pizza"/>.
/// </summary>
public sealed record Pizza
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string OrderId { get; init; }
    public PizzaState State { get; init; } = PizzaState.OrderAccepted;
    public string? CustomerName { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? ReadyAt { get; init; }

    public static Pizza FromOrder(Order order) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Name = order.ItemName,
        OrderId = order.Id,
        CustomerName = order.CustomerName,
    };

    public Pizza BeginPreparing(DateTimeOffset at) =>
        this with { State = PizzaState.Preparing, StartedAt = at };

    // StartedAt tracks the current station phase's start, so the Pizzaiolo can time baking.
    public Pizza BeginBaking(DateTimeOffset at) =>
        this with { State = PizzaState.Baking, StartedAt = at };

    public Pizza MarkReady(DateTimeOffset at) =>
        this with { State = PizzaState.Ready, ReadyAt = at };

    public Pizza SendOut() => this with { State = PizzaState.Out };
}
