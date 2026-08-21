using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;

namespace PizzaFactory.Trattoria;

/// <summary>
/// Ticks the dining room: maître d' (arrivals, serving, departures), online desk, and the
/// pre-order book. Runs alongside the factory floor's worker — the restaurant is the demand
/// side of the same perpetuum mobile.
///
/// It also works the doors. The floor and the online counter follow the service window rather
/// than a flag of their own: opening the service unlocks both, closing it locks both, and the
/// worker re-checks every tick so the two can never drift apart. Between services it ticks
/// and does nothing, which is what a restaurant between services does.
/// </summary>
public sealed class TrattoriaWorker(
    MaitreD maitreD,
    OnlineOrderDesk desk,
    PreOrderBook preOrders,
    ServiceWindow service,
    TrattoriaOptions options,
    TimeProvider clock,
    ILogger<TrattoriaWorker> logger) : BackgroundService
{
    private bool _doorsOpen;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trattoria front of house ready (tick every {Interval}).", options.TickInterval);

        // The diary is full before the doors open — a house between services still has
        // tonight and the weekend spoken for.
        preOrders.SeedUpcoming(clock.GetUtcNow());

        using var timer = new PeriodicTimer(options.TickInterval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = clock.GetUtcNow();
            WorkTheDoors(now);

            if (!_doorsOpen)
            {
                continue;
            }

            try
            {
                await maitreD.StepAsync(now, stoppingToken);
                await desk.StepAsync(now, stoppingToken);
                await preOrders.StepAsync(now, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Trattoria tick failed");
            }
        }
    }

    /// <summary>Keeps the floor and the counter in step with the service window.</summary>
    private void WorkTheDoors(DateTimeOffset now)
    {
        var shouldBeOpen = service.IsOpen;
        if (shouldBeOpen == _doorsOpen)
        {
            return;
        }

        if (shouldBeOpen)
        {
            maitreD.OpenService(now);
            desk.Open();
            logger.LogInformation("Service open — chairs down, counter live.");
        }
        else
        {
            maitreD.CloseService(now);
            desk.Close();
            logger.LogInformation("Service closed — books shut, nobody new seated.");
        }

        _doorsOpen = shouldBeOpen;
    }
}
