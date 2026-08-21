using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;

namespace PizzaFactory.Factory;

/// <summary>
/// The perpetuum mobile: ticks the stations on an interval. This replaces the legacy
/// Timer-driven IHostedServices with a single, testable, cancellation-aware loop.
///
/// It only runs while a service is open. A kitchen with nobody in the dining room does not
/// bake on speculation, and an unattended floor running around the clock is how this demo
/// came to report three and a half thousand orders in one day. The loop keeps ticking so the
/// window can close itself on time; it simply does no work in between.
/// </summary>
public sealed class FactoryWorker(
    DoughMaster doughMaster,
    Pizzaiolo pizzaiolo,
    Expeditor expeditor,
    Procurement procurement,
    CrisisWatch crisisWatch,
    ServiceWindow service,
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
            service.CloseIfExpired(now);
            if (!service.IsOpen)
            {
                continue;
            }

            try
            {
                await doughMaster.StepAsync(now, stoppingToken);
                await pizzaiolo.StepAsync(now, stoppingToken);
                await expeditor.StepAsync(now, stoppingToken);
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
