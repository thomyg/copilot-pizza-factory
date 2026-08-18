using System.Globalization;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Trattoria;

public sealed record PreOrder(string Id, string Pizza, int Amount, DateTimeOffset When, string Name);

/// <summary>
/// The reservation ledger: "10 Salami for Saturday 18:00." Entries wait in the book and fire as
/// REAL orders (channel: Planned) the moment they come due — the factory picks them up like any
/// other ticket. Validation at the boundary: menu pizzas only, sane amounts, future times.
/// </summary>
public sealed class PreOrderBook(IOrderRepository orders, TrattoriaFeed feed)
{
    public const int MaxAmount = 24;

    private readonly Lock _gate = new();
    private readonly List<PreOrder> _book = [];

    public IReadOnlyList<PreOrder> Upcoming
    {
        get { lock (_gate) { return [.. _book.OrderBy(p => p.When)]; } }
    }

    /// <summary>Adds a pre-order. Returns an error message for humans, or null when accepted.</summary>
    public string? TryAdd(string pizza, int amount, DateTimeOffset when, string name, DateTimeOffset now)
    {
        var recipe = RecipeCatalog.FindPizza(pizza);
        if (recipe is null)
        {
            return $"'{pizza}' is not on the menu.";
        }

        if (amount is < 1 or > MaxAmount)
        {
            return $"Amount must be between 1 and {MaxAmount} — for more, call Giuseppe and bring a good story.";
        }

        if (when <= now)
        {
            return "That moment has already happened. Pre-orders need a future date.";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "A name for the order, per favore — 'mystery guest' confuses the courier.";
        }

        var preOrder = new PreOrder(Guid.NewGuid().ToString("n"), recipe.Name, amount, when, name.Trim());
        lock (_gate)
        {
            _book.Add(preOrder);
        }

        feed.Post(now, $"📅 Pre-order booked: {amount}× {recipe.Name} for {name.Trim()} at {when.ToLocalTime():ddd d MMM HH:mm}.");
        return null;
    }

    /// <summary>
    /// Books from a free-text date ("yyyy-MM-dd HH:mm") — the one implementation behind every
    /// concierge tool. Returns a confirmation, or the reason the book refused.
    /// </summary>
    public string BookFromText(string pizza, int amount, string when, string forName, DateTimeOffset now)
    {
        if (!DateTimeOffset.TryParseExact(when, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var parsed) &&
            !DateTimeOffset.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return $"Could not read '{when}' as a date — use the format yyyy-MM-dd HH:mm.";
        }

        return TryAdd(pizza, amount, parsed, forName, now)
            ?? $"Reservation booked: {amount}× {pizza} for {forName} at {parsed.ToLocalTime():dddd d MMMM HH:mm}. We'll fire the ovens right on time.";
    }

    public void Cancel(string id)
    {
        lock (_gate)
        {
            _book.RemoveAll(p => p.Id == id);
        }
    }

    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        List<PreOrder> due;
        lock (_gate)
        {
            due = [.. _book.Where(p => p.When <= now)];
            _book.RemoveAll(p => p.When <= now);
        }

        foreach (var preOrder in due)
        {
            await orders.AddAsync(
                Order.Create(preOrder.Pizza, preOrder.Amount, OrderChannel.Planned, $"{preOrder.Name} (pre-order)"),
                cancellationToken);
            feed.Post(now, $"⏰ Pre-order fired: {preOrder.Amount}× {preOrder.Pizza} for {preOrder.Name} — the oven says ciao.");
        }
    }
}
