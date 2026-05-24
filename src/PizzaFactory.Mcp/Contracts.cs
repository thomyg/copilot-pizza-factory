using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Mcp;

/// <summary>Compact, serialization-friendly shapes returned by the MCP tools.</summary>
public sealed record OrderSummary(string Id, string Pizza, int Amount, string State, string? Customer)
{
    public static OrderSummary From(Order order) =>
        new(order.Id, order.ItemName, order.Amount, order.State.ToString(), order.CustomerName);
}

public sealed record StockLevel(string Ingredient, int Grams);

public sealed record CanMakeResult(string Pizza, bool CanMake, string? MissingIngredient);

public sealed record RecipeInfo(string Name, IReadOnlyList<string> Toppings, int PreparingSeconds, int BakingSeconds);

public sealed record StationStatus(int Ordered, int Preparing, int Baking, int Ready, int OpenOrders, int DoughReady);
