using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure.InMemory;
using PizzaFactory.Mcp.Tools;

namespace PizzaFactory.Mcp.Tests;

public class McpToolTests
{
    private static OrderTools NewOrderTools(out IOrderRepository repo)
    {
        repo = new InMemoryOrderRepository();
        return new OrderTools(repo);
    }

    [Fact]
    public async Task create_order_then_get_status_round_trips()
    {
        var tools = NewOrderTools(out _);

        var created = await tools.CreateOrderAsync("hawaii", 2, "Anchovy Anonymous");

        Assert.Equal("Hawaii", created.Pizza);        // normalized to menu casing
        Assert.Equal(2, created.Amount);
        Assert.Equal(nameof(OrderState.Created), created.State);
        Assert.Equal("Anchovy Anonymous", created.Customer);

        var fetched = await tools.GetOrderStatusAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task create_order_rejects_items_not_on_the_menu()
    {
        var tools = NewOrderTools(out _);
        await Assert.ThrowsAsync<ArgumentException>(() => tools.CreateOrderAsync("Calzone", 1));
    }

    [Fact]
    public async Task get_order_status_returns_null_for_unknown_id()
    {
        var tools = NewOrderTools(out _);
        Assert.Null(await tools.GetOrderStatusAsync("does-not-exist"));
    }

    [Fact]
    public async Task list_orders_returns_created_orders()
    {
        var tools = NewOrderTools(out _);
        await tools.CreateOrderAsync("Margherita", 1);
        await tools.CreateOrderAsync("Diavolo", 3);

        var all = await tools.ListOrdersAsync();
        Assert.Equal(2, all.Count);

        var created = await tools.ListOrdersAsync("created");
        Assert.Equal(2, created.Count);
    }

    [Fact]
    public async Task get_stock_lists_every_ingredient()
    {
        var tools = new InventoryTools(new InMemoryStockRepository());

        var levels = await tools.GetStockAsync();

        Assert.Equal(Enum.GetValues<Ingredient>().Length, levels.Count);
        Assert.Equal(250, levels.Single(l => l.Ingredient == nameof(Ingredient.Pineapple)).Grams);
    }

    [Fact]
    public async Task low_stock_report_flags_pineapple_at_the_default_threshold()
    {
        var tools = new InventoryTools(new InMemoryStockRepository());

        var low = await tools.LowStockReportAsync();

        Assert.Contains(low, l => l.Ingredient == nameof(Ingredient.Pineapple));
    }

    [Fact]
    public async Task check_can_make_is_true_on_opening_stock()
    {
        var tools = new InventoryTools(new InMemoryStockRepository());

        var result = await tools.CheckCanMakeAsync("Hawaii");

        Assert.True(result.CanMake);
        Assert.Null(result.MissingIngredient);
    }

    [Fact]
    public async Task check_can_make_reports_missing_when_out_of_stock()
    {
        var repo = new InMemoryStockRepository();
        await repo.SaveAsync(Stock.Empty);
        var tools = new InventoryTools(repo);

        var result = await tools.CheckCanMakeAsync("Margherita");

        Assert.False(result.CanMake);
        Assert.NotNull(result.MissingIngredient);
    }
}
