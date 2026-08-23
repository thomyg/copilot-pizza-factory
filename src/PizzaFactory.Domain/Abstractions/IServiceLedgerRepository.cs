namespace PizzaFactory.Domain.Abstractions;

/// <summary>
/// What a service was worth, written once when the books close.
///
/// Deliberately a summary and not a pointer into the order stream: a closed service is a
/// fact about a day that stays true after the orders are cleared down, and reporting should
/// not have to re-derive last Tuesday from raw tickets every time somebody asks.
/// </summary>
public sealed record ClosedService(
    string Id,
    DateOnly Date,
    DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt,
    int Orders,
    int Pizzas,
    int Guests,
    decimal RevenueEur,
    double? AverageStars);

/// <summary>The house's own books: the services that actually ran.</summary>
public interface IServiceLedgerRepository
{
    /// <summary>Most recent first, capped — reporting never wants the whole history at once.</summary>
    Task<IReadOnlyList<ClosedService>> RecentAsync(int take, CancellationToken cancellationToken = default);

    Task AddAsync(ClosedService service, CancellationToken cancellationToken = default);
}
