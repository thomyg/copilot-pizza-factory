using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Mcp.Tests;

public class InMemoryStoreTests
{
    [Fact]
    public async Task order_repo_adds_lists_and_filters_by_state()
    {
        var repo = new InMemoryOrderRepository();
        await repo.AddAsync(Order.Create("Margherita", 1, OrderChannel.Guest));
        await repo.AddAsync(Order.Create("Diavolo", 2, OrderChannel.Online).Start());

        Assert.Equal(2, (await repo.ListAsync()).Count);
        Assert.Single(await repo.GetByStateAsync(OrderState.Created));
        Assert.Single(await repo.GetByStateAsync(OrderState.Started));
    }

    [Fact]
    public async Task order_repo_update_replaces_existing()
    {
        var repo = new InMemoryOrderRepository();
        var order = await repo.AddAsync(Order.Create("Funghi", 1, OrderChannel.Bot));

        await repo.UpdateAsync(order.Start());

        var reloaded = (await repo.ListAsync()).Single();
        Assert.Equal(OrderState.Started, reloaded.State);
    }

    [Fact]
    public async Task stock_repo_seeds_opening_stock_and_persists_saves()
    {
        var repo = new InMemoryStockRepository();

        var initial = await repo.GetAsync();
        Assert.Equal(250, initial.GramsOf(Ingredient.Pineapple));

        await repo.SaveAsync(initial.Refill([IngredientQuantity.Of(Ingredient.Pineapple, 250)]));

        Assert.Equal(500, (await repo.GetAsync()).GramsOf(Ingredient.Pineapple));
    }
}
