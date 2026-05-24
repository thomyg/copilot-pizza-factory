using System.Net;
using Microsoft.Azure.Cosmos;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos-backed stock store. A single document (id = "stock"); seeds opening stock on first read.</summary>
public sealed class CosmosStockRepository : IStockRepository
{
    private const string StockId = "stock";
    private static readonly PartitionKey Partition = new("stock");
    private readonly Container _container;

    public CosmosStockRepository(CosmosClient client, CosmosOptions options) =>
        _container = client.GetContainer(options.Database, options.StockContainer);

    public async Task<Stock> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<StockDocument>(
                StockId, Partition, cancellationToken: cancellationToken);
            return response.Resource.ToStock();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var initial = Stock.Initial();
            await SaveAsync(initial, cancellationToken);
            return initial;
        }
    }

    public async Task SaveAsync(Stock stock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stock);
        await _container.UpsertItemAsync(StockDocument.From(stock), Partition, cancellationToken: cancellationToken);
    }
}
