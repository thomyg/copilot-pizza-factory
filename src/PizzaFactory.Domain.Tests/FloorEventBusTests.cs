using PizzaFactory.Domain.Floor;

namespace PizzaFactory.Domain.Tests;

public sealed class FloorEventBusTests
{
    private sealed class Clock(DateTimeOffset at) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = at;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 18, 30, 0, TimeSpan.Zero);

    private static FloorEventDraft Approved(string subject = "PO-2041") =>
        new("Nonna", FloorArea.Office, FloorEventKinds.PurchaseOrderApproved, subject, $"Approved {subject}");

    [Fact]
    public void every_event_gets_the_next_number_and_the_time_it_happened()
    {
        var clock = new Clock(T0);
        var bus = new FloorEventBus(clock);

        var first = bus.Publish(Approved("PO-1"));
        clock.Now = T0.AddSeconds(2);
        var second = bus.Publish(Approved("PO-2"));

        Assert.Equal(1, first.Seq);
        Assert.Equal(2, second.Seq);
        Assert.Equal(T0, first.At);
        Assert.Equal(T0.AddSeconds(2), second.At);
    }

    /// <summary>Most of the house runs itself; an event nobody claimed is the agents at work.</summary>
    [Fact]
    public void an_event_outside_any_request_is_autonomous()
    {
        var bus = new FloorEventBus(TimeProvider.System);

        var e = bus.Publish(Approved());

        Assert.Equal(FloorChannel.Autonomous, e.Channel);
    }

    [Fact]
    public void the_channel_of_the_surrounding_request_follows_the_event()
    {
        var bus = new FloorEventBus(TimeProvider.System);

        FloorEvent e;
        using (FloorChannelScope.Enter(FloorChannel.Copilot))
        {
            e = bus.Publish(Approved());
        }

        var after = bus.Publish(Approved());
        Assert.Equal(FloorChannel.Copilot, e.Channel);
        Assert.Equal(FloorChannel.Autonomous, after.Channel);
    }

    [Fact]
    public async Task the_channel_survives_an_await()
    {
        var bus = new FloorEventBus(TimeProvider.System);

        using var scope = FloorChannelScope.Enter(FloorChannel.Teams);
        await Task.Yield();
        var e = bus.Publish(Approved());

        Assert.Equal(FloorChannel.Teams, e.Channel);
    }

    [Fact]
    public void a_channel_named_on_the_event_beats_the_request()
    {
        var bus = new FloorEventBus(TimeProvider.System);

        using var scope = FloorChannelScope.Enter(FloorChannel.SharePoint);
        var e = bus.Publish(Approved() with { Channel = FloorChannel.Copilot });

        Assert.Equal(FloorChannel.Copilot, e.Channel);
    }

    [Fact]
    public void a_late_viewer_catches_up_from_the_last_number_it_saw()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        bus.Publish(Approved("PO-1"));
        var seen = bus.Publish(Approved("PO-2"));
        bus.Publish(Approved("PO-3"));

        var missed = bus.Since(seen.Seq);

        Assert.Equal(["PO-3"], missed.Select(e => e.Subject));
    }

    [Fact]
    public void the_bus_remembers_a_bounded_tail()
    {
        var bus = new FloorEventBus(TimeProvider.System, capacity: 3);
        for (var i = 1; i <= 5; i++)
        {
            bus.Publish(Approved($"PO-{i}"));
        }

        Assert.Equal(["PO-3", "PO-4", "PO-5"], bus.Since(0).Select(e => e.Subject));
    }

    [Fact]
    public void subscribers_hear_each_event_as_it_happens()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var heard = new List<string>();
        using var _ = bus.Subscribe(e => heard.Add(e.Subject));

        bus.Publish(Approved("PO-1"));
        bus.Publish(Approved("PO-2"));

        Assert.Equal(["PO-1", "PO-2"], heard);
    }

    [Fact]
    public void an_unsubscribed_viewer_hears_nothing_more()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var heard = 0;
        var subscription = bus.Subscribe(_ => heard++);

        bus.Publish(Approved());
        subscription.Dispose();
        bus.Publish(Approved());

        Assert.Equal(1, heard);
    }

    /// <summary>A stage tab that crashed must not stop the kitchen from reporting.</summary>
    [Fact]
    public void a_failing_subscriber_does_not_silence_the_others()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var heard = 0;
        using var bad = bus.Subscribe(_ => throw new InvalidOperationException("tab closed"));
        using var good = bus.Subscribe(_ => heard++);

        var e = bus.Publish(Approved());

        Assert.Equal(1, heard);
        Assert.Equal(1, e.Seq);
    }

    [Theory]
    [InlineData("copilot", FloorChannel.Copilot)]
    [InlineData("Teams", FloorChannel.Teams)]
    [InlineData("SHAREPOINT", FloorChannel.SharePoint)]
    [InlineData(" web ", FloorChannel.Web)]
    public void our_surfaces_name_their_channel_in_a_header(string header, FloorChannel expected)
    {
        Assert.Equal(expected, FloorChannelScope.Parse(header));
    }

    /// <summary>The header is a label for the stage, not a credential; junk is ignored, not trusted.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("autonomous")]
    [InlineData("presenter")]
    [InlineData("<script>")]
    [InlineData("42")]
    public void an_unknown_or_privileged_header_value_is_ignored(string? header)
    {
        Assert.Null(FloorChannelScope.Parse(header));
    }

    [Fact]
    public void the_last_number_is_known_without_reading_the_tail()
    {
        var bus = new FloorEventBus(TimeProvider.System, capacity: 1);

        Assert.Equal(0, bus.LastSeq);
        bus.Publish(Approved());
        bus.Publish(Approved());

        Assert.Equal(2, bus.LastSeq);
    }

    [Fact]
    public void an_event_text_is_trimmed_to_a_banner_not_a_novel()
    {
        var bus = new FloorEventBus(TimeProvider.System);

        var e = bus.Publish(Approved() with { Text = new string('x', 500) });

        Assert.True(e.Text.Length <= FloorEvent.MaxTextLength);
    }
}
