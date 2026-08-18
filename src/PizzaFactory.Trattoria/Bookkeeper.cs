using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Trattoria;

/// <summary>Tonight's numbers, aggregated from the REAL order stream and dining room.</summary>
public sealed record BusinessReport(
    bool ServiceOpen,
    int OrdersToday,
    int PizzasOrderedToday,
    int PizzasDeliveredToday,
    decimal RevenueDeliveredEur,
    decimal RevenueInFlightEur,
    IReadOnlyDictionary<string, int> OrdersByChannel,
    IReadOnlyDictionary<string, int> PizzasByType,
    string? TopSeller,
    int PartiesServed,
    int GuestsServed,
    int Walkouts,
    double? AverageStars,
    int OrdersLastTenMinutes,
    int ProjectedOrdersNextHour);

/// <summary>One day in the ledger's backstory.</summary>
public sealed record DailyLedger(DateOnly Date, string Day, int Orders, int Guests, decimal RevenueEur, double Stars);

/// <summary>
/// Giuseppe's bookkeeper. Tonight's report is aggregated from REAL data — every order the run
/// produced, priced via the <see cref="PriceList"/> — with an honest pace projection (last ten
/// minutes, extrapolated). The seven-day history is a SEEDED BACKSTORY: deterministic, plausible
/// numbers (weekends run hotter) so "versus a typical Tuesday" comparisons work in a demo that
/// booted five minutes ago. It never pretends to be anything else.
/// </summary>
public sealed class Bookkeeper(IOrderRepository orders, MaitreD maitreD, TimeProvider clock)
{
    public async Task<BusinessReport> ReportAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var today = (await orders.ListAsync(cancellationToken))
            .Where(o => o.CreatedAt.ToLocalTime().Date == now.ToLocalTime().Date)
            .ToList();

        var delivered = today.Where(o => o.State == OrderState.Delivered).ToList();
        var inFlight = today.Where(o => o.State is OrderState.Created or OrderState.Started or OrderState.Ready).ToList();

        var byChannel = today
            .GroupBy(o => o.Channel.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byPizza = today
            .GroupBy(o => o.ItemName)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Amount));

        var lastTen = today.Count(o => now - o.CreatedAt <= TimeSpan.FromMinutes(10));

        var dining = maitreD.Snapshot();
        return new BusinessReport(
            ServiceOpen: dining.IsOpen,
            OrdersToday: today.Count,
            PizzasOrderedToday: today.Sum(o => o.Amount),
            PizzasDeliveredToday: delivered.Sum(o => o.Amount),
            RevenueDeliveredEur: delivered.Sum(o => o.Amount * PriceList.Of(o.ItemName)),
            RevenueInFlightEur: inFlight.Sum(o => o.Amount * PriceList.Of(o.ItemName)),
            OrdersByChannel: byChannel,
            PizzasByType: byPizza,
            TopSeller: byPizza.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault(),
            PartiesServed: dining.PartiesServed,
            GuestsServed: dining.GuestsServed,
            Walkouts: dining.Walkouts,
            AverageStars: dining.AverageStars,
            OrdersLastTenMinutes: lastTen,
            ProjectedOrdersNextHour: lastTen * 6);
    }

    /// <summary>The last seven days' ledger — seeded backstory, deterministic per calendar date.</summary>
    public IReadOnlyList<DailyLedger> History()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToLocalTime().Date);
        return Enumerable.Range(1, 7)
            .Select(back => today.AddDays(-back))
            .Select(BackstoryFor)
            .OrderBy(d => d.Date)
            .ToList();
    }

    private static DailyLedger BackstoryFor(DateOnly date)
    {
        var random = new Random(date.DayNumber);   // stable per calendar date, across restarts
        var weekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        var orders = weekend ? random.Next(95, 131) : date.DayOfWeek == DayOfWeek.Sunday ? random.Next(70, 91) : random.Next(55, 86);
        var guests = (int)(orders * (1.9 + random.NextDouble() * 0.5));
        var revenue = Math.Round(orders * (19.5m + (decimal)random.NextDouble() * 4m), 2);
        var stars = Math.Round(4.1 + random.NextDouble() * 0.6, 1);
        return new DailyLedger(date, date.DayOfWeek.ToString(), orders, guests, revenue, stars);
    }
}
