using PizzaFactory.BackOffice;
using PizzaFactory.Domain;

namespace PizzaFactory.BackOffice.Tests;

public sealed class PurchaseBookTests
{
    private static PurchaseBook Book(int limit = 1000) =>
        new(new BackOfficeOptions { AutoApproveLimitGrams = limit },
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void small_orders_auto_approve_the_perpetuum_mobile_stays_autonomous()
    {
        var book = Book();

        Assert.True(book.Request(Ingredient.Flour, 1000));
        var order = Assert.Single(book.Orders());
        Assert.Equal(PurchaseOrderState.Approved, order.State);
        Assert.Equal(1.80m, order.Cost);
    }

    [Fact]
    public void big_orders_wait_for_a_human_signature()
    {
        var book = Book();

        Assert.False(book.Request(Ingredient.Pineapple, 4000, "emergency replenishment (silo empty)"));
        var order = Assert.Single(book.Orders(PurchaseOrderState.PendingApproval));
        Assert.Equal(18.40m, order.Cost);
        Assert.Empty(book.ReadyForDelivery());
    }

    [Fact]
    public void one_pending_order_per_ingredient_trattoriasoft_does_not_nag()
    {
        var book = Book();
        book.Request(Ingredient.Pineapple, 4000);
        book.Request(Ingredient.Pineapple, 4000);
        book.Request(Ingredient.Pineapple, 4000);

        Assert.Single(book.Orders(PurchaseOrderState.PendingApproval));
    }

    [Fact]
    public void approval_leads_to_delivery_and_an_invoice()
    {
        var book = Book();
        book.Request(Ingredient.Pineapple, 4000);
        var pending = book.Orders(PurchaseOrderState.PendingApproval)[0];

        var approved = book.Approve(pending.Id);
        Assert.NotNull(approved);
        var ready = Assert.Single(book.ReadyForDelivery());

        book.MarkDelivered(ready.Id);
        Assert.Equal(PurchaseOrderState.Delivered, book.Orders()[0].State);
        var invoice = Assert.Single(book.Invoices());
        Assert.Equal(18.40m, invoice.Cost);
    }

    [Fact]
    public void rejection_closes_the_order_without_money_moving()
    {
        var book = Book();
        book.Request(Ingredient.Tuna, 4000);
        var pending = book.Orders(PurchaseOrderState.PendingApproval)[0];

        var rejected = book.Reject(pending.Id, "we are not made of money");

        Assert.Equal(PurchaseOrderState.Rejected, rejected!.State);
        Assert.Empty(book.ReadyForDelivery());
        Assert.Empty(book.Invoices());
    }

    [Fact]
    public void the_a2a_self_heal_leaves_a_paper_trail()
    {
        var book = Book();

        book.RecordExternalDelivery("Ananas Express GmbH", Ingredient.Pineapple, 1000);

        var order = Assert.Single(book.Orders(PurchaseOrderState.Delivered));
        Assert.Equal("Ananas Express GmbH", order.Supplier);
        Assert.Single(book.Invoices());
    }
}
