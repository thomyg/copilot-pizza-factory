namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Configuration for the Cosmos DB store. Bound from the "Cosmos" configuration section.</summary>
public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    /// <summary>Account endpoint, e.g. https://&lt;your-cosmos-account&gt;.documents.azure.com. Auth is key-less (AAD).</summary>
    public string Endpoint { get; set; } = "";

    public string Database { get; set; } = "pizzafactory";
    public string OrdersContainer { get; set; } = "orders";
    public string StockContainer { get; set; } = "stock";
    public string PizzasContainer { get; set; } = "pizzas";
    public string DoughsContainer { get; set; } = "doughs";
}
