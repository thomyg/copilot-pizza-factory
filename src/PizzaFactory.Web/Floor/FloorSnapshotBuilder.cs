using System.Globalization;
using PizzaFactory.BackOffice;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Domain.Recipes;
using PizzaFactory.Factory;
using PizzaFactory.Trattoria;

namespace PizzaFactory.Web.Floor;

/// <summary>Reads the whole house for the stage. Every source is the live one the floor runs on.</summary>
public sealed class FloorSnapshotBuilder(
    FloorEventBus bus,
    ServiceWindow service,
    FactoryOptions factory,
    IPizzaRepository pizzas,
    IRestingDoughRepository doughs,
    IStockRepository stock,
    MaitreD maitreD,
    OnlineOrderDesk desk,
    PurchaseBook purchases,
    TimeOffBook timeOff,
    StaffBook staff,
    BackOfficeOptions backOffice,
    FloorSignalLedger signalled,
    TimeProvider clock)
{
    /// <summary>More than the line can hold; a backlog past this is a number, not a picture.</summary>
    private const int PizzasPerState = 60;

    /// <summary>How stale a shared read may be. Every open stage asks twice a second; the store is read once.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMilliseconds(400);

    private readonly SemaphoreSlim _cacheGate = new(1, 1);
    private FloorSnapshot? _cached;

    /// <summary>The latest read, shared between viewers; rebuilt when older than <see cref="MaxAge"/>.</summary>
    public async Task<FloorSnapshot> CurrentAsync(CancellationToken cancellationToken = default)
    {
        if (Fresh(_cached) is { } hit)
        {
            return hit;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            return Fresh(_cached) ?? (_cached = await BuildAsync(cancellationToken));
        }
        finally
        {
            _cacheGate.Release();
        }
    }

    private FloorSnapshot? Fresh(FloorSnapshot? snapshot) =>
        snapshot is not null && clock.GetUtcNow() - snapshot.At < MaxAge && snapshot.Seq == bus.LastSeq ? snapshot : null;

    public async Task<FloorSnapshot> BuildAsync(CancellationToken cancellationToken = default)
    {
        // Read the sequence first: an event published during the read is then re-delivered
        // rather than lost, and the stage treats events idempotently anyway.
        var seq = bus.LastSeq;
        var house = maitreD.Snapshot();
        var tableOfOrder = house.Tables
            .Where(t => t.Party?.OrderId is not null)
            .ToDictionary(t => t.Party!.OrderId!, t => t.Table.Id);

        return new FloorSnapshot(
            seq,
            clock.GetUtcNow(),
            new ServiceView(service.IsOpen, service.Remaining is { } left ? Math.Round(left.TotalMinutes, 1) : null),
            new KitchenView(
                factory.FridgeCapacity,
                factory.PrepCapacity,
                factory.OvenCapacity,
                await LineAsync(tableOfOrder, cancellationToken),
                await DoughsAsync(cancellationToken)),
            Pantry(await stock.GetAsync(cancellationToken)),
            new DiningView(
                [.. house.Tables.Select(Table)],
                house.GuestsServed,
                house.Walkouts,
                house.AverageStars),
            [.. desk.Tickets.Select(t => new TicketView(
                t.OrderId, t.Mode.ToString(), t.Customer, t.Pizza, t.Amount, t.State.ToString(), t.PlacedAt, t.ReadyAt,
                signalled.SurfaceOf(t.OrderId)))],
            Office(),
            Brigade());
    }

    private async Task<IReadOnlyList<PizzaView>> LineAsync(
        IReadOnlyDictionary<string, int> tableOfOrder, CancellationToken cancellationToken)
    {
        var line = new List<PizzaView>();
        foreach (var state in (PizzaState[])[PizzaState.OrderAccepted, PizzaState.Preparing, PizzaState.Baking, PizzaState.Ready])
        {
            foreach (var p in await pizzas.GetByStateAsync(state, PizzasPerState, cancellationToken))
            {
                var recipe = RecipeCatalog.FindPizza(p.Name);
                line.Add(new PizzaView(
                    p.Id,
                    p.Name,
                    p.OrderId,
                    p.State.ToString(),
                    p.StartedAt,
                    recipe?.PreparingTime.TotalSeconds ?? 0,
                    recipe?.BakingTime.TotalSeconds ?? 0,
                    tableOfOrder.TryGetValue(p.OrderId, out var table) ? table : null,
                    signalled.SurfaceOf(p.OrderId)));
            }
        }

        return line;
    }

    private async Task<IReadOnlyList<DoughView>> DoughsAsync(CancellationToken cancellationToken)
    {
        var resting = await doughs.GetByStateAsync(DoughState.Resting, cancellationToken);
        var ready = await doughs.GetByStateAsync(DoughState.Ready, cancellationToken);
        return [.. resting.Concat(ready).Select(d => new DoughView(d.Id, d.State.ToString(), d.StartedAt, d.FinishesAt))];
    }

    private static IReadOnlyList<StockView> Pantry(Stock current) =>
        [.. Enum.GetValues<Ingredient>().Select(i =>
        {
            var grams = current.GramsOf(i);
            var opening = Stock.InitialByName.GetValueOrDefault(i.ToString(), 0);
            return new StockView(i.ToString(), grams, opening, opening > 0 ? Math.Round(Math.Min(1, (double)grams / opening), 3) : 0);
        })];

    private static TableSnap Table(TableView view)
    {
        var t = view.Table;
        var p = view.Party;
        return new TableSnap(
            t.Id, t.Seats, t.Shape.ToString(), t.X, t.Y, t.W, t.H,
            p is null ? null : new PartyView(p.Name, p.Size, p.State.ToString(), p.SinceUtc, p.OrderId, p.OrderedAtUtc, p.Wish));
    }

    private OfficeView Office()
    {
        var position = purchases.Position();
        return new OfficeView(
            new BudgetView(position.Period, position.BudgetEur, position.CommittedEur, position.RemainingEur, position.UsedPercent, position.IsTight),
            backOffice.AutoApproveLimitGrams,
            [.. purchases.Orders().Take(12).Select(o => new PurchaseOrderView(
                o.Id, o.Ingredient.ToString(), o.Grams, o.Cost, o.Supplier, o.State.ToString(), o.Decision.ToString(), o.At, purchases.Explain(o)))],
            [.. purchases.Invoices().Take(8).Select(i => new InvoiceView(i.Id, i.Supplier, i.Ingredient.ToString(), i.Grams, i.Cost, i.At))],
            [.. timeOff.Requests().Take(8).Select(r => new TimeOffView(
                r.Id, TimeOffBook.OnStage(r.Name), r.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture), r.Slot?.ToString(), r.Reason,
                r.State.ToString(), r.ProposedCover, r.LeavesAGap, r.At))],
            [.. staff.Rota(staff.Today, 3).Select(e => new RotaView(
                e.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture), e.Slot.ToString(), e.Role.ToString(), e.AssignedTo))]);
    }

    private IReadOnlyList<StaffView> Brigade()
    {
        var today = staff.Today;
        var absent = staff.Absences().Where(a => a.Date == today).Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. Roster.Members.Select(m => new StaffView(m.Name, m.Role.ToString(), m.OvenCertified, m.Quirk, absent.Contains(m.Name)))];
    }
}
