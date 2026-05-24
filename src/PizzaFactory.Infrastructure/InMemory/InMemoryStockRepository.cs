using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.InMemory;

/// <summary>
/// Thread-safe in-memory stock store, seeded with the factory's standard opening stock.
/// <see cref="Stock"/> is immutable, so reads are lock-free; writes swap the reference under a lock.
/// </summary>
public sealed class InMemoryStockRepository : IStockRepository
{
    private readonly Lock _gate = new();
    private Stock _stock = Stock.Initial();

    public Task<Stock> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_stock);
        }
    }

    public Task SaveAsync(Stock stock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stock);
        lock (_gate)
        {
            _stock = stock;
        }

        return Task.CompletedTask;
    }
}
