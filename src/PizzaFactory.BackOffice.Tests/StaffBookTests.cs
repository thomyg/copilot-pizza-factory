using PizzaFactory.BackOffice;

namespace PizzaFactory.BackOffice.Tests;

public sealed class StaffBookTests
{
    // A Wednesday — deterministic rota rotation.
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);

    private static StaffBook Book() => new(new FixedTimeProvider(Now));

    [Fact]
    public void the_rota_fills_every_seat_with_qualified_people()
    {
        var rota = Book().Rota(new DateOnly(2026, 8, 19), 7);

        Assert.Equal(7 * (2 + 4), rota.Count); // lunch 2 seats + dinner 4 seats per day
        Assert.All(rota, e => Assert.NotNull(e.AssignedTo));
        foreach (var entry in rota.Where(e => e.Role == StaffRole.Pizzaiolo))
        {
            var member = Roster.Find(entry.AssignedTo!)!;
            Assert.True(member.OvenCertified, $"{member.Name} on a Pizzaiolo seat without certification");
        }
    }

    [Fact]
    public void a_sick_call_opens_exactly_the_absentees_seats()
    {
        var book = Book();
        var date = book.Today;
        var rota = book.Rota(date, 1);
        var victim = rota.First(e => e.Slot == ShiftSlot.Dinner && e.Role == StaffRole.Service).AssignedTo!;

        var opened = book.ReportAbsence(victim, date, ShiftSlot.Dinner);

        Assert.Single(opened);
        Assert.Equal(StaffRole.Service, opened[0].Role);
        Assert.Contains(book.Rota(date, 1), e => e.Slot == ShiftSlot.Dinner && e.AssignedTo is null);
    }

    [Fact]
    public void cover_candidates_are_qualified_available_and_not_already_working()
    {
        var book = Book();
        var date = book.Today;
        var rota = book.Rota(date, 1);
        var working = rota.Where(e => e.Slot == ShiftSlot.Dinner).Select(e => e.AssignedTo).ToHashSet();
        var victim = rota.First(e => e.Slot == ShiftSlot.Dinner && e.Role == StaffRole.Service).AssignedTo!;
        book.ReportAbsence(victim, date, ShiftSlot.Dinner);

        var candidates = book.FindCover(date, ShiftSlot.Dinner, StaffRole.Service);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, c =>
        {
            Assert.NotEqual(victim, c.Name);
            Assert.DoesNotContain(c.Name, working);
        });
    }

    [Fact]
    public void the_fire_has_rules_uncertified_apprentices_stay_off_pizzaiolo_seats()
    {
        var book = Book();

        var candidates = book.FindCover(book.Today.AddDays(1), ShiftSlot.Lunch, StaffRole.Pizzaiolo);
        Assert.DoesNotContain(candidates, c => c.Name == "Giulia");

        var ex = Assert.Throws<InvalidOperationException>(
            () => book.Assign("Giulia", book.Today.AddDays(1), ShiftSlot.Dinner, StaffRole.Pizzaiolo));
        Assert.Contains("oven-certified", ex.Message);
    }

    [Fact]
    public void assigning_cover_closes_the_open_seat()
    {
        var book = Book();
        var date = book.Today;
        var victim = book.Rota(date, 1).First(e => e.Slot == ShiftSlot.Dinner && e.Role == StaffRole.Service).AssignedTo!;
        book.ReportAbsence(victim, date, ShiftSlot.Dinner);
        var cover = book.FindCover(date, ShiftSlot.Dinner, StaffRole.Service)[0];

        var entry = book.Assign(cover.Name, date, ShiftSlot.Dinner, StaffRole.Service);

        Assert.Equal(cover.Name, entry.AssignedTo);
        Assert.DoesNotContain(
            book.Rota(date, 1),
            e => e.Slot == ShiftSlot.Dinner && e.Role == StaffRole.Service && e.AssignedTo is null);
    }
}
