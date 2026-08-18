using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;
using PizzaFactory.Giuseppe.Tools;

namespace PizzaFactory.Trattoria;

/// <summary>
/// The front desk hands Giuseppe his reservations book and a view of the dining room: tools to
/// list and book pre-orders and to check how the floor is doing. Registered only where the
/// trattoria runs (the Web app) — the Teams bot doesn't get tools it can't honour.
/// </summary>
public sealed class FrontDeskToolSource(
    PreOrderBook book,
    MaitreD maitreD,
    Bookkeeper bookkeeper,
    TimeProvider clock) : IGiuseppeToolSource
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(ListPreOrders, "list_pre_orders",
                "Read the reservations book: all upcoming pre-orders (pizza, amount, when, who for)."),
            AIFunctionFactory.Create(BookPreOrder, "book_pre_order",
                "Book a pre-order in the reservations book, e.g. 10x Diavolo for Saturday 18:00. " +
                "Returns a confirmation, or the reason the book refused it."),
            AIFunctionFactory.Create(DiningRoomStatus, "dining_room_status",
                "How the dining room is doing right now: service open/closed, tables free and occupied, " +
                "parties served, walkouts, and the average review stars."),
            AIFunctionFactory.Create(BusinessReportAsync, "business_report",
                "Tonight's business numbers, all from real data: orders and pizzas today, revenue (EUR, " +
                "delivered and in flight), orders by channel, sales by pizza with the top seller, guests, " +
                "walkouts, satisfaction, and a pace-based projection for the next hour."),
            AIFunctionFactory.Create(SalesHistory, "sales_history",
                "The ledger for the last seven days (orders, guests, revenue EUR, stars per day) — for " +
                "'versus a typical Tuesday' comparisons and simple trends."),
        ];

        return Task.FromResult(tools);
    }

    private string ListPreOrders()
    {
        var upcoming = book.Upcoming;
        return upcoming.Count == 0
            ? "The reservations book is empty — nothing booked ahead."
            : JsonSerializer.Serialize(
                upcoming.Select(p => new { p.Pizza, p.Amount, When = p.When.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), For = p.Name }),
                SerializerOptions);
    }

    private string BookPreOrder(
        [Description("Pizza name from the menu, e.g. 'Diavolo'.")] string pizza,
        [Description("How many pizzas (1 to 24).")] int amount,
        [Description("When the order should fire, local time, format yyyy-MM-dd HH:mm (e.g. 2026-08-22 18:00).")] string when,
        [Description("Who the pre-order is for, e.g. 'Nonna's Bingo Club'.")] string forName)
    {
        if (!DateTimeOffset.TryParseExact(when, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var parsed) &&
            !DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return $"Could not read '{when}' as a date — use the format yyyy-MM-dd HH:mm.";
        }

        var error = book.TryAdd(pizza, amount, parsed, forName, clock.GetUtcNow());
        return error ?? $"Booked: {amount}× {pizza} for {forName} at {parsed.ToLocalTime():dddd d MMMM HH:mm}. " +
            "It will fire into the oven right on time.";
    }

    private async Task<string> BusinessReportAsync(CancellationToken cancellationToken = default) =>
        JsonSerializer.Serialize(await bookkeeper.ReportAsync(cancellationToken), SerializerOptions);

    private string SalesHistory() =>
        JsonSerializer.Serialize(bookkeeper.History(), SerializerOptions);

    private string DiningRoomStatus()
    {
        var snapshot = maitreD.Snapshot();
        var occupied = snapshot.Tables.Count(t => t.Party is not null);
        return JsonSerializer.Serialize(new
        {
            ServiceOpen = snapshot.IsOpen,
            TablesOccupied = occupied,
            TablesFree = snapshot.Tables.Count - occupied,
            PartiesServedTonight = snapshot.PartiesServed,
            Walkouts = snapshot.Walkouts,
            AverageStars = snapshot.AverageStars is { } avg ? Math.Round(avg, 1) : (double?)null,
        }, SerializerOptions);
    }
}
