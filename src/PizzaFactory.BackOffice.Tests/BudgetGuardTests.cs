using PizzaFactory.BackOffice;
using PizzaFactory.Domain;

namespace PizzaFactory.BackOffice.Tests;

/// <summary>
/// Money first, then authority, then autonomy. These pin the order those rules fire in,
/// because getting it wrong is the difference between a control and a formality.
/// </summary>
public sealed class BudgetGuardTests
{
    private static PurchaseBook Book(decimal budget = 2500m, int limit = 1000) =>
        new(new BackOfficeOptions { MonthlyBudgetEur = budget, AutoApproveLimitGrams = limit },
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void a_fresh_month_starts_with_the_whole_budget_free()
    {
        var position = Book(budget: 2500m).Position();

        Assert.Equal(2500m, position.BudgetEur);
        Assert.Equal(0m, position.CommittedEur);
        Assert.Equal(2500m, position.RemainingEur);
        Assert.False(position.IsTight);
    }

    /// <summary>An order on somebody's desk is money you have very nearly spent.</summary>
    [Fact]
    public void orders_awaiting_a_signature_still_count_against_the_budget()
    {
        var book = Book();
        book.Request(Ingredient.Salami, 4000);        // over the gram limit -> pending

        var position = book.Position();

        Assert.Equal(PurchaseBook.CostOf(Ingredient.Salami, 4000), position.CommittedEur);
        Assert.Equal(1, position.OrdersCounted);
    }

    [Fact]
    public void a_purchase_that_would_breach_the_budget_is_refused_not_queued()
    {
        var book = Book(budget: 10m);

        var granted = book.Request(Ingredient.Tuna, 4000);   // €49.20, far past €10

        Assert.False(granted);
        var order = Assert.Single(book.Orders(PurchaseOrderState.BlockedByBudget));
        Assert.Equal(PurchaseDecision.OverBudget, order.Decision);
        Assert.Empty(book.Orders(PurchaseOrderState.PendingApproval));
    }

    /// <summary>
    /// The point of refusing rather than queueing: a signature cannot conjure funds, so
    /// offering one would be theatre. This must not be approvable by accident.
    /// </summary>
    [Fact]
    public void a_blocked_purchase_cannot_be_waved_through_by_approving_it()
    {
        var book = Book(budget: 10m);
        book.Request(Ingredient.Tuna, 4000);
        var blocked = Assert.Single(book.Orders(PurchaseOrderState.BlockedByBudget));

        Assert.Null(book.Approve(blocked.Id));
        Assert.Empty(book.ReadyForDelivery());
    }

    [Fact]
    public void inside_the_budget_the_usual_two_tiers_still_decide()
    {
        var book = Book(budget: 5000m, limit: 1000);

        Assert.True(book.Request(Ingredient.Flour, 1000));      // within limit -> autonomous
        Assert.False(book.Request(Ingredient.Mozzarella, 4000)); // over limit  -> needs a human

        Assert.Equal(PurchaseDecision.AutoApproved, book.Orders().First(o => o.Ingredient == Ingredient.Flour).Decision);
        Assert.Equal(PurchaseDecision.NeedsApproval, book.Orders().First(o => o.Ingredient == Ingredient.Mozzarella).Decision);
    }

    [Fact]
    public void no_budget_configured_means_no_budget_guard()
    {
        var book = Book(budget: 0m);

        book.Request(Ingredient.Tuna, 4000);

        Assert.Empty(book.Orders(PurchaseOrderState.BlockedByBudget));
    }

    /// <summary>Every refusal has to be sayable in one line, with the arithmetic in it.</summary>
    [Fact]
    public void every_decision_explains_itself_with_numbers()
    {
        var book = Book(budget: 10m);
        book.Request(Ingredient.Tuna, 4000);
        var blocked = Assert.Single(book.Orders(PurchaseOrderState.BlockedByBudget));

        var said = book.Explain(blocked);

        Assert.Contains("would breach", said, StringComparison.Ordinal);
        Assert.Contains("August 2026", said, StringComparison.Ordinal);
        Assert.Contains("budget decision", said, StringComparison.Ordinal);
    }
}
