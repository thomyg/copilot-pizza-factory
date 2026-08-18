namespace PizzaFactory.Trattoria;

public enum PartyState
{
    Seated,
    WaitingForFood,
    Eating,
    Paying,
    Departed,
}

public sealed record PartyFeedback(int Stars, string Comment);

/// <summary>
/// A dining party working its way through the evening. Immutable — every transition returns a
/// new snapshot, mirroring the domain's style.
/// </summary>
public sealed record Party
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Size { get; init; }
    public required int TableId { get; init; }
    public PartyState State { get; init; } = PartyState.Seated;

    /// <summary>When the party entered its current state.</summary>
    public required DateTimeOffset SinceUtc { get; init; }

    /// <summary>When the food order was placed (for the waited-how-long verdict).</summary>
    public DateTimeOffset? OrderedAtUtc { get; init; }

    public string? OrderId { get; init; }
    public string? Wish { get; init; }
    public PartyFeedback? Feedback { get; init; }

    public Party Advance(PartyState next, DateTimeOffset at) => this with { State = next, SinceUtc = at };
}
