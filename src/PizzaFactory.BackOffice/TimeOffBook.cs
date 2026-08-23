namespace PizzaFactory.BackOffice;

public enum TimeOffState
{
    /// <summary>Filed and waiting on a manager. Cover has already been proposed.</summary>
    Pending,

    Approved,
    Declined,
}

/// <summary>
/// Someone asking for a day off, and everything the back office worked out about it before
/// a human looked: whether the shift is covered, by whom, and what happens if nobody can.
/// </summary>
public sealed record TimeOffRequest(
    string Id,
    string Name,
    DateOnly Date,
    ShiftSlot? Slot,
    string Reason,
    TimeOffState State,
    DateTimeOffset At,
    IReadOnlyList<string> ProposedCover,
    string? Note = null)
{
    /// <summary>Nobody qualified is free — approving this leaves the shift short.</summary>
    public bool LeavesAGap => ProposedCover.Count == 0;
}

/// <summary>
/// Time off, done the way a back office actually does it.
///
/// The difference between this and simply marking someone absent is the order of events. A
/// request arrives; the house works out who could cover it BEFORE anyone is asked to decide,
/// so the manager sees "Maria is out Friday dinner, Elena and Paolo can both take it" rather
/// than a yes/no with no information in it. Approving is the only step that touches the rota —
/// which is what makes the approval mean something.
///
/// Declining is a first-class outcome and keeps its reason. A back office that only records
/// the yeses cannot answer the question everyone eventually asks, which is why.
/// </summary>
public sealed class TimeOffBook(StaffBook staff, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly List<TimeOffRequest> _requests = [];
    private int _nextNumber = 500;

    /// <summary>
    /// Files a request and works out cover for it. Never changes the rota — that is the
    /// approver's doing, and keeping the two apart is the whole point of an approval.
    /// </summary>
    public TimeOffRequest Request(string name, DateOnly date, ShiftSlot? slot, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var person = Roster.Members.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

        // Cover is worked out BEFORE the absence is recorded, so the roster still believes the
        // requester is available — and would cheerfully propose them to cover their own shift.
        // Excluding them here keeps the absence out of the filing step, which is what makes
        // filing free of consequence.
        var cover = person is null
            ? []
            : SlotsOf(slot)
                .SelectMany(s => staff.FindCover(date, s, person.Role))
                .Select(m => m.Name)
                .Where(n => !string.Equals(n, person.Name, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var request = new TimeOffRequest(
            $"TO-{_nextNumber++}",
            person?.Name ?? name.Trim(),
            date,
            slot,
            string.IsNullOrWhiteSpace(reason) ? "not stated" : reason.Trim(),
            TimeOffState.Pending,
            _clock.GetUtcNow(),
            cover);

        lock (_gate)
        {
            _requests.Add(request);
        }

        return request;
    }

    /// <summary>
    /// Approves the request: records the absence and hands the shift to cover.
    ///
    /// A named substitute wins; otherwise the first qualified person the house proposed takes
    /// it. When nobody can, the day off is still granted and the shift is left visibly open —
    /// the alternative is a rota that lies, and an open slot is information a manager needs.
    /// </summary>
    public TimeOffRequest? Approve(string id, string? coverName = null)
    {
        TimeOffRequest request;
        lock (_gate)
        {
            var index = _requests.FindIndex(r => r.Id == id && r.State == TimeOffState.Pending);
            if (index < 0)
            {
                return null;
            }

            request = _requests[index];
        }

        var chosen = coverName ?? request.ProposedCover.FirstOrDefault();
        var person = Roster.Members.FirstOrDefault(m =>
            string.Equals(m.Name, request.Name, StringComparison.OrdinalIgnoreCase));

        staff.ReportAbsence(request.Name, request.Date, request.Slot);

        // Only fill seats the absence actually opened. Someone can perfectly well ask for a day
        // they were never rostered on, and handing that shift to a substitute would invent work
        // — StaffBook rightly refuses, so ask it first rather than catching its objection.
        var filled = new List<string>();
        if (chosen is not null && person is not null)
        {
            foreach (var s in SlotsOf(request.Slot))
            {
                if (HasOpenSeat(request.Date, s, person.Role))
                {
                    staff.Assign(chosen, request.Date, s, person.Role);
                    filled.Add(s.ToString());
                }
            }
        }

        var note = filled.Count > 0
            ? $"approved — {chosen} covers {string.Join(" and ", filled).ToLowerInvariant()}"
            : chosen is null && !request.LeavesAGap
                ? "approved — no cover named"
                : request.LeavesAGap
                    ? "approved — no qualified cover free, the shift is open"
                    : "approved — nothing to cover, they were not on the rota";

        return Settle(id, TimeOffState.Approved, note);
    }

    public TimeOffRequest? Decline(string id, string reason) =>
        Settle(id, TimeOffState.Declined, $"declined: {(string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason.Trim())}");

    public IReadOnlyList<TimeOffRequest> Requests(TimeOffState? state = null)
    {
        lock (_gate)
        {
            return [.. _requests.Where(r => state is null || r.State == state).OrderByDescending(r => r.At)];
        }
    }

    /// <summary>The sentence Nonna says when she puts this in front of someone.</summary>
    public static string Explain(TimeOffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var when = request.Slot is { } slot
            ? $"{request.Date:ddd d MMM} {slot.ToString().ToLowerInvariant()}"
            : $"{request.Date:ddd d MMM}, all day";

        return request.LeavesAGap
            ? $"{request.Name} asks for {when} ({request.Reason}). Nobody qualified is free — approving leaves the shift short."
            : $"{request.Name} asks for {when} ({request.Reason}). {Join(request.ProposedCover)} can cover.";
    }

    private bool HasOpenSeat(DateOnly date, ShiftSlot slot, StaffRole role) =>
        staff.Rota(date, 1).Any(e => e.Slot == slot && e.Role == role && e.AssignedTo is null);

    private static IEnumerable<ShiftSlot> SlotsOf(ShiftSlot? slot) =>
        slot is { } one ? [one] : Enum.GetValues<ShiftSlot>();

    private static string Join(IReadOnlyList<string> names) => names.Count switch
    {
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}",
    };

    private TimeOffRequest? Settle(string id, TimeOffState state, string note)
    {
        lock (_gate)
        {
            var index = _requests.FindIndex(r => r.Id == id && r.State == TimeOffState.Pending);
            if (index < 0)
            {
                return null;
            }

            var settled = _requests[index] with { State = state, Note = note };
            _requests[index] = settled;
            return settled;
        }
    }
}
