using System.Collections.Concurrent;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.InMemory;

/// <summary>Thread-safe in-memory resting-dough store.</summary>
public sealed class InMemoryRestingDoughRepository : IRestingDoughRepository
{
    private readonly ConcurrentDictionary<string, RestingDough> _doughs = new();

    public Task<IReadOnlyList<RestingDough>> GetByStateAsync(DoughState state, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RestingDough>>(
            [.. _doughs.Values.Where(d => d.State == state).OrderBy(d => d.FinishesAt ?? DateTimeOffset.MaxValue)]);

    public Task<RestingDough> AddAsync(RestingDough dough, CancellationToken cancellationToken = default)
    {
        _doughs[dough.Id] = dough;
        return Task.FromResult(dough);
    }

    public Task<RestingDough> UpdateAsync(RestingDough dough, CancellationToken cancellationToken = default)
    {
        _doughs[dough.Id] = dough;
        return Task.FromResult(dough);
    }
}
