using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class FactorySnapshotProviderTests
{
    [Fact]
    public async Task snapshot_reflects_orders_pizzas_dough_and_stock()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var doughs = new InMemoryRestingDoughRepository();
        var stock = new InMemoryStockRepository();
        var now = DateTimeOffset.UtcNow;

        var order = Order.Create("Hawaii", 3, OrderChannel.Guest);
        await orders.AddAsync(order);
        await pizzas.AddRangeAsync(
        [
            Pizza.FromOrder(order),
            Pizza.FromOrder(order).BeginPreparing(now),
            Pizza.FromOrder(order).BeginPreparing(now).BeginBaking(now),
        ]);

        var provider = new FactorySnapshotProvider(orders, pizzas, doughs, stock);
        var snapshot = await provider.GetAsync();

        Assert.Equal(1, snapshot.Ordered);
        Assert.Equal(1, snapshot.Preparing);
        Assert.Equal(1, snapshot.Baking);
        Assert.Equal(1, snapshot.OpenOrders);
        Assert.Equal(Enum.GetValues<Ingredient>().Length, snapshot.Stock.Count);
        Assert.Equal(250, snapshot.Stock.Single(s => s.Ingredient == nameof(Ingredient.Pineapple)).Grams);
    }
}
