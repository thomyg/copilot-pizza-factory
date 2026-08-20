namespace PizzaFactory.BackOffice;

/// <summary>
/// The rota and the absence ledger — TrattoriaSoft's HR heart. Seeds a repeating weekly
/// plan (lunch: 1 pizzaiolo + 1 service; dinner: 1 pizzaiolo + 2 service + 1 courier),
/// records absences without recording reasons, and finds qualified cover by rule, not vibes:
/// right role, oven-certified where the slot demands it, not absent, not already working,
/// fewest shifts this week first (fairness).
/// </summary>
public sealed class StaffBook(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly List<Absence> _absences = [];
    private readonly Dictionary<(DateOnly Date, ShiftSlot Slot, StaffRole Role, int Seat), string> _overrides = [];

    public DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    /// <summary>The needs of one slot: (role, seats).</summary>
    private static IReadOnlyList<(StaffRole Role, int Seats)> Needs(ShiftSlot slot) =>
        slot == ShiftSlot.Lunch
            ? [(StaffRole.Pizzaiolo, 1), (StaffRole.Service, 1)]
            : [(StaffRole.Pizzaiolo, 1), (StaffRole.Service, 2), (StaffRole.Courier, 1)];

    /// <summary>The rota for a range of days, absences and overrides applied.</summary>
    public IReadOnlyList<RotaEntry> Rota(DateOnly from, int days)
    {
        lock (_gate)
        {
            var entries = new List<RotaEntry>();
            for (var d = 0; d < days; d++)
            {
                var date = from.AddDays(d);
                foreach (var slot in new[] { ShiftSlot.Lunch, ShiftSlot.Dinner })
                {
                    foreach (var (role, seats) in Needs(slot))
                    {
                        for (var seat = 0; seat < seats; seat++)
                        {
                            var assigned = AssignedUnsafe(date, slot, role, seat);
                            entries.Add(new RotaEntry(date, slot, role, assigned));
                        }
                    }
                }
            }

            return entries;
        }
    }

    /// <summary>
    /// Marks someone out (whole day when slot is null). Returns the rota seats that just
    /// became open because of it — the shifts Nonna now has to fill.
    /// </summary>
    public IReadOnlyList<RotaEntry> ReportAbsence(string name, DateOnly date, ShiftSlot? slot)
    {
        var member = Roster.Find(name) ?? throw new ArgumentException($"'{name}' is not on the roster.");
        lock (_gate)
        {
            _absences.Add(new Absence(member.Name, date, slot));

            var opened = new List<RotaEntry>();
            foreach (var s in slot is { } one ? [one] : new[] { ShiftSlot.Lunch, ShiftSlot.Dinner })
            {
                foreach (var (role, seats) in Needs(s))
                {
                    for (var seat = 0; seat < seats; seat++)
                    {
                        if (string.Equals(AssignedNameIgnoringAbsence(date, s, role, seat), member.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            opened.Add(new RotaEntry(date, s, role, null));
                        }
                    }
                }
            }

            return opened;
        }
    }

    /// <summary>Qualified, available cover for an open seat — best candidates first.</summary>
    public IReadOnlyList<StaffMember> FindCover(DateOnly date, ShiftSlot slot, StaffRole role)
    {
        lock (_gate)
        {
            var weekStart = date.AddDays(-(int)date.DayOfWeek);
            return Roster.Members
                .Where(m => m.Role == role || (role == StaffRole.Courier && m.Name == "Paolo"))
                .Where(m => role != StaffRole.Pizzaiolo || m.OvenCertified)
                .Where(m => !IsAbsentUnsafe(m.Name, date, slot))
                .Where(m => !IsWorkingUnsafe(m.Name, date, slot))
                .OrderBy(m => ShiftsInWeekUnsafe(m.Name, weekStart))
                .ThenBy(m => m.Name, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>Puts someone on an open seat. Validates qualification and availability.</summary>
    public RotaEntry Assign(string name, DateOnly date, ShiftSlot slot, StaffRole role)
    {
        var member = Roster.Find(name) ?? throw new ArgumentException($"'{name}' is not on the roster.");
        if (role == StaffRole.Pizzaiolo && !member.OvenCertified)
        {
            throw new InvalidOperationException($"{member.Name} is not oven-certified — the fire has rules.");
        }

        lock (_gate)
        {
            if (IsAbsentUnsafe(member.Name, date, slot))
            {
                throw new InvalidOperationException($"{member.Name} is out that day.");
            }

            var (seatRole, seats) = Needs(slot).First(n => n.Role == role);
            for (var seat = 0; seat < seats; seat++)
            {
                if (AssignedUnsafe(date, slot, role, seat) is null)
                {
                    _overrides[(date, slot, role, seat)] = member.Name;
                    return new RotaEntry(date, slot, role, member.Name);
                }
            }

            throw new InvalidOperationException($"No open {role} seat on {date:ddd d MMM} {slot} — the shift is covered.");
        }
    }

    public IReadOnlyList<Absence> Absences()
    {
        lock (_gate)
        {
            return [.. _absences];
        }
    }

    /* ------------------------------------------------ internals (call under _gate) */

    private string? AssignedUnsafe(DateOnly date, ShiftSlot slot, StaffRole role, int seat)
    {
        var name = AssignedNameIgnoringAbsence(date, slot, role, seat);
        return name is not null && IsAbsentUnsafe(name, date, slot) ? null : name;
    }

    /// <summary>The seeded weekly pattern (with any manual override) — before absence blanking.</summary>
    private string? AssignedNameIgnoringAbsence(DateOnly date, ShiftSlot slot, StaffRole role, int seat)
    {
        if (_overrides.TryGetValue((date, slot, role, seat), out var overridden))
        {
            return overridden;
        }

        // Deterministic rotation: day-of-year shifts the candidate list so the plan looks alive
        // but is fully reproducible for tests and demos.
        var pool = Roster.Members
            .Where(m => m.Role == role)
            .Where(m => role != StaffRole.Pizzaiolo || m.OvenCertified)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
        if (pool.Count == 0)
        {
            return null;
        }

        var rotation = (date.DayNumber + (int)slot) % pool.Count;
        return pool[(rotation + seat) % pool.Count].Name;
    }

    private bool IsAbsentUnsafe(string name, DateOnly date, ShiftSlot? slot) =>
        _absences.Any(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase) &&
            a.Date == date &&
            (a.Slot is null || slot is null || a.Slot == slot));

    private bool IsWorkingUnsafe(string name, DateOnly date, ShiftSlot slot) =>
        Needs(slot).Any(n =>
            Enumerable.Range(0, n.Seats).Any(seat =>
                string.Equals(AssignedNameIgnoringAbsence(date, slot, n.Role, seat), name, StringComparison.OrdinalIgnoreCase)));

    private int ShiftsInWeekUnsafe(string name, DateOnly weekStart)
    {
        var count = 0;
        for (var d = 0; d < 7; d++)
        {
            var date = weekStart.AddDays(d);
            foreach (var slot in new[] { ShiftSlot.Lunch, ShiftSlot.Dinner })
            {
                if (IsWorkingUnsafe(name, date, slot))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
