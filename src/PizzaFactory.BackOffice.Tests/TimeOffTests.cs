using PizzaFactory.BackOffice;

namespace PizzaFactory.BackOffice.Tests;

/// <summary>
/// The separation these pin down: filing works out cover, approving changes the rota, and
/// nothing else does. An approval that did not move anything would be a formality.
/// </summary>
public sealed class TimeOffTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

    private static (TimeOffBook Book, StaffBook Staff, DateOnly Friday) House()
    {
        var clock = new FixedTimeProvider(T0);
        var staff = new StaffBook(clock);
        return (new TimeOffBook(staff, clock), staff, staff.Today.AddDays(1));
    }

    /// <summary>
    /// Someone who is genuinely on the rota tomorrow. The roster rotates by date, so hard-coding
    /// a name and a slot would make these tests pass or fail depending on the calendar.
    /// </summary>
    private static (string Name, ShiftSlot Slot) SomeoneRostered(StaffBook staff, DateOnly date)
    {
        var entry = staff.Rota(date, 1).First(e => e.AssignedTo is not null && e.Role == StaffRole.Service);
        return (entry.AssignedTo!, entry.Slot);
    }

    [Fact]
    public void filing_a_request_works_out_cover_before_anyone_decides()
    {
        var (book, _, friday) = House();

        var request = book.Request("Maria", friday, ShiftSlot.Dinner, "wedding");

        Assert.Equal(TimeOffState.Pending, request.State);
        Assert.NotEmpty(request.ProposedCover);
        Assert.DoesNotContain("Maria", request.ProposedCover, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Cover has to be someone who may actually do the job.</summary>
    [Fact]
    public void proposed_cover_respects_the_role_and_its_certification()
    {
        var (book, _, friday) = House();

        var request = book.Request("Sofia", friday, ShiftSlot.Dinner, "training day");

        Assert.All(request.ProposedCover, name =>
        {
            var person = Roster.Members.Single(m => m.Name == name);
            Assert.Equal(StaffRole.Pizzaiolo, person.Role);
            Assert.True(person.OvenCertified, $"{name} is not oven-certified and must not cover a pizzaiolo shift");
        });
    }

    [Fact]
    public void filing_alone_changes_nothing_on_the_rota()
    {
        var (book, staff, friday) = House();
        var before = staff.Rota(friday, 1);

        book.Request("Maria", friday, ShiftSlot.Dinner, "wedding");

        Assert.Equal(before, staff.Rota(friday, 1));
        Assert.Empty(staff.Absences());
    }

    [Fact]
    public void approving_records_the_absence_and_hands_the_shift_to_cover()
    {
        var (book, staff, friday) = House();
        var (who, slot) = SomeoneRostered(staff, friday);
        var request = book.Request(who, friday, slot, "wedding");
        var cover = request.ProposedCover[0];

        var approved = book.Approve(request.Id);

        Assert.Equal(TimeOffState.Approved, approved!.State);
        Assert.Contains(staff.Absences(), a => a.Name == who && a.Date == friday);
        Assert.Contains(staff.Rota(friday, 1), e => e.Slot == slot && e.AssignedTo == cover);
        Assert.DoesNotContain(staff.Rota(friday, 1), e => e.Slot == slot && e.AssignedTo == who);
    }

    /// <summary>Asking for a day you were never rostered on is fine and invents no work.</summary>
    [Fact]
    public void a_day_off_from_a_shift_you_were_not_on_covers_nothing()
    {
        var (book, staff, friday) = House();
        var offRota = Roster.Members.First(m =>
            m.Role == StaffRole.Service &&
            staff.Rota(friday, 1).All(e => e.AssignedTo != m.Name));

        var request = book.Request(offRota.Name, friday, ShiftSlot.Dinner, "concert");
        var approved = book.Approve(request.Id);

        Assert.Equal(TimeOffState.Approved, approved!.State);
        Assert.Contains("not on the rota", approved.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void a_named_substitute_beats_the_suggestion()
    {
        var (book, staff, friday) = House();
        var (who, slot) = SomeoneRostered(staff, friday);
        var request = book.Request(who, friday, slot, "wedding");
        var second = request.ProposedCover.Skip(1).FirstOrDefault();
        Assert.NotNull(second);   // four in service; more than one free is the point of the roster

        book.Approve(request.Id, coverName: second);

        Assert.Contains(staff.Rota(friday, 1), e => e.Slot == slot && e.AssignedTo == second);
    }

    [Fact]
    public void declining_keeps_its_reason_and_leaves_the_rota_alone()
    {
        var (book, staff, friday) = House();
        var request = book.Request("Maria", friday, ShiftSlot.Dinner, "wedding");

        var declined = book.Decline(request.Id, "we are three short already");

        Assert.Equal(TimeOffState.Declined, declined!.State);
        Assert.Contains("three short", declined.Note, StringComparison.Ordinal);
        Assert.Empty(staff.Absences());
    }

    [Fact]
    public void a_settled_request_cannot_be_settled_twice()
    {
        var (book, staff, friday) = House();
        var (who, slot) = SomeoneRostered(staff, friday);
        var request = book.Request(who, friday, slot, "wedding");
        book.Approve(request.Id);

        Assert.Null(book.Approve(request.Id));
        Assert.Null(book.Decline(request.Id, "changed my mind"));
    }

    /// <summary>A manager should see the situation, not a yes/no with no information in it.</summary>
    [Fact]
    public void every_request_explains_itself_in_one_line()
    {
        var (book, _, friday) = House();
        var request = book.Request("Maria", friday, ShiftSlot.Dinner, "wedding");

        var said = TimeOffBook.Explain(request);

        Assert.Contains("Maria", said, StringComparison.Ordinal);
        Assert.Contains("wedding", said, StringComparison.Ordinal);
        Assert.Contains("cover", said, StringComparison.Ordinal);
    }
}
