using System.Collections.Concurrent;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.InMemory;

/// <summary>
/// Thread-safe in-memory order store. The Phase 1 stand-in for the Cosmos DB adapter;
/// good enough to run the whole factory locally and in tests.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>([.. _orders.Values.OrderBy(o => o.CreatedAt)]);

    public Task<IReadOnlyList<Order>> GetByStateAsync(OrderState state, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>(
            [.. _orders.Values.Where(o => o.State == state).OrderBy(o => o.CreatedAt)]);

    public Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.FromResult(order);
    }

    public Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.FromResult(order);
    }
}
