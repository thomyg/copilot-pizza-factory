using System.ComponentModel;
using ModelContextProtocol.Server;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Mcp.Tools;

/// <summary>
/// MCP tools for placing and tracking orders. Any MCP client — M365 Copilot, Copilot Studio,
/// an Agent Framework agent, or a dev tool — can drive the factory through these.
/// </summary>
[McpServerToolType]
public sealed class OrderTools(IOrderRepository orders)
{
    [McpServerTool(Name = "create_order")]
    [Description("Place a new order for a pizza from the menu. Returns the created order with its id and state.")]
    public async Task<OrderSummary> CreateOrderAsync(
        [Description("Pizza name from the menu, e.g. 'Hawaii' or 'Diavolo'.")] string pizza,
        [Description("How many of this pizza to make (1 or more).")] int amount,
        [Description("Optional display name to show on the factory floor.")] string? customerName = null,
        CancellationToken cancellationToken = default)
    {
        var recipe = RecipeCatalog.FindPizza(pizza)
            ?? throw new ArgumentException(
                $"'{pizza}' is not on the menu. Available: {string.Join(", ", RecipeCatalog.Menu)}.",
                nameof(pizza));

        var order = await orders.AddAsync(
            Order.Create(recipe.Name, amount, OrderChannel.Bot, customerName),
            cancellationToken);

        return OrderSummary.From(order);
    }

    [McpServerTool(Name = "get_order_status")]
    [Description("Get the current status of a single order by its id.")]
    public async Task<OrderSummary?> GetOrderStatusAsync(
        [Description("The order id returned by create_order.")] string orderId,
        CancellationToken cancellationToken = default)
    {
        var all = await orders.ListAsync(cancellationToken);
        var match = all.FirstOrDefault(o => o.Id == orderId);
        return match is null ? null : OrderSummary.From(match);
    }

    [McpServerTool(Name = "list_orders")]
    [Description("List orders, optionally filtered by state (created, started, ready, delivered).")]
    public async Task<IReadOnlyList<OrderSummary>> ListOrdersAsync(
        [Description("Optional state filter. Omit to list all orders.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        var list = state is { Length: > 0 } && Enum.TryParse<OrderState>(state, ignoreCase: true, out var parsed)
            ? await orders.GetByStateAsync(parsed, cancellationToken)
            : await orders.ListAsync(cancellationToken);

        return [.. list.Select(OrderSummary.From)];
    }
}
