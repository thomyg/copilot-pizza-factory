using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

public class DemoDirectorTests
{
    private static (DemoDirector Director, InMemoryOrderRepository Orders, InMemoryStockRepository Stock) Build()
    {
        var orders = new InMemoryOrderRepository();
        var stock = new InMemoryStockRepository();
        return (new DemoDirector(orders, stock), orders, stock);
    }

    [Fact]
    public async Task sabotage_drains_the_ingredient_to_zero_and_reports_grams_removed()
    {
        var (director, _, stock) = Build();

        var removed = await director.SabotageAsync(Ingredient.Pineapple);

        Assert.Equal(250, removed); // opening stock
        Assert.Equal(0, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    [Fact]
    public async Task sabotage_on_an_empty_ingredient_is_a_harmless_no_op()
    {
        var (director, _, stock) = Build();
        await director.SabotageAsync(Ingredient.Pineapple);

        var removed = await director.SabotageAsync(Ingredient.Pineapple);

        Assert.Equal(0, removed);
        Assert.Equal(0, (await stock.GetAsync()).GramsOf(Ingredient.Pineapple));
    }

    [Fact]
    public async Task adjust_up_and_down_clamps_at_zero()
    {
        var (director, _, stock) = Build();

        await director.AdjustAsync(Ingredient.Mushroom, +200);
        Assert.Equal(700, (await stock.GetAsync()).GramsOf(Ingredient.Mushroom));

        await director.AdjustAsync(Ingredient.Mushroom, -10_000);
        Assert.Equal(0, (await stock.GetAsync()).GramsOf(Ingredient.Mushroom));
    }

    [Fact]
    public async Task restock_resets_the_pantry_to_opening_levels()
    {
        var (director, _, stock) = Build();
        await director.SabotageAsync(Ingredient.Pineapple);
        await director.SabotageAsync(Ingredient.Mozzarella);

        await director.RestockAsync();

        var current = await stock.GetAsync();
        Assert.Equal(250, current.GramsOf(Ingredient.Pineapple));
        Assert.Equal(1300, current.GramsOf(Ingredient.Mozzarella));
    }

    [Fact]
    public async Task rush_hour_places_the_requested_number_of_guest_orders()
    {
        var (director, orders, _) = Build();

        var placed = await director.RushHourAsync(15);

        Assert.Equal(15, placed);
        var all = await orders.ListAsync();
        Assert.Equal(15, all.Count);
        Assert.All(all, o =>
        {
            Assert.Equal(OrderChannel.Guest, o.Channel);
            Assert.Contains(o.ItemName, RecipeCatalog.Menu);
            Assert.InRange(o.Amount, 1, 2);
        });
    }

    [Fact]
    public async Task rush_hour_is_clamped_to_the_maximum()
    {
        var (director, orders, _) = Build();

        var placed = await director.RushHourAsync(9999);

        Assert.Equal(DemoDirector.MaxRushOrders, placed);
        Assert.Equal(DemoDirector.MaxRushOrders, (await orders.ListAsync()).Count);
    }
}
