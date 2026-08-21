using PizzaFactory.Domain;

namespace PizzaFactory.BackOffice;

public enum PurchaseOrderState
{
    PendingApproval,
    Approved,
    Rejected,
    Delivered,
}

public sealed record PurchaseOrder(
    string Id,
    Ingredient Ingredient,
    int Grams,
    decimal Cost,
    string Supplier,
    PurchaseOrderState State,
    DateTimeOffset At,
    string? Note);

public sealed record Invoice(
    string Id,
    string Supplier,
    Ingredient Ingredient,
    int Grams,
    decimal Cost,
    DateTimeOffset At);

public sealed class BackOfficeOptions
{
    /// <summary>Orders up to this size auto-approve — the perpetuum mobile stays autonomous.</summary>
    public int AutoApproveLimitGrams { get; set; } = 1000;

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
    /// refill may be applied immediately; false when it waits for a human.
    /// </summary>
    public bool Request(Ingredient ingredient, int grams, string? note = null)
    {
        lock (_gate)
        {
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
                note ?? (auto ? "auto-approved (within limit)" : "over limit — awaiting approval"));
            _orders.Add(order);
            return auto;
        }
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
