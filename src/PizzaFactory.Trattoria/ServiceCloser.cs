using Microsoft.Extensions.Logging;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Trattoria;

/// <summary>
/// Closes the books when a service ends.
///
/// Everything the house did during a window is still sitting in the order stream, but the
/// order stream gets cleared down and re-derived; a service that ran is a fact about a day
/// that should outlive it. So the moment the doors shut, the takings are totalled once and
/// written to the ledger — and from then on "last Tuesday" is something the house remembers
/// rather than something a generator invents.
///
/// Failures here are logged and swallowed on purpose: a bookkeeping hiccup must not take the
/// house down, and the generated backstory still covers any day the ledger is missing.
/// </summary>
public sealed class ServiceCloser(
    Bookkeeper bookkeeper,
    IServiceLedgerRepository ledger,
    ILogger<ServiceCloser> logger)
{
    public async Task CloseAsync(ServiceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            var report = await bookkeeper.ReportAsync(cancellationToken);
            var closed = new ClosedService(
                session.Id,
                session.Date,
                session.OpenedAt,
                session.ClosedAt ?? session.OpenedAt,
                report.OrdersToday,
                report.PizzasOrderedToday,
                report.GuestsServed,
                report.RevenueDeliveredEur + report.RevenueInFlightEur,
                report.AverageStars);

            await ledger.AddAsync(closed, cancellationToken);
            logger.LogInformation(
                "Books closed for {Service}: {Orders} orders, {Pizzas} pizzas, €{Revenue} — on the record",
                closed.Id, closed.Orders, closed.Pizzas, closed.RevenueEur);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not close the books for {Service} — the service ran, the ledger missed it", session.Id);
        }
    }
}
