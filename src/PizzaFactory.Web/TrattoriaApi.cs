using System.Globalization;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Trattoria;

namespace PizzaFactory.Web;

/// <summary>
/// One read-only snapshot of the whole house, shaped exactly like the SPFx cockpit's
/// <c>ITrattoriaSnapshot</c>. It exists so the Trattoria Command Copilot component on
/// SharePoint can drop its rehearsal data and read the REAL running factory instead —
/// the UI contract never changes, only where the numbers come from.
/// Same CORS + rate-limit seams as the other SharePoint-facing endpoints.
/// </summary>
public static class TrattoriaApi
{
    /// <summary>Grams below which Procurement calls a restock / declares a crisis.</summary>
    private const int LowGrams = 300;
    private const int CrisisGrams = 150;

    /// <summary>Ingredients the cockpit gauges — the ones an audience can reason about.</summary>
    private static readonly Ingredient[] Watched =
    [
        Ingredient.Flour, Ingredient.TomatoSauce, Ingredient.Mozzarella,
        Ingredient.Salami, Ingredient.Ham, Ingredient.Pineapple,
        Ingredient.Mushroom, Ingredient.Tuna,
    ];

    public static void MapTrattoriaApi(this WebApplication app)
    {
        var hasCors = !string.IsNullOrWhiteSpace(app.Configuration["SharePointChat:AllowedOrigins"]);

        var group = app.MapGroup("/api/trattoria").RequireRateLimiting(GiuseppeChatApi.ReadRateLimitPolicy);
        if (hasCors)
        {
            group = group.RequireCors(GiuseppeChatApi.CorsPolicy);
        }

        // Opening and closing the house. Deliberately POST with no body: the button on the
        // SharePoint hero and the one in the Engine Room press the same thing.
        group.MapPost("/service/open", (ServiceWindow service) =>
        {
            var session = service.Open();
            return Results.Ok(new
            {
                open = true,
                id = session.Id,
                openedAt = session.OpenedAt,
                minutesLeft = Math.Round((service.Remaining ?? TimeSpan.Zero).TotalMinutes, 1),
            });
        });

        group.MapPost("/service/close", (ServiceWindow service) =>
        {
            var closed = service.Close();
            return closed is null
                ? Results.Ok(new { open = false, closed = false })
                : Results.Ok(new { open = false, closed = true, id = closed.Id, closedAt = closed.ClosedAt });
        });

        group.MapGet("/snapshot", async (
            MaitreD maitreD,
            Bookkeeper bookkeeper,
            PreOrderBook preOrders,
            TrattoriaFeed feed,
            ServiceWindow service,
            IPizzaRepository pizzas,
            IStockRepository stockRepository,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var now = clock.GetLocalNow();
            var house = maitreD.Snapshot();
            var report = await bookkeeper.ReportAsync(cancellationToken);
            var forecast = await bookkeeper.ForecastAsync(cancellationToken);
            var stock = await stockRepository.GetAsync(cancellationToken);

            var session = service.Current;
            return Results.Ok(new
            {
                // The window itself, so a surface can say "between services" honestly and
                // offer to open one instead of pretending the house is merely quiet.
                service = new
                {
                    open = service.IsOpen,
                    minutesLeft = Math.Round((service.Remaining ?? TimeSpan.Zero).TotalMinutes, 1),
                    openedAt = session?.OpenedAt,
                    closedAt = session?.ClosedAt,
                    everRan = session is not null,
                },
                tonight = new
                {
                    serviceOpen = house.IsOpen,
                    tablesSeated = house.Tables.Count(t => t.Party is not null),
                    tablesTotal = house.Tables.Count,
                    line = await LineAsync(pizzas, cancellationToken),
                    guestsServed = house.GuestsServed,
                    averageStars = house.AverageStars,
                    stock = StockGauges(stock),
                    channels = Channels(report.OrdersByChannel),
                    feed = feed.Recent
                        .OrderByDescending(e => e.At)
                        .Take(12)
                        .Select(e => new { at = e.At.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), text = e.Text })
                        .ToArray(),
                },
                report = new
                {
                    dateLabel = now.ToString("dddd d MMMM", CultureInfo.InvariantCulture),
                    ordersToday = report.OrdersToday,
                    pizzasToday = report.PizzasOrderedToday,
                    revenueToday = report.RevenueDeliveredEur + report.RevenueInFlightEur,
                    paceProjection = Projection(report),
                    topPizza = report.TopSeller ?? "—",
                    averageStars = report.AverageStars,
                    channels = Channels(report.OrdersByChannel),
                    history = History(await bookkeeper.HistoryAsync(cancellationToken), now),
                },
                risks = forecast.Select(r => new
                {
                    severity = r.Severity.ToLowerInvariant(),
                    title = r.Risk,
                    detail = r.Detail,
                    suggestion = Mitigation(r),
                }).ToArray(),
                preOrders = preOrders.Upcoming
                    .OrderBy(p => p.When)
                    .Take(8)
                    .Select(p => new
                    {
                        pizza = p.Pizza,
                        amount = p.Amount,
                        whenLabel = p.When.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture),
                        hoursOut = Math.Round((p.When - now).TotalHours, 1),
                        name = p.Name,
                    })
                    .ToArray(),
            });
        });
    }

    /// <summary>The kitchen line, counted straight off the pizza repository.</summary>
    private static async Task<object> LineAsync(IPizzaRepository pizzas, CancellationToken cancellationToken)
    {
        const int Cap = 200;
        async Task<int> Count(PizzaState state) =>
            (await pizzas.GetByStateAsync(state, Cap, cancellationToken)).Count;

        return new
        {
            ordered = await Count(PizzaState.OrderAccepted),
            preparing = await Count(PizzaState.Preparing),
            baking = await Count(PizzaState.Baking),
            ready = await Count(PizzaState.Ready),
        };
    }

    /// <summary>Stock gauges against opening levels, with Procurement's own thresholds.</summary>
    private static object[] StockGauges(Stock stock) => Watched
        .Select(ingredient =>
        {
            var grams = stock.GramsOf(ingredient);
            var opening = Stock.InitialByName.GetValueOrDefault(ingredient.ToString(), grams);

            return new
            {
                ingredient = Humanise(ingredient),
                grams,
                // The gauge's full mark, not a historical fact: a silo the supplier has
                // topped up sits ABOVE its opening level, and a bar drawn against the
                // opening level would render past 100%. Take whichever is larger so the
                // gauge always reads as a fraction of something the silo has actually held.
                openingGrams = Math.Max(opening, grams),
                state = grams <= CrisisGrams ? "crisis" : grams <= LowGrams ? "low" : "ok",
            };
        })
        .Cast<object>()
        .ToArray();

    /// <summary>Factory channels folded into the five the cockpit shows.</summary>
    private static object Channels(IReadOnlyDictionary<string, int> byChannel)
    {
        int Of(params OrderChannel[] channels) =>
            channels.Sum(c => byChannel.GetValueOrDefault(c.ToString(), 0));

        return new
        {
            web = Of(OrderChannel.Online, OrderChannel.Guest),
            chat = Of(OrderChannel.Bot),
            copilot = Of(OrderChannel.Copilot),
            phone = Of(OrderChannel.Phone),
            walkIn = Of(OrderChannel.Restaurant, OrderChannel.Planned),
        };
    }

    /// <summary>
    /// Honest end-of-day projection: tonight's revenue plus the last ten minutes' pace
    /// carried over the hours still left in service. Never projects backwards.
    /// </summary>
    private static decimal Projection(BusinessReport report)
    {
        var booked = report.RevenueDeliveredEur + report.RevenueInFlightEur;
        if (report.OrdersToday == 0 || !report.ServiceOpen)
        {
            return booked;
        }

        var perOrder = booked / report.OrdersToday;
        return booked + (perOrder * report.ProjectedOrdersNextHour);
    }

    /// <summary>The seven-day ledger — real services where the house has them, backstory elsewhere.</summary>
    private static object[] History(IReadOnlyList<DailyLedger> ledger, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.Date);
        return ledger
            .Select(d => new
            {
                label = d.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture),
                orders = d.Orders,
                revenue = d.RevenueEur,
                isToday = d.Date == today,
            })
            .Cast<object>()
            .ToArray();
    }

    /// <summary>
    /// The mitigation Giuseppe would say out loud. Derived from severity so the cockpit
    /// always has one, even when no model is configured.
    /// </summary>
    private static string Mitigation(RiskForecast risk) => risk.Severity.ToLowerInvariant() switch
    {
        "high" => "Reorder now — don't wait for the line to notice.",
        "medium" => "Keep an eye on it; top up before the next rush.",
        _ => "Nothing to do yet — worth watching.",
    };

    /// <summary>"TomatoSauce" → "Tomato sauce", the way a menu would print it.</summary>
    private static string Humanise(Ingredient ingredient)
    {
        var raw = ingredient.ToString();
        var spaced = string.Concat(raw.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
