using Microsoft.Azure.Cosmos;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos-backed books. One document per closed service, partitioned as "service".</summary>
public sealed class CosmosServiceLedgerRepository : IServiceLedgerRepository
{
    private static readonly PartitionKey Partition = new("service");
    private readonly Container _container;

    public CosmosServiceLedgerRepository(CosmosClient client, CosmosOptions options) =>
        _container = client.GetContainer(options.Database, options.ServicesContainer);

    public async Task<IReadOnlyList<ClosedService>> RecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.closedAt DESC OFFSET 0 LIMIT @take")
            .WithParameter("@take", take);

        var results = new List<ClosedService>();
        using var iterator = _container.GetItemQueryIterator<ServiceDocument>(
            query, requestOptions: new QueryRequestOptions { PartitionKey = Partition });

        while (iterator.HasMoreResults)
        {
            foreach (var document in await iterator.ReadNextAsync(cancellationToken))
            {
                results.Add(document.ToClosedService());
            }
        }

        return results;
    }

    public async Task AddAsync(ClosedService service, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        await _container.UpsertItemAsync(ServiceDocument.From(service), Partition, cancellationToken: cancellationToken);
    }
}
