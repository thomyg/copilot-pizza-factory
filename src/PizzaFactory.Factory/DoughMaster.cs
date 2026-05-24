using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Factory;

/// <summary>
/// Keeps the fridge fed and rests dough: tops up a buffer of dough, promotes Waiting -> Resting up
/// to fridge capacity, and Resting -> Ready once each batch has rested long enough.
/// </summary>
public sealed class DoughMaster(IRestingDoughRepository doughs, FactoryOptions options, ILogger<DoughMaster> logger)
{
    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var waiting = await doughs.GetByStateAsync(DoughState.Waiting, cancellationToken);
        var resting = await doughs.GetByStateAsync(DoughState.Resting, cancellationToken);
        var ready = await doughs.GetByStateAsync(DoughState.Ready, cancellationToken);

        // Keep a buffer of dough in flight.
        var inFlight = waiting.Count + resting.Count + ready.Count;
        for (var i = inFlight; i < options.DoughBuffer; i++)
        {
            await doughs.AddAsync(RestingDough.FromRecipe(RecipeCatalog.NapolitanDough), cancellationToken);
        }

        // Promote Waiting -> Resting up to fridge capacity.
        var free = options.FridgeCapacity - resting.Count;
        if (free > 0)
        {
            var toRest = await doughs.GetByStateAsync(DoughState.Waiting, cancellationToken);
            foreach (var dough in toRest.Take(free))
            {
                await doughs.UpdateAsync(dough.BeginResting(now), cancellationToken);
            }
        }

        // Promote Resting -> Ready when rested.
        foreach (var dough in resting.Where(d => d.IsReady(now)))
        {
            await doughs.UpdateAsync(dough.MarkReady(), cancellationToken);
            logger.LogDebug("Dough {Id} ready", dough.Id);
        }
    }
}
