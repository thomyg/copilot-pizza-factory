using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;
using PizzaFactory.Mcp.Tools;

namespace PizzaFactory.Mcp.Tests;

public class RecipeTelemetryToolTests
{
    [Fact]
    public void list_pizzas_returns_the_menu()
    {
        var tools = new RecipeTools();
        Assert.Equal(6, tools.ListPizzas().Count);
        Assert.Contains("Hawaii", tools.ListPizzas());
    }

    [Fact]
    public void get_recipe_returns_toppings_and_times()
    {
        var info = new RecipeTools().GetRecipe("hawaii");
        Assert.Equal("Hawaii", info.Name);
        Assert.Contains(info.Toppings, t => t.StartsWith("Pineapple", StringComparison.Ordinal));
        Assert.True(info.PreparingSeconds > 0);
        Assert.True(info.BakingSeconds > 0);
    }

    [Fact]
    public void get_recipe_throws_for_unknown_pizza() =>
        Assert.Throws<ArgumentException>(() => new RecipeTools().GetRecipe("Calzone"));

    [Fact]
    public async Task station_status_counts_each_stage()
    {
        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var doughs = new InMemoryRestingDoughRepository();
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create("Margherita", 4, OrderChannel.Online);
        await orders.AddAsync(order); // 1 open (Created)

        var accepted = Pizza.FromOrder(order);
        var preparing = Pizza.FromOrder(order).BeginPreparing(now);
        var baking = Pizza.FromOrder(order).BeginPreparing(now).BeginBaking(now);
        var ready = Pizza.FromOrder(order).BeginPreparing(now).BeginBaking(now).MarkReady(now);
        await pizzas.AddRangeAsync([accepted, preparing, baking, ready]);
        await doughs.AddAsync(RestingDough.FromRecipe(PizzaFactory.Domain.Recipes.RecipeCatalog.NapolitanDough).BeginResting(now).MarkReady());

        var status = await new TelemetryTools(orders, pizzas, doughs).StationStatusAsync();

        Assert.Equal(1, status.Ordered);
        Assert.Equal(1, status.Preparing);
        Assert.Equal(1, status.Baking);
        Assert.Equal(1, status.Ready);
        Assert.Equal(1, status.OpenOrders);
        Assert.Equal(1, status.DoughReady);
    }
}
