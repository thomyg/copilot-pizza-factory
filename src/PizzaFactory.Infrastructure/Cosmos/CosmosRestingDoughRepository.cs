using Microsoft.Azure.Cosmos;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos-backed resting-dough store (single logical partition "dough").</summary>
public sealed class CosmosRestingDoughRepository : IRestingDoughRepository
{
    private static readonly PartitionKey Partition = new("dough");
    private readonly Container _container;

    public CosmosRestingDoughRepository(CosmosClient client, CosmosOptions options) =>
        _container = client.GetContainer(options.Database, options.DoughsContainer);

    public async Task<IReadOnlyList<RestingDough>> GetByStateAsync(DoughState state, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.state = @state").WithParameter("@state", state.ToString());
        var results = new List<RestingDough>();
        using var iterator = _container.GetItemQueryIterator<RestingDoughDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var doc in await iterator.ReadNextAsync(cancellationToken))
            {
                results.Add(doc.ToRestingDough());
            }
        }

        return [.. results.OrderBy(d => d.FinishesAt ?? DateTimeOffset.MaxValue)];
    }

    public async Task<RestingDough> AddAsync(RestingDough dough, CancellationToken cancellationToken = default)
    {
        await _container.UpsertItemAsync(RestingDoughDocument.From(dough), Partition, cancellationToken: cancellationToken);
        return dough;
    }

    public async Task<RestingDough> UpdateAsync(RestingDough dough, CancellationToken cancellationToken = default)
    {
        await _container.UpsertItemAsync(RestingDoughDocument.From(dough), Partition, cancellationToken: cancellationToken);
        return dough;
    }
}
