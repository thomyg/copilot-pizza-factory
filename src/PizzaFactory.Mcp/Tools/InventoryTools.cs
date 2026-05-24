using System.ComponentModel;
using ModelContextProtocol.Server;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Mcp.Tools;

/// <summary>
/// MCP tools for inspecting ingredient stock — the data behind the "Ask the Factory" Copilot
/// answers and the Pineapple-Crisis story.
/// </summary>
[McpServerToolType]
public sealed class InventoryTools(IStockRepository stock)
{
    [McpServerTool(Name = "get_stock")]
    [Description("List the current grams in stock for every ingredient.")]
    public async Task<IReadOnlyList<StockLevel>> GetStockAsync(CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);
        return [.. Enum.GetValues<Ingredient>().Select(i => new StockLevel(i.ToString(), current.GramsOf(i)))];
    }

    [McpServerTool(Name = "low_stock_report")]
    [Description("List ingredients at or below a gram threshold — the ones at risk of running out.")]
    public async Task<IReadOnlyList<StockLevel>> LowStockReportAsync(
        [Description("Threshold in grams; ingredients at or below this are reported. Default 300.")] int thresholdGrams = 300,
        CancellationToken cancellationToken = default)
    {
        var current = await stock.GetAsync(cancellationToken);
        return
        [
            .. Enum.GetValues<Ingredient>()
                .Select(i => new StockLevel(i.ToString(), current.GramsOf(i)))
                .Where(level => level.Grams <= thresholdGrams)
                .OrderBy(level => level.Grams)
        ];
    }

    [McpServerTool(Name = "check_can_make")]
    [Description("Check whether there is enough stock to make a given pizza right now.")]
    public async Task<CanMakeResult> CheckCanMakeAsync(
        [Description("Pizza name from the menu.")] string pizza,
        CancellationToken cancellationToken = default)
    {
        var recipe = RecipeCatalog.FindPizza(pizza)
            ?? throw new ArgumentException(
                $"'{pizza}' is not on the menu. Available: {string.Join(", ", RecipeCatalog.Menu)}.",
                nameof(pizza));

        var current = await stock.GetAsync(cancellationToken);
        var canMake = current.CanFulfill(recipe.Toppings, out var missing);
        return new CanMakeResult(recipe.Name, canMake, missing?.ToString());
    }
}
