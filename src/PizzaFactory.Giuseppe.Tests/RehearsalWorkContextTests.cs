using PizzaFactory.Giuseppe.WorkContext;

namespace PizzaFactory.Giuseppe.Tests;

public class RehearsalWorkContextTests
{
    [Fact]
    public async Task finds_the_team_retro_with_attendees_and_dietary_notes()
    {
        // Monday 2026-08-17 → the retro lands on Friday 2026-08-21 at 12:00.
        var context = new RehearsalWorkContext(new FixedTimeProvider(new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero)));

        var meeting = await context.FindMeetingAsync("friday team retro");

        Assert.NotNull(meeting);
        Assert.Equal("Team Retro", meeting.Subject);
        Assert.Equal(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero), meeting.Start);
        Assert.Equal(8, meeting.Attendees.Count);
        Assert.Contains("vegetarian", meeting.DietaryNotes);
        Assert.Equal("rehearsal", meeting.Source);
    }

    [Fact]
    public async Task on_a_friday_morning_the_retro_is_today_at_noon()
    {
        var fridayMorning = new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);
        var context = new RehearsalWorkContext(new FixedTimeProvider(fridayMorning));

        var meeting = await context.FindMeetingAsync("retro");

        Assert.Equal(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero), meeting!.Start);
    }

    [Fact]
    public async Task on_a_friday_afternoon_the_retro_is_next_week()
    {
        var fridayAfternoon = new DateTimeOffset(2026, 8, 21, 15, 0, 0, TimeSpan.Zero);
        var context = new RehearsalWorkContext(new FixedTimeProvider(fridayAfternoon));

        var meeting = await context.FindMeetingAsync("retro");

        Assert.Equal(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero), meeting!.Start);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
