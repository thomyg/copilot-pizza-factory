using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PizzaFactory.Trattoria;

/// <summary>
/// Ticks the dining room: maître d' (arrivals, serving, departures), online desk, and the
/// pre-order book. Runs alongside the factory floor's worker — the restaurant is the demand
/// side of the same perpetuum mobile.
/// </summary>
public sealed class TrattoriaWorker(
    MaitreD maitreD,
    OnlineOrderDesk desk,
    PreOrderBook preOrders,
    TrattoriaOptions options,
    TimeProvider clock,
    ILogger<TrattoriaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trattoria front of house ready (tick every {Interval}).", options.TickInterval);
        using var timer = new PeriodicTimer(options.TickInterval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = clock.GetUtcNow();
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
}
