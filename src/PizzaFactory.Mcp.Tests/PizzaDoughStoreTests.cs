using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Mcp.Tests;

public class PizzaDoughStoreTests
{
    [Fact]
    public async Task pizza_repo_adds_filters_by_state_and_respects_take()
    {
        var repo = new InMemoryPizzaRepository();
        var order = Order.Create("Margherita", 1, OrderChannel.Online);
        var p1 = Pizza.FromOrder(order);
        var p2 = Pizza.FromOrder(order);

        await repo.AddRangeAsync([p1, p2]);

        var accepted = await repo.GetByStateAsync(PizzaState.OrderAccepted, take: 10);
        Assert.Equal(2, accepted.Count);

        Assert.Single(await repo.GetByStateAsync(PizzaState.OrderAccepted, take: 1));
    }

    [Fact]
    public async Task pizza_repo_update_moves_state()
    {
        var repo = new InMemoryPizzaRepository();
        var pizza = Pizza.FromOrder(Order.Create("Diavolo", 1, OrderChannel.Bot));
        await repo.AddRangeAsync([pizza]);

        await repo.UpdateAsync(pizza.BeginPreparing(DateTimeOffset.UtcNow));

        Assert.Empty(await repo.GetByStateAsync(PizzaState.OrderAccepted, 10));
        Assert.Single(await repo.GetByStateAsync(PizzaState.Preparing, 10));
    }

    [Fact]
    public async Task dough_repo_adds_filters_and_updates_state()
    {
        var repo = new InMemoryRestingDoughRepository();
        var dough = RestingDough.FromRecipe(RecipeCatalog.NapolitanDough);
        await repo.AddAsync(dough);

        Assert.Single(await repo.GetByStateAsync(DoughState.Waiting));

        await repo.UpdateAsync(dough.BeginResting(DateTimeOffset.UtcNow));

        Assert.Empty(await repo.GetByStateAsync(DoughState.Waiting));
        Assert.Single(await repo.GetByStateAsync(DoughState.Resting));
    }
}
