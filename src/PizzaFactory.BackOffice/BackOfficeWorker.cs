using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.BackOffice;

/// <summary>
/// TrattoriaSoft's delivery dock: applies approved purchase orders to the REAL pantry and
/// books the invoice. The human clicks approve; two seconds later the silo fills — and the
/// paper trail exists before the mozzarella does.
/// </summary>
public sealed class BackOfficeWorker(
    PurchaseBook purchases,
    IStockRepository stock,
    ILogger<BackOfficeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var order in purchases.ReadyForDelivery())
            {
                var current = await stock.GetAsync(stoppingToken);
                await stock.SaveAsync(
                    current.Refill([IngredientQuantity.Of(order.Ingredient, order.Grams)]), stoppingToken);
                purchases.MarkDelivered(order.Id);
                logger.LogInformation(
                    "TrattoriaSoft: {Order} delivered — {Grams}g {Ingredient}, €{Cost} invoiced to {Supplier}",
                    order.Id, order.Grams, order.Ingredient, order.Cost, order.Supplier);
            }
        }
    }
}
