using System.Collections.Concurrent;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Infrastructure.InMemory;

/// <summary>
/// The books, in memory. Fine for local runs and tests; a restart forgets the services
/// that ran, which is exactly why the hosted demo uses the Cosmos one.
/// </summary>
public sealed class InMemoryServiceLedgerRepository : IServiceLedgerRepository
{
    private readonly ConcurrentDictionary<string, ClosedService> _services = new();

    public Task<IReadOnlyList<ClosedService>> RecentAsync(int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClosedService> recent = [.. _services.Values.OrderByDescending(s => s.ClosedAt).Take(take)];
        return Task.FromResult(recent);
    }

    public Task AddAsync(ClosedService service, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[service.Id] = service;
        return Task.CompletedTask;
    }
}
