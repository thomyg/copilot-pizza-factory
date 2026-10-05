using PizzaFactory.BackOffice;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Floor;

namespace PizzaFactory.BackOffice.Tests;

/// <summary>
/// The back office tells the stage what it decided. These pin down that every decision a
/// person or Nonna makes produces exactly one event, in the right room, with the right actor —
/// and that a refusal Procurement keeps asking for is still reported once.
/// </summary>
public sealed class FloorEventsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

    private static (PurchaseBook Book, FloorEventBus Bus) Ledger(decimal budget = 2500m)
    {
        var clock = new FixedTimeProvider(T0);
        var bus = new FloorEventBus(clock);
        return (new PurchaseBook(new BackOfficeOptions { AutoApproveLimitGrams = 1000, MonthlyBudgetEur = budget }, clock, bus), bus);
    }

    private static (TimeOffBook Book, StaffBook Staff, FloorEventBus Bus, DateOnly Day) Rota()
    {
        var clock = new FixedTimeProvider(T0);
        var bus = new FloorEventBus(clock);
        var staff = new StaffBook(clock, bus);
        return (new TimeOffBook(staff, clock, bus), staff, bus, staff.Today.AddDays(1));
    }

    [Fact]
    public void a_small_refill_is_reported_as_nonna_approving_it_herself()
    {
        var (book, bus) = Ledger();

        book.Request(Ingredient.Salt, 500);

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorEventKinds.PurchaseOrderApproved, e.Kind);
        Assert.Equal("Nonna", e.Actor);
        Assert.Equal(FloorArea.Office, e.Area);
        Assert.Equal(FloorChannel.Autonomous, e.Channel);
    }

    [Fact]
    public void a_big_order_is_reported_as_waiting_for_a_signature()
    {
        var (book, bus) = Ledger();

        book.Request(Ingredient.Pineapple, 2100);

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorEventKinds.PurchaseOrderRequested, e.Kind);
        Assert.Equal(book.Orders().Single().Id, e.Subject);
    }

    [Fact]
    public void a_signature_from_copilot_shows_the_capo_approving_via_copilot()
    {
        var (book, bus) = Ledger();
        book.Request(Ingredient.Pineapple, 2100);
        var id = book.Orders().Single().Id;

        using (FloorChannelScope.Enter(FloorChannel.Copilot))
        {
            book.Approve(id);
        }

        var e = bus.Since(1).Single();
        Assert.Equal(FloorEventKinds.PurchaseOrderApproved, e.Kind);
        Assert.Equal("Capo", e.Actor);
        Assert.Equal(FloorChannel.Copilot, e.Channel);
        Assert.Equal(id, e.Subject);
    }

    [Fact]
    public void nonna_acting_on_an_instruction_is_named_as_herself()
    {
        var (book, bus) = Ledger();
        book.Request(Ingredient.Pineapple, 2100);
        var id = book.Orders().Single().Id;

        book.Reject(id, "we stretch the pineapple", actor: "Nonna");

        var e = bus.Since(1).Single();
        Assert.Equal(FloorEventKinds.PurchaseOrderRejected, e.Kind);
        Assert.Equal("Nonna", e.Actor);
    }

    [Fact]
    public void approving_an_unknown_order_reports_nothing()
    {
        var (book, bus) = Ledger();

        Assert.Null(book.Approve("PO-404"));
        Assert.Empty(bus.Since(0));
    }

    /// <summary>Procurement asks every tick; the stage must not flash the same refusal sixty times.</summary>
    [Fact]
    public void a_budget_refusal_is_reported_once_however_often_it_is_asked()
    {
        var (book, bus) = Ledger(budget: 5m);

        for (var i = 0; i < 5; i++)
        {
            book.Request(Ingredient.Tuna, 3000);
        }

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorEventKinds.PurchaseOrderBlocked, e.Kind);
    }

    [Fact]
    public void a_delivery_shows_up_in_the_storeroom_and_its_invoice_in_the_office()
    {
        var (book, bus) = Ledger();
        book.Request(Ingredient.Salt, 500);
        var id = book.Orders().Single().Id;

        book.MarkDelivered(id);

        var kinds = bus.Since(1).Select(e => (e.Kind, e.Area)).ToList();
        Assert.Equal(
            [(FloorEventKinds.StockReceived, FloorArea.Storeroom), (FloorEventKinds.InvoiceFiled, FloorArea.Office)],
            kinds);
    }

    [Fact]
    public void the_supplier_agents_self_heal_is_reported_as_the_supplier()
    {
        var (book, bus) = Ledger();

        book.RecordExternalDelivery("Fruttivendolo Marittimo S.r.l.", Ingredient.Mozzarella, 4000);

        Assert.Equal(["Supplier", "Nonna"], bus.Since(0).Select(e => e.Actor));
    }

    [Fact]
    public void a_time_off_request_is_pinned_up_for_the_stage()
    {
        var (book, _, bus, day) = Rota();

        var request = book.Request("Luca", day, ShiftSlot.Lunch, "exam at the culinary school");

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorEventKinds.TimeOffRequested, e.Kind);
        Assert.Equal(request.Id, e.Subject);
        Assert.Equal(FloorArea.BreakRoom, e.Area);
    }

    [Fact]
    public void approving_time_off_is_reported_and_so_is_whatever_it_did_to_the_rota()
    {
        var (book, staff, bus, day) = Rota();
        var rostered = staff.Rota(day, 1).First(r => r.AssignedTo is not null);
        var request = book.Request(rostered.AssignedTo!, day, rostered.Slot, "moving flat");
        var before = bus.Since(0).Last().Seq;

        using (FloorChannelScope.Enter(FloorChannel.Teams))
        {
            book.Approve(request.Id);
        }

        var after = bus.Since(before);
        Assert.Contains(after, e => e.Kind == FloorEventKinds.TimeOffApproved && e.Channel == FloorChannel.Teams && e.Actor == "Capo");
        Assert.Contains(after, e => e.Kind == FloorEventKinds.AbsenceReported);
    }

    /// <summary>The roster is the house's cast; someone else filing through Teams is a real person and stays unnamed.</summary>
    [Fact]
    public void a_name_from_outside_the_roster_never_reaches_the_stage()
    {
        var (book, _, bus, day) = Rota();

        book.Request("Thomas Example", day, ShiftSlot.Dinner, "wedding");

        var e = bus.Since(0).Single();
        Assert.Equal("A colleague", e.Actor);
        Assert.DoesNotContain("Thomas", e.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void declining_time_off_is_reported()
    {
        var (book, _, bus, day) = Rota();
        var request = book.Request("Rosa", day, ShiftSlot.Dinner, "football final");

        book.Decline(request.Id, "busy night");

        Assert.Equal(FloorEventKinds.TimeOffDeclined, bus.Since(1).Single().Kind);
    }

    [Fact]
    public void the_books_still_work_without_a_stage()
    {
        var book = new PurchaseBook(new BackOfficeOptions(), new FixedTimeProvider(T0));

        Assert.True(book.Request(Ingredient.Salt, 500));
    }
}
