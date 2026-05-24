using Microsoft.Azure.Cosmos;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos-backed pizza store (single logical partition "pizza").</summary>
public sealed class CosmosPizzaRepository : IPizzaRepository
{
    private static readonly PartitionKey Partition = new("pizza");
    private readonly Container _container;

    public CosmosPizzaRepository(CosmosClient client, CosmosOptions options) =>
        _container = client.GetContainer(options.Database, options.PizzasContainer);

    public async Task<IReadOnlyList<Pizza>> GetByStateAsync(PizzaState state, int take, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.state = @state").WithParameter("@state", state.ToString());
        var results = new List<Pizza>();
        using var iterator = _container.GetItemQueryIterator<PizzaDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var doc in await iterator.ReadNextAsync(cancellationToken))
            {
                results.Add(doc.ToPizza());
            }
        }

        return [.. results.OrderBy(p => p.StartedAt ?? DateTimeOffset.MaxValue).Take(take)];
    }

    public async Task AddRangeAsync(IEnumerable<Pizza> pizzas, CancellationToken cancellationToken = default)
    {
        foreach (var pizza in pizzas)
        {
            await _container.UpsertItemAsync(PizzaDocument.From(pizza), Partition, cancellationToken: cancellationToken);
        }
    }

    public async Task<Pizza> UpdateAsync(Pizza pizza, CancellationToken cancellationToken = default)
    {
        await _container.UpsertItemAsync(PizzaDocument.From(pizza), Partition, cancellationToken: cancellationToken);
        return pizza;
    }
}
