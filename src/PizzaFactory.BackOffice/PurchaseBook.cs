using PizzaFactory.Domain;

namespace PizzaFactory.BackOffice;

public enum PurchaseOrderState
{
    PendingApproval,
    Approved,
    Rejected,
    Delivered,

    /// <summary>Refused by policy, not by a person — the spend would breach the period budget.</summary>
    BlockedByBudget,
}

/// <summary>Which rule decided a requisition, so the answer can be explained rather than asserted.</summary>
public enum PurchaseDecision
{
    /// <summary>Within the autonomous limit — the agent stopped the bleeding and moved on.</summary>
    AutoApproved,

    /// <summary>Above the limit: a person has to sign.</summary>
    NeedsApproval,

    /// <summary>Would take the period past its budget. Nobody may wave this through casually.</summary>
    OverBudget,
}

public sealed record PurchaseOrder(
    string Id,
    Ingredient Ingredient,
    int Grams,
    decimal Cost,
    string Supplier,
    PurchaseOrderState State,
    DateTimeOffset At,
    string? Note,
    PurchaseDecision Decision = PurchaseDecision.AutoApproved);

/// <summary>Where the period's money has gone, and how much room is left.</summary>
public sealed record BudgetPosition(
    string Period,
    decimal BudgetEur,
    decimal CommittedEur,
    decimal RemainingEur,
    int OrdersCounted)
{
    public double UsedPercent => BudgetEur <= 0 ? 0 : Math.Round((double)(CommittedEur / BudgetEur) * 100, 1);

    /// <summary>Past this the back office starts warning rather than nodding along.</summary>
    public bool IsTight => UsedPercent >= 80;
}

public sealed record Invoice(
    string Id,
    string Supplier,
    Ingredient Ingredient,
    int Grams,
    decimal Cost,
    DateTimeOffset At);

public sealed class BackOfficeOptions
{
    public const string SectionName = "BackOffice";

    /// <summary>Orders up to this size auto-approve — the perpetuum mobile stays autonomous.</summary>
    public int AutoApproveLimitGrams { get; set; } = 1000;

    /// <summary>
    /// What the house may spend on supplies in a calendar month. A single approval limit says
    /// nothing about whether the money exists; a budget does. Set to zero to switch the guard
    /// off entirely — a demo without a budget is a demo about approvals, not about money.
    /// </summary>
    public decimal MonthlyBudgetEur { get; set; } = 2500m;

    public string DefaultSupplier { get; set; } = "Fruttivendolo Marittimo S.r.l.";
}

