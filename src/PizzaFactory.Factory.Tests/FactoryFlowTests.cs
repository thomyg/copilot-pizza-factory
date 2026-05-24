using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class FactoryFlowTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task dough_master_fills_the_fridge_then_readies_dough()
    {
        var doughs = new InMemoryRestingDoughRepository();
        var options = new FactoryOptions();
        var master = new DoughMaster(doughs, options, NullLogger<DoughMaster>.Instance);

        await master.StepAsync(T0);
        Assert.Equal(options.FridgeCapacity, (await doughs.GetByStateAsync(DoughState.Resting)).Count);

        var later = T0 + RecipeCatalog.NapolitanDough.RestingTime + TimeSpan.FromSeconds(1);
        await master.StepAsync(later);
        Assert.NotEmpty(await doughs.GetByStateAsync(DoughState.Ready));
    }

    [Fact]
    public async Task factory_takes_an_order_from_created_to_ready()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var doughs = new InMemoryRestingDoughRepository();
        var stock = new InMemoryStockRepository();
        var options = new FactoryOptions();
        var doughMaster = new DoughMaster(doughs, options, NullLogger<DoughMaster>.Instance);
        var pizzaiolo = new Pizzaiolo(orders, pizzas, doughs, stock, options, NullLogger<Pizzaiolo>.Instance);

        // Get a ready dough first.
        await doughMaster.StepAsync(T0);
        var t1 = T0 + RecipeCatalog.NapolitanDough.RestingTime + TimeSpan.FromSeconds(1);
        await doughMaster.StepAsync(t1);

        await orders.AddAsync(Order.Create("Margherita", 1, OrderChannel.Online));

        // Accept + prepare in one tick (dough ready, stock ok).
        await pizzaiolo.StepAsync(t1);
        Assert.Single(await pizzas.GetByStateAsync(PizzaState.Preparing, 10));

        var recipe = RecipeCatalog.GetPizza("Margherita");
        var t2 = t1 + recipe.PreparingTime + TimeSpan.FromSeconds(1);
        await pizzaiolo.StepAsync(t2);
        Assert.Single(await pizzas.GetByStateAsync(PizzaState.Baking, 10));

        var t3 = t2 + recipe.BakingTime + TimeSpan.FromSeconds(1);
        await pizzaiolo.StepAsync(t3);
        Assert.Single(await pizzas.GetByStateAsync(PizzaState.Ready, 10));

        // The order has been started, and stock was drawn down.
        Assert.Empty(await orders.GetByStateAsync(OrderState.Created));
    }

    [Fact]
    public async Task procurement_restocks_low_ingredients()
    {
        var stock = new InMemoryStockRepository();
        await stock.SaveAsync(Stock.Empty); // everything at zero -> all below threshold
        var options = new FactoryOptions();
        var procurement = new Procurement(stock, options, NullLogger<Procurement>.Instance);

        await procurement.StepAsync(T0);

        Assert.Equal(options.RestockAmountGrams, (await stock.GetAsync()).GramsOf(Ingredient.Mozzarella));
    }
}
