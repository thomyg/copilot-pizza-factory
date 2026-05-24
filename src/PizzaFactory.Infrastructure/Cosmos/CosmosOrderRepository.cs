using Microsoft.Azure.Cosmos;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos-backed order store. All orders live in a single logical partition ("order").</summary>
public sealed class CosmosOrderRepository : IOrderRepository
{
    private static readonly PartitionKey Partition = new("order");
    private readonly Container _container;

    public CosmosOrderRepository(CosmosClient client, CosmosOptions options) =>
        _container = client.GetContainer(options.Database, options.OrdersContainer);

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default) =>
        QueryAsync(new QueryDefinition("SELECT * FROM c"), cancellationToken);

    public Task<IReadOnlyList<Order>> GetByStateAsync(OrderState state, CancellationToken cancellationToken = default) =>
        QueryAsync(
            new QueryDefinition("SELECT * FROM c WHERE c.state = @state").WithParameter("@state", state.ToString()),
            cancellationToken);

    public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _container.UpsertItemAsync(OrderDocument.From(order), Partition, cancellationToken: cancellationToken);
        return order;
    }

    public Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default) =>
        AddAsync(order, cancellationToken);

    private async Task<IReadOnlyList<Order>> QueryAsync(QueryDefinition query, CancellationToken cancellationToken)
    {
        var results = new List<Order>();
        using var iterator = _container.GetItemQueryIterator<OrderDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var doc in await iterator.ReadNextAsync(cancellationToken))
            {
                results.Add(doc.ToOrder());
            }
        }

        results.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return results;
    }
}
