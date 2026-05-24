using PizzaFactory.Factory;

namespace PizzaFactory.Web;

/// <summary>
/// In-memory feed for the Window's Trust &amp; Safety panel: a content-free count of guard blocks
/// (the "Bouncer") plus a short ticker of recent escalations / blocks. Thread-safe — the floor
/// writes from a background thread; the dashboard reads from the Blazor circuit.
/// </summary>
public sealed class WindowEventLog
{
    private const int MaxRecent = 12;
    private readonly Lock _gate = new();
    private readonly LinkedList<string> _recent = new();
    private int _blocked;

    public int BlockedCount
    {
        get { lock (_gate) { return _blocked; } }
    }

    public IReadOnlyList<string> Recent
    {
        get { lock (_gate) { return [.. _recent]; } }
    }

    /// <summary>Record a guard block. Pass only the safe reason/category — never the offending text.</summary>
    public void RecordBlock(string reason)
    {
        lock (_gate)
        {
            _blocked++;
            Add($"🛡️ blocked — {reason}");
        }
    }

    public void RecordEscalation(string message)
    {
        lock (_gate)
        {
            Add($"📣 {message}");
        }
    }

    private void Add(string entry)
    {
        _recent.AddFirst($"{DateTimeOffset.Now:HH:mm:ss}  {entry}");
        while (_recent.Count > MaxRecent)
        {
            _recent.RemoveLast();
        }
    }
}

/// <summary>Escalation sink that feeds the Window ticker (composed alongside log + supplier sinks).</summary>
public sealed class WindowEscalationSink(WindowEventLog log) : IEscalationSink
{
    public Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default)
    {
        log.RecordEscalation($"{escalation.Ingredient} critically low ({escalation.Grams}g) — reordering from supplier");
        return Task.CompletedTask;
    }
}
