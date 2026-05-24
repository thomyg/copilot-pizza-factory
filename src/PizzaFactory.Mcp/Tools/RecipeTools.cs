using System.ComponentModel;
using ModelContextProtocol.Server;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Mcp.Tools;

/// <summary>MCP tools for browsing the menu — recipe lookups for agents and Copilot.</summary>
[McpServerToolType]
public sealed class RecipeTools
{
    [McpServerTool(Name = "list_pizzas")]
    [Description("List the pizzas on the menu.")]
    public IReadOnlyList<string> ListPizzas() => RecipeCatalog.Menu;

    [McpServerTool(Name = "get_recipe")]
    [Description("Get a pizza's toppings and prep/bake times.")]
    public RecipeInfo GetRecipe([Description("Pizza name from the menu.")] string pizza)
    {
        var recipe = RecipeCatalog.GetPizza(pizza);
        return new RecipeInfo(
            recipe.Name,
            [.. recipe.Toppings.Select(t => $"{t.Ingredient} {t.Grams}g")],
            (int)recipe.PreparingTime.TotalSeconds,
            (int)recipe.BakingTime.TotalSeconds);
    }
}
