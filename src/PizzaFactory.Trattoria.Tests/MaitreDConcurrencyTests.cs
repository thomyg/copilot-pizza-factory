using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

/// <summary>
/// The stage reads the dining room several times a second while the tick seats and serves.
/// A snapshot taken mid-tick must never throw or see a torn table map.
/// </summary>
public sealed class MaitreDConcurrencyTests
{
    [Fact]
    public async Task snapshots_taken_while_the_room_turns_over_never_fail()
    {
        var options = new TrattoriaOptions
        {
            RandomSeed = 7,
            ArrivalChancePerTick = 1.0,
            OrderingDelay = TimeSpan.Zero,
            EatingDuration = TimeSpan.Zero,
            PayingDuration = TimeSpan.Zero,
        };
        var maitreD = new MaitreD(new InMemoryOrderRepository(), new InMemoryPizzaRepository(), options, new TrattoriaFeed());
        var t = new DateTimeOffset(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);
        maitreD.OpenService(t);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var reading = new ManualResetEventSlim();
        var reader = Task.Run(() =>
        {
            var reads = 0;
            while (!cts.IsCancellationRequested)
            {
                reading.Set();
                var snap = maitreD.Snapshot();
                Assert.Equal(17, snap.Tables.Count);
                reads++;
            }

            return reads;
        });

        Assert.True(reading.Wait(TimeSpan.FromSeconds(5)), "the reader never started");
        for (var i = 0; i < 400 && !cts.IsCancellationRequested; i++)
        {
            await maitreD.StepAsync(t.AddSeconds(i));
            if (i % 50 == 49)
            {
                maitreD.EndOfService(t.AddSeconds(i));
            }
        }

        await cts.CancelAsync();
        Assert.True(await reader > 0);
    }
}
