using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Infrastructure;
using PizzaFactory.Infrastructure.Cosmos;

namespace PizzaFactory.Infrastructure.IntegrationTests;

/// <summary>
/// Live Cosmos round-trips via key-less auth (DefaultAzureCredential / az login).
/// Gated on the COSMOS_ENDPOINT env var so the default test run stays offline-safe:
///   COSMOS_ENDPOINT=https://<your-cosmos-account>.documents.azure.com dotnet test
/// </summary>
public class CosmosStoreTests
{
    private static CosmosOptions? OptionsFromEnv()
    {
        var endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT");
        return string.IsNullOrWhiteSpace(endpoint) ? null : new CosmosOptions { Endpoint = endpoint };
    }

    [Fact]
    public async Task order_round_trips_through_cosmos()
    {
        var options = OptionsFromEnv();
        if (options is null)
        {
            return; // skipped — set COSMOS_ENDPOINT to run
        }

        using var client = DependencyInjection.CreateCosmosClient(options);
        var repo = new CosmosOrderRepository(client, options);

        var order = Order.Create("Hawaii", 2, OrderChannel.Guest, "Anchovy Anonymous");
        await repo.AddAsync(order);
        await repo.UpdateAsync(order.Start());

        var all = await repo.ListAsync();
        Assert.Contains(all, o => o.Id == order.Id);

        var started = await repo.GetByStateAsync(OrderState.Started);
        Assert.Contains(started, o =>
            o.Id == order.Id && o.ItemName == "Hawaii" && o.CustomerName == "Anchovy Anonymous");
    }

    [Fact]
    public async Task stock_round_trips_through_cosmos()
    {
        var options = OptionsFromEnv();
        if (options is null)
        {
            return; // skipped — set COSMOS_ENDPOINT to run
        }

        using var client = DependencyInjection.CreateCosmosClient(options);
        var repo = new CosmosStockRepository(client, options);

        var before = (await repo.GetAsync()).GramsOf(Ingredient.Pineapple);   // seeds Initial() on first run
        await repo.SaveAsync((await repo.GetAsync()).Refill([IngredientQuantity.Of(Ingredient.Pineapple, 100)]));
        var after = (await repo.GetAsync()).GramsOf(Ingredient.Pineapple);

        Assert.Equal(before + 100, after);
    }
}
