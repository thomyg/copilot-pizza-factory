namespace PizzaFactory.Domain.Floor;

/// <summary>Where the house reports what just happened. One per process.</summary>
public interface IFloorEvents
{
    FloorEvent Publish(FloorEventDraft draft);
}

/// <summary>
/// The floor's event bus: numbers each event, stamps it with time and channel, keeps a short
/// tail for viewers that join late, and tells live subscribers straight away. Until now every
/// screen in the house polled and diffed; the stage needs to know *that* something happened
/// and *who asked for it*, which a diff of counts cannot tell it.
///
/// In-process by design: the Web app is a single instance and holds the trattoria and back
/// office in memory anyway. The MCP server, a separate process, reaches it over HTTP.
/// </summary>
public sealed class FloorEventBus(TimeProvider clock, int capacity = 200) : IFloorEvents
{
    private readonly Lock _gate = new();
    private readonly Queue<FloorEvent> _tail = new();
    private readonly List<Action<FloorEvent>> _subscribers = [];
    private long _seq;

    public FloorEvent Publish(FloorEventDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        FloorEvent e;
        Action<FloorEvent>[] listeners;
        lock (_gate)
        {
            e = new FloorEvent(
                ++_seq,
                clock.GetUtcNow(),
                draft.Actor,
                draft.Channel ?? FloorChannelScope.Current ?? FloorChannel.Autonomous,
                draft.Area,
                draft.Kind,
                draft.Subject,
                Trim(draft.Text));
            _tail.Enqueue(e);
            while (_tail.Count > capacity)
            {
                _tail.Dequeue();
            }

            listeners = [.. _subscribers];
        }

        // Outside the lock: a slow stage tab must not hold up the kitchen.
        foreach (var listener in listeners)
        {
            try
            {
                listener(e);
            }
            catch (Exception)
            {
                // A viewer that fails is that viewer's problem. The floor keeps reporting.
            }
        }

        return e;
    }

    /// <summary>The number of the most recent event; 0 before the first.</summary>
    public long LastSeq
    {
        get
        {
            lock (_gate)
            {
                return _seq;
            }
        }
    }

    /// <summary>Everything still in the tail after <paramref name="seq"/>, oldest first.</summary>
    public IReadOnlyList<FloorEvent> Since(long seq)
    {
        lock (_gate)
        {
            return [.. _tail.Where(e => e.Seq > seq)];
        }
    }

    public IDisposable Subscribe(Action<FloorEvent> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_gate)
        {
            _subscribers.Add(listener);
        }

        return new Subscription(this, listener);
    }

    private void Unsubscribe(Action<FloorEvent> listener)
    {
        lock (_gate)
        {
            _subscribers.Remove(listener);
        }
    }

    private static string Trim(string text) =>
        text.Length <= FloorEvent.MaxTextLength ? text : string.Concat(text.AsSpan(0, FloorEvent.MaxTextLength - 1), "…");

    private sealed class Subscription(FloorEventBus bus, Action<FloorEvent> listener) : IDisposable
    {
        public void Dispose() => bus.Unsubscribe(listener);
    }
}

/// <summary>For a house with no stage — tests, tools, the MCP process. Reports go nowhere.</summary>
public sealed class NullFloorEvents : IFloorEvents
{
    public static readonly NullFloorEvents Instance = new();

    private NullFloorEvents()
    {
    }

    public FloorEvent Publish(FloorEventDraft draft) =>
        new(0, DateTimeOffset.UtcNow, draft.Actor, draft.Channel ?? FloorChannelScope.Current ?? FloorChannel.Autonomous,
            draft.Area, draft.Kind, draft.Subject, draft.Text);
}
