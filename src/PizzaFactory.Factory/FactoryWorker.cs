using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PizzaFactory.Factory;

/// <summary>
/// The perpetuum mobile: ticks the three stations on an interval. This replaces the legacy
/// Timer-driven IHostedServices with a single, testable, cancellation-aware loop.
/// </summary>
public sealed class FactoryWorker(
    DoughMaster doughMaster,
    Pizzaiolo pizzaiolo,
    Procurement procurement,
    CrisisWatch crisisWatch,
    FactoryOptions options,
    TimeProvider clock,
    ILogger<FactoryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Pizza Factory floor starting (tick every {Interval}).", options.TickInterval);
        using var timer = new PeriodicTimer(options.TickInterval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = clock.GetUtcNow();
            try
            {
                await doughMaster.StepAsync(now, stoppingToken);
                await pizzaiolo.StepAsync(now, stoppingToken);
                await procurement.StepAsync(now, stoppingToken);
                await crisisWatch.StepAsync(now, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Factory tick failed");
            }
        }
    }
}
