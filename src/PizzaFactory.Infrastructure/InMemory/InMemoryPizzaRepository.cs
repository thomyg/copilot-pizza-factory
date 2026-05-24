using System.Collections.Concurrent;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.InMemory;

/// <summary>Thread-safe in-memory pizza store.</summary>
public sealed class InMemoryPizzaRepository : IPizzaRepository
{
    private readonly ConcurrentDictionary<string, Pizza> _pizzas = new();

    public Task<IReadOnlyList<Pizza>> GetByStateAsync(PizzaState state, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Pizza>>(
            [.. _pizzas.Values.Where(p => p.State == state).OrderBy(p => p.StartedAt ?? DateTimeOffset.MaxValue).Take(take)]);

    public Task AddRangeAsync(IEnumerable<Pizza> pizzas, CancellationToken cancellationToken = default)
    {
        foreach (var pizza in pizzas)
        {
            _pizzas[pizza.Id] = pizza;
        }

        return Task.CompletedTask;
    }

    public Task<Pizza> UpdateAsync(Pizza pizza, CancellationToken cancellationToken = default)
    {
        _pizzas[pizza.Id] = pizza;
        return Task.FromResult(pizza);
    }
}
