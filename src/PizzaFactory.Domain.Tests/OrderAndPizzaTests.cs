using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Domain.Tests;

public class OrderAndPizzaTests
{
    [Fact]
    public void Create_sets_sensible_defaults()
    {
        var order = Order.Create("Hawaii", 2, OrderChannel.Guest, "Anchovy Anonymous");

        Assert.False(string.IsNullOrWhiteSpace(order.Id));
        Assert.Equal(OrderState.Created, order.State);
        Assert.Equal(OrderChannel.Guest, order.Channel);
        Assert.Equal("Anchovy Anonymous", order.CustomerName);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("  ", 1)]
    [InlineData("Hawaii", 0)]
    [InlineData("Hawaii", -3)]
    public void Create_rejects_invalid_input(string itemName, int amount)
    {
        Assert.ThrowsAny<ArgumentException>(() => Order.Create(itemName, amount, OrderChannel.Online));
    }

    [Fact]
    public void Order_transitions_are_immutable()
    {
        var created = Order.Create("Diavolo", 1, OrderChannel.Online);

        var started = created.Start();

        Assert.Equal(OrderState.Started, started.State);
        Assert.Equal(OrderState.Created, created.State); // original unchanged
        Assert.Equal(OrderState.Delivered, started.MarkReady().MarkDelivered().State);
    }

    [Fact]
    public void Pizza_FromOrder_copies_name_and_customer_and_starts_accepted()
    {
        var order = Order.Create("Funghi", 1, OrderChannel.Guest, "Pepperoni Picasso");

        var pizza = Pizza.FromOrder(order);

        Assert.Equal("Funghi", pizza.Name);
        Assert.Equal(order.Id, pizza.OrderId);
        Assert.Equal("Pepperoni Picasso", pizza.CustomerName);
        Assert.Equal(PizzaState.OrderAccepted, pizza.State);
    }

    [Fact]
    public void Pizza_moves_through_the_floor_immutably()
    {
        var at = new DateTimeOffset(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);
        var pizza = Pizza.FromOrder(Order.Create("Margherita", 1, OrderChannel.Restaurant));

        var preparing = pizza.BeginPreparing(at);
        var baking = preparing.BeginBaking(at.AddMinutes(5));
        var ready = baking.MarkReady(at.AddMinutes(10));

        Assert.Equal(PizzaState.OrderAccepted, pizza.State); // original unchanged
        Assert.Equal(PizzaState.Preparing, preparing.State);
        Assert.Equal(at, preparing.StartedAt);
        Assert.Equal(PizzaState.Baking, baking.State);
        Assert.Equal(PizzaState.Ready, ready.State);
        Assert.Equal(at.AddMinutes(10), ready.ReadyAt);
        Assert.Equal(PizzaState.Out, ready.SendOut().State);
    }
}
