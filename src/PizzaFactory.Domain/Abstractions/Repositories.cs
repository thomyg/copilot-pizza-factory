using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Domain.Abstractions;

/// <summary>
/// Storage boundaries for the factory's aggregates. Implementations live outside the domain
/// (e.g. a Cosmos DB adapter or an in-memory test double); the domain depends only on these.
/// </summary>
public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByStateAsync(OrderState state, CancellationToken cancellationToken = default);
    Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

public interface IPizzaRepository
{
    Task<IReadOnlyList<Pizza>> GetByStateAsync(PizzaState state, int take, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Pizza> pizzas, CancellationToken cancellationToken = default);
    Task<Pizza> UpdateAsync(Pizza pizza, CancellationToken cancellationToken = default);
}

public interface IRestingDoughRepository
{
    Task<IReadOnlyList<RestingDough>> GetByStateAsync(DoughState state, CancellationToken cancellationToken = default);
    Task<RestingDough> AddAsync(RestingDough dough, CancellationToken cancellationToken = default);
    Task<RestingDough> UpdateAsync(RestingDough dough, CancellationToken cancellationToken = default);
}

public interface IStockRepository
{
    Task<Stock> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(Stock stock, CancellationToken cancellationToken = default);
}
