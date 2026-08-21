using PizzaFactory.Domain;

namespace PizzaFactory.Domain.Tests;

public sealed class ServiceWindowTests
{
    private sealed class Clock(DateTimeOffset at) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = at;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 18, 0, 0, TimeSpan.Zero);

    private static (ServiceWindow Window, Clock Clock) House(int minutes = 15)
    {
        var clock = new Clock(T0);
        return (new ServiceWindow(new ServiceWindowOptions { Duration = TimeSpan.FromMinutes(minutes) }, clock), clock);
    }

    [Fact]
    public void a_house_that_has_never_traded_is_shut_and_says_so()
    {
        var (window, _) = House();

        Assert.False(window.IsOpen);
        Assert.Null(window.Current);
        Assert.Null(window.Remaining);
    }

    [Fact]
    public void opening_starts_a_service_and_the_clock_on_it()
    {
        var (window, _) = House(minutes: 15);

        var session = window.Open();

        Assert.True(window.IsOpen);
        Assert.True(session.IsOpen);
        Assert.Equal(TimeSpan.FromMinutes(15), window.Remaining);
    }

    /// <summary>Pressing play twice is a presenter being careful, not an error.</summary>
    [Fact]
    public void opening_an_open_service_returns_the_same_one()
    {
        var (window, clock) = House();
        var first = window.Open();
        clock.Now = T0.AddMinutes(3);

        var second = window.Open();

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.OpenedAt, second.OpenedAt);
    }

    [Fact]
    public void closing_shuts_the_house_and_announces_it_once()
    {
        var (window, clock) = House();
        window.Open();
        var announced = new List<ServiceSession>();
        window.Closed += announced.Add;
        clock.Now = T0.AddMinutes(9);

        var closed = window.Close();

        Assert.False(window.IsOpen);
        Assert.NotNull(closed);
        Assert.Equal(TimeSpan.FromMinutes(9), closed!.Length(clock.Now));
        Assert.Null(window.Close());              // already shut — nothing to announce
        Assert.Single(announced);
    }

    /// <summary>
    /// The whole point: an unattended window closes itself. Everyone walks away from a demo,
    /// and a service left open is how this house came to report a day's takings of EUR 78,000.
    /// </summary>
    [Fact]
    public void a_forgotten_service_closes_itself_when_its_time_is_up()
    {
        var (window, clock) = House(minutes: 15);
        window.Open();

        clock.Now = T0.AddMinutes(14);
        Assert.Null(window.CloseIfExpired(clock.Now));
        Assert.True(window.IsOpen);

        clock.Now = T0.AddMinutes(15);
        var closed = window.CloseIfExpired(clock.Now);

        Assert.NotNull(closed);
        Assert.False(window.IsOpen);
    }

    [Fact]
    public void the_service_that_just_closed_is_still_the_one_we_report_on()
    {
        var (window, clock) = House();
        window.Open();
        clock.Now = T0.AddMinutes(20);
        window.Close();

        Assert.False(window.IsOpen);
        Assert.NotNull(window.Current);
        Assert.False(window.Current!.IsOpen);
        Assert.Equal(new DateOnly(2026, 8, 21), window.Current.Date);
    }
}
