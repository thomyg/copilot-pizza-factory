using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Trattoria;

/// <summary>A predicted problem, ranked: High burns tonight, Medium burns soon, Low is a note.</summary>
public sealed record RiskForecast(string Severity, string Risk, string Detail);

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
public sealed class Bookkeeper(
    IOrderRepository orders,
    IStockRepository stock,
    IRestingDoughRepository doughs,
    MaitreD maitreD,
    PreOrderBook preOrders,
    TrattoriaOptions options,
    TimeProvider clock)
{
    /// <summary>
    /// The crystal ball: cross-references current stock against demand that is already committed
    /// (open orders + reservations firing within three hours), the dough buffer, and seating
    /// pressure — and names what will most likely become a problem soon, worst first. Honest
    /// arithmetic, not vibes: every risk cites the numbers behind it.
    /// </summary>
    public async Task<IReadOnlyList<RiskForecast>> ForecastAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var risks = new List<RiskForecast>();

        var open = (await orders.ListAsync(cancellationToken))
            .Where(o => o.State is OrderState.Created or OrderState.Started)
            .ToList();
        var dueSoon = preOrders.Upcoming.Where(p => p.When <= now.AddHours(3)).ToList();

        // Grams already spoken for: everything on open tickets plus reservations about to fire.
        var committed = new Dictionary<Ingredient, int>();
        void Commit(string pizza, int amount)
        {
            if (RecipeCatalog.FindPizza(pizza) is not { } recipe)
            {
                return;
            }

            foreach (var topping in recipe.Toppings)
            {
                committed[topping.Ingredient] = committed.GetValueOrDefault(topping.Ingredient) + topping.Grams * amount;
            }
        }

        foreach (var order in open)
        {
            Commit(order.ItemName, order.Amount);
        }

        foreach (var pre in dueSoon)
        {
            Commit(pre.Pizza, pre.Amount);
        }

        var pantry = await stock.GetAsync(cancellationToken);
        foreach (var ingredient in Enum.GetValues<Ingredient>())
        {
            var have = pantry.GramsOf(ingredient);
            var need = committed.GetValueOrDefault(ingredient);
            var left = have - need;
            if (left < 0)
            {
                risks.Add(new RiskForecast("High", $"{ingredient} will run out",
                    $"{have}g in stock but {need}g already committed to open orders and reservations — short by {-left}g before anything new is even ordered."));
            }
            else if (left <= options.CrisisThresholdGrams && need > 0)
            {
                risks.Add(new RiskForecast("High", $"{ingredient} heading for crisis",
                    $"{have}g in stock minus {need}g committed leaves {left}g — at or below the crisis threshold ({options.CrisisThresholdGrams}g). Expect an escalation."));
            }
            else if (have <= options.RestockThresholdGrams)
            {
                risks.Add(new RiskForecast("Medium", $"{ingredient} running low",
                    $"{have}g in stock is at or below the restock threshold ({options.RestockThresholdGrams}g) — Procurement will need to reorder soon."));
            }
        }

        var doughReady = (await doughs.GetByStateAsync(DoughState.Ready, cancellationToken)).Count;
        if (doughReady == 0 && open.Count >= 3)
        {
            risks.Add(new RiskForecast("High", "Dough buffer is empty",
                $"0 doughs ready with {open.Count} open orders — the kitchen queues on dough, waits grow, and the reviews will notice."));
        }

        var dining = maitreD.Snapshot();
        var occupied = dining.Tables.Count(t => t.Party is not null);
        if (dining.IsOpen && occupied >= 15)
        {
            risks.Add(new RiskForecast("Medium", "Dining room near capacity",
                $"{occupied}/17 tables taken — the next big party likely walks out. {dining.Walkouts} walkouts so far tonight."));
        }

        foreach (var pre in dueSoon.Where(p => p.Amount >= 8))
        {
            risks.Add(new RiskForecast("Medium", $"Big reservation firing at {pre.When.ToLocalTime():HH:mm}",
                $"{pre.Amount}× {pre.Pizza} for {pre.Name} hits the oven soon — make sure stock and dough are ahead of it."));
        }

        if (risks.Count == 0)
        {
            risks.Add(new RiskForecast("Low", "No storm clouds",
                "Stock, dough, and seating all look comfortable for the next hour. Enjoy it — it won't last."));
        }

        return [.. risks
            .OrderBy(r => r.Severity switch { "High" => 0, "Medium" => 1, _ => 2 })];
    }

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