/// <summary>
/// TrattoriaSoft's procurement ledger. Small refills auto-approve and keep the factory
/// autonomous; anything bigger becomes a PENDING purchase order and waits for a human —
/// agents stop the bleeding, big money asks permission. Every delivery leaves an invoice,
/// including the A2A supplier's self-heal restocks: no restock without a paper trail.
/// </summary>
public sealed class PurchaseBook(BackOfficeOptions options, TimeProvider? clock = null)
{
    // Supplier price list, € per kg — boringly plausible, like a real ERP master-data table.
    private static readonly Dictionary<Ingredient, decimal> PricePerKg = new()
    {
        [Ingredient.Flour] = 1.80m,
        [Ingredient.Water] = 0.10m,
        [Ingredient.Salt] = 0.90m,
        [Ingredient.Yeast] = 6.50m,
        [Ingredient.TomatoSauce] = 3.20m,
        [Ingredient.Mozzarella] = 8.90m,
        [Ingredient.Ham] = 9.80m,
        [Ingredient.Salami] = 11.40m,
        [Ingredient.Pineapple] = 4.60m,
        [Ingredient.Mushroom] = 7.20m,
        [Ingredient.Tuna] = 12.30m,
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly List<PurchaseOrder> _orders = [];
    private readonly List<Invoice> _invoices = [];
    private int _nextNumber = 1000;

    public static decimal CostOf(Ingredient ingredient, int grams) =>
        Math.Round(PricePerKg.GetValueOrDefault(ingredient, 5m) * grams / 1000m, 2);

    /// <summary>
    /// Files a purchase order for a refill. Returns true when the order auto-approved and the
    /// refill may be applied immediately; false when it waits for a human or was refused.
    ///
    /// Three rules, in order, and the order matters: money first, then authority, then autonomy.
    /// A requisition that would take the month past its budget is refused outright — a signature
    /// cannot conjure funds, so offering one would be theatre. Below that, size decides whether
    /// the house may act alone or has to ask.
    /// </summary>
    public bool Request(Ingredient ingredient, int grams, string? note = null)
    {
        lock (_gate)
        {
            var cost = CostOf(ingredient, grams);

            if (options.MonthlyBudgetEur > 0 && PositionUnlocked().RemainingEur < cost)
            {
                // Record the refusal ONCE. Procurement asks again every tick — it has no memory
                // and should not need one — but a ledger that files the same rejection sixty
                // times a minute is noise, not a paper trail. It stands until the budget or the
                // month changes, at which point the next ask files a fresh one.
                var now = _clock.GetUtcNow();
                if (_orders.Any(o => o.Ingredient == ingredient
                                     && o.State == PurchaseOrderState.BlockedByBudget
                                     && o.At.Year == now.Year && o.At.Month == now.Month))
                {
                    return false;
                }

                _orders.Add(new PurchaseOrder(
                    $"PO-{_nextNumber++}", ingredient, grams, cost, options.DefaultSupplier,
                    PurchaseOrderState.BlockedByBudget, _clock.GetUtcNow(),
                    note is null
                        ? $"blocked — €{cost:0.00} would breach this month's budget"
                        : $"{note} — blocked, €{cost:0.00} would breach this month's budget",
                    PurchaseDecision.OverBudget));
                return false;
            }

            var auto = grams <= options.AutoApproveLimitGrams;

            // One PENDING order per ingredient — TrattoriaSoft does not nag twice per tick.
            // The guard deliberately applies to orders that would also need a signature:
            // blocking auto-approvable refills too would mean an ingredient that once ran
            // dry could never be topped up again until a human signed, and the line would
            // starve behind its own paperwork. A small refill and a bulk order awaiting
            // approval are different things, and only the second one queues.
            if (!auto && _orders.Any(o => o.Ingredient == ingredient && o.State == PurchaseOrderState.PendingApproval))
            {
                return false;
            }

            var order = new PurchaseOrder(
                $"PO-{_nextNumber++}",
                ingredient,
                grams,
                CostOf(ingredient, grams),
                options.DefaultSupplier,
                auto ? PurchaseOrderState.Approved : PurchaseOrderState.PendingApproval,
                _clock.GetUtcNow(),
                note ?? (auto ? "auto-approved (within limit)" : "over limit — awaiting approval"),
                auto ? PurchaseDecision.AutoApproved : PurchaseDecision.NeedsApproval);
            _orders.Add(order);
            return auto;
        }
    }

    /// <summary>
    /// Where this month's supplies money stands. Committed means everything the house has
    /// promised — approved, delivered, and orders still waiting on a signature — because a
    /// requisition on someone's desk is money you have very nearly spent.
    /// </summary>
    public BudgetPosition Position()
    {
        lock (_gate)
        {
            return PositionUnlocked();
        }
    }

    private BudgetPosition PositionUnlocked()
    {
        var now = _clock.GetUtcNow();
        var counted = _orders
            .Where(o => o.At.Year == now.Year && o.At.Month == now.Month)
            .Where(o => o.State is PurchaseOrderState.Approved
                                or PurchaseOrderState.Delivered
                                or PurchaseOrderState.PendingApproval)
            .ToList();

        var committed = counted.Sum(o => o.Cost);
        return new BudgetPosition(
            now.ToString("MMMM yyyy"),
            options.MonthlyBudgetEur,
            committed,
            Math.Max(0, options.MonthlyBudgetEur - committed),
            counted.Count);
    }

    /// <summary>
    /// The sentence Nonna says. Every refusal in a back office should be explainable in one
    /// line, with the arithmetic in it — "computer says no" is not a process.
    /// </summary>
    public string Explain(PurchaseOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var position = Position();
        return order.Decision switch
        {
            PurchaseDecision.OverBudget =>
                $"€{order.Cost:0.00} for {order.Grams}g of {order.Ingredient} would breach {position.Period}: " +
                $"€{position.RemainingEur:0.00} left of €{position.BudgetEur:0.00}. Needs a budget decision, not a signature.",
            PurchaseDecision.NeedsApproval =>
                $"{order.Grams}g is over the {options.AutoApproveLimitGrams}g I may approve alone — " +
                $"€{order.Cost:0.00}, waiting for a signature. {position.RemainingEur:0.00}€ still in {position.Period}.",
            _ =>
                $"{order.Grams}g of {order.Ingredient} at €{order.Cost:0.00} — within my limit, so I handled it. " +
                $"{position.UsedPercent}% of {position.Period} used.",
        };
    }

    /// <summary>Approve a pending order. Returns the order, or null when the id is unknown/not pending.</summary>
    public PurchaseOrder? Approve(string id) => Transition(id, PurchaseOrderState.Approved, "approved");

    public PurchaseOrder? Reject(string id, string reason) => Transition(id, PurchaseOrderState.Rejected, $"rejected: {reason}");

    /// <summary>Approved orders ready for delivery (the worker applies them to real stock).</summary>
    public IReadOnlyList<PurchaseOrder> ReadyForDelivery()
    {
        lock (_gate)
        {
            return [.. _orders.Where(o => o.State == PurchaseOrderState.Approved)];
        }
    }

    /// <summary>Marks an approved order delivered and books its invoice.</summary>
    public void MarkDelivered(string id)
    {
        lock (_gate)
        {
            var index = _orders.FindIndex(o => o.Id == id && o.State == PurchaseOrderState.Approved);
            if (index < 0)
            {
                return;
            }

            var order = _orders[index] with { State = PurchaseOrderState.Delivered };
            _orders[index] = order;
            _invoices.Add(new Invoice(
                $"INV-{order.Id}", order.Supplier, order.Ingredient, order.Grams, order.Cost, _clock.GetUtcNow()));
        }
    }

    /// <summary>The A2A self-heal path books its paper trail here: delivered order + invoice in one step.</summary>
    public void RecordExternalDelivery(string supplier, Ingredient ingredient, int grams)
    {
        lock (_gate)
        {
            var order = new PurchaseOrder(
                $"PO-{_nextNumber++}", ingredient, grams, CostOf(ingredient, grams), supplier,
                PurchaseOrderState.Delivered, _clock.GetUtcNow(), "A2A self-heal delivery");
            _orders.Add(order);
            _invoices.Add(new Invoice($"INV-{order.Id}", supplier, ingredient, grams, order.Cost, _clock.GetUtcNow()));
        }
    }

    public IReadOnlyList<PurchaseOrder> Orders(PurchaseOrderState? state = null)
    {
        lock (_gate)
        {
            return [.. _orders.Where(o => state is null || o.State == state).OrderByDescending(o => o.At)];
        }
    }

    public IReadOnlyList<Invoice> Invoices()
    {
        lock (_gate)
        {
            return [.. _invoices.OrderByDescending(i => i.At)];
        }
    }

    private PurchaseOrder? Transition(string id, PurchaseOrderState to, string note)
    {
        lock (_gate)
        {
            var index = _orders.FindIndex(o => o.Id.Equals(id?.Trim(), StringComparison.OrdinalIgnoreCase)
                && o.State == PurchaseOrderState.PendingApproval);
            if (index < 0)
            {
                return null;
            }

            var order = _orders[index] with { State = to, Note = note };
            _orders[index] = order;
            return order;
        }
    }
}
