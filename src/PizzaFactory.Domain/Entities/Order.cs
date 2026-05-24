namespace PizzaFactory.Domain.Entities;

/// <summary>
/// A customer order for one or more of the same pizza. Immutable: lifecycle changes
/// return a new <see cref="Order"/> rather than mutating in place.
/// </summary>
public sealed record Order
{
    public required string Id { get; init; }
    public required string ItemName { get; init; }
    public required int Amount { get; init; }
    public required OrderChannel Channel { get; init; }
    public OrderState State { get; init; } = OrderState.Created;
    public string? CustomerName { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public static Order Create(
        string itemName,
        int amount,
        OrderChannel channel,
        string? customerName = null,
        TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new Order
        {
            Id = Guid.NewGuid().ToString("n"),
            ItemName = itemName,
            Amount = amount,
            Channel = channel,
            CustomerName = customerName,
            CreatedAt = (clock ?? TimeProvider.System).GetUtcNow(),
        };
    }

    public Order Start() => this with { State = OrderState.Started };
    public Order MarkReady() => this with { State = OrderState.Ready };
    public Order MarkDelivered() => this with { State = OrderState.Delivered };
}
