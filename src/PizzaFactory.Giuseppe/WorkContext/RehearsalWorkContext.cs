namespace PizzaFactory.Giuseppe.WorkContext;

/// <summary>
/// Deterministic stand-in for Work IQ: always finds the Friday team retro. Used when live
/// Microsoft 365 context is unavailable (no user token, offline rehearsal, deployed bot
/// before SSO lands) so the demo storyline works everywhere, every time.
/// </summary>
public sealed class RehearsalWorkContext(TimeProvider? clock = null)
{
    private static readonly string[] RetroAttendees =
        ["Thomas", "Anna", "Lukas", "Sofia", "Markus", "Elena", "Jonas", "Petra"];

    private const string RetroDietaryNotes =
        "Anna is vegetarian — include at least one meat-free pizza (Margherita or Funghi).";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Task<MeetingContext?> FindMeetingAsync(string query, CancellationToken cancellationToken = default)
    {
        var meeting = new MeetingContext(
            Subject: "Team Retro",
            Start: NextFridayNoon(_clock.GetLocalNow()),
            Attendees: RetroAttendees,
            DietaryNotes: RetroDietaryNotes,
            Source: "rehearsal");

        return Task.FromResult<MeetingContext?>(meeting);
    }

    /// <summary>Next Friday 12:00 local; if it's already Friday before noon, that's today.</summary>
    internal static DateTimeOffset NextFridayNoon(DateTimeOffset now)
    {
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilFriday == 0 && now.Hour >= 12)
        {
            daysUntilFriday = 7;
        }

        var friday = now.Date.AddDays(daysUntilFriday).AddHours(12);
        return new DateTimeOffset(friday, now.Offset);
    }
}
