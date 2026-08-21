namespace PizzaFactory.Domain;

/// <summary>One sitting of the house — from "chairs down" to "books closed".</summary>
public sealed record ServiceSession(string Id, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt)
{
    /// <summary>The calendar day the service belongs to, in local time.</summary>
    public DateOnly Date => DateOnly.FromDateTime(OpenedAt.ToLocalTime().Date);

    public bool IsOpen => ClosedAt is null;

    public TimeSpan Length(DateTimeOffset now) => (ClosedAt ?? now) - OpenedAt;
}

/// <summary>Pacing for a service window.</summary>
public sealed class ServiceWindowOptions
{
    public const string SectionName = "Service";

    /// <summary>
    /// How long a service runs before it closes itself. A demo is a quarter of an hour,
    /// not a working day — and an unattended window that never closes is exactly how the
    /// hosted demo ended up reporting 3,500 orders for a seventeen-table trattoria.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Whether the house is trading right now.
///
/// Everything that moves — the floor, the ovens, procurement, the maître d' — asks this
/// first and does nothing when it is shut. Between services the trattoria is not "broken"
/// or "idle": it is a restaurant that closed after lunch, with yesterday's takings booked,
/// a stocked pantry and a full reservation book. That is a real state, and showing it
/// honestly beats simulating a rush nobody is having.
///
/// A window closes itself after <see cref="ServiceWindowOptions.Duration"/>, because the
/// thing most likely to happen after a demo is that everyone walks away from it.
/// </summary>
public sealed class ServiceWindow(ServiceWindowOptions options, TimeProvider clock)
{
    private readonly Lock _gate = new();
    private ServiceSession? _current;

    /// <summary>Raised when a service closes, so the books can be written. Never on open.</summary>
    public event Action<ServiceSession>? Closed;

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _current?.IsOpen == true;
            }
        }
    }

    /// <summary>The service in progress, or the one that closed most recently. Null before the first ever.</summary>
    public ServiceSession? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Time left before the window closes itself; null when nothing is open.</summary>
    public TimeSpan? Remaining
    {
        get
        {
            lock (_gate)
            {
                if (_current?.IsOpen != true)
                {
                    return null;
                }

                var left = options.Duration - (clock.GetUtcNow() - _current.OpenedAt);
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }
    }

    /// <summary>Opens a service. Opening one that is already open just returns it — pressing play twice is not an error.</summary>
    public ServiceSession Open()
    {
        lock (_gate)
        {
            if (_current?.IsOpen == true)
            {
                return _current;
            }

            _current = new ServiceSession($"svc-{clock.GetUtcNow():yyyyMMdd-HHmmss}", clock.GetUtcNow(), null);
            return _current;
        }
    }

    /// <summary>Closes the service and announces it. Returns null when nothing was open.</summary>
    public ServiceSession? Close()
    {
        ServiceSession? closed;
        lock (_gate)
        {
            if (_current?.IsOpen != true)
            {
                return null;
            }

            closed = _current = _current with { ClosedAt = clock.GetUtcNow() };
        }

        Closed?.Invoke(closed);
        return closed;
    }

    /// <summary>Closes the window once its time is up. Called from the workers' tick.</summary>
    public ServiceSession? CloseIfExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_current?.IsOpen != true || now - _current.OpenedAt < options.Duration)
            {
                return null;
            }
        }

        return Close();
    }
}
