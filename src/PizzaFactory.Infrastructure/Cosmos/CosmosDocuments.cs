using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos persistence shape for an <see cref="Order"/>. Serialized camelCase, so Id -> "id".</summary>
internal sealed class OrderDocument
{
    public string Id { get; set; } = "";
    public string PartitionKey { get; set; } = "order";
    public string ItemName { get; set; } = "";
    public int Amount { get; set; }
    public OrderChannel Channel { get; set; }
    public OrderState State { get; set; }
    public string? CustomerName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static OrderDocument From(Order o) => new()
    {
        Id = o.Id,
        PartitionKey = "order",
        ItemName = o.ItemName,
        Amount = o.Amount,
        Channel = o.Channel,
        State = o.State,
        CustomerName = o.CustomerName,
        CreatedAt = o.CreatedAt,
    };

    public Order ToOrder() => new()
    {
        Id = Id,
        ItemName = ItemName,
        Amount = Amount,
        Channel = Channel,
        State = State,
        CustomerName = CustomerName,
        CreatedAt = CreatedAt,
    };
}

/// <summary>Cosmos persistence shape for the singleton <see cref="Stock"/> document (id = "stock").</summary>
internal sealed class StockDocument
{
    public string Id { get; set; } = "stock";
    public string PartitionKey { get; set; } = "stock";
    public Dictionary<string, int> Grams { get; set; } = [];

    public static StockDocument From(Stock stock) => new()
    {
        Id = "stock",
        PartitionKey = "stock",
        Grams = Enum.GetValues<Ingredient>().ToDictionary(i => i.ToString(), stock.GramsOf),
    };

    public Stock ToStock() => Stock.Empty.Refill(
        Grams.Where(kv => Enum.TryParse<Ingredient>(kv.Key, ignoreCase: true, out _))
             .Select(kv => IngredientQuantity.Of(Enum.Parse<Ingredient>(kv.Key, ignoreCase: true), kv.Value)));
}
