namespace PizzaFactory.Trattoria;

public sealed record FeedEntry(DateTimeOffset At, string Text);

/// <summary>
/// The dining room's live ticker: arrivals, orders, servings, wishes, reviews, walkouts.
/// Thread-safe — the simulation writes from its worker, the dashboard reads from the circuit.
/// </summary>
public sealed class TrattoriaFeed
{
    private const int MaxEntries = 30;
    private readonly Lock _gate = new();
    private readonly LinkedList<FeedEntry> _entries = new();

    public IReadOnlyList<FeedEntry> Recent
    {
        get { lock (_gate) { return [.. _entries]; } }
    }

    public void Post(DateTimeOffset at, string text)
    {
        lock (_gate)
        {
            _entries.AddFirst(new FeedEntry(at, text));
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveLast();
            }
        }
    }
}
