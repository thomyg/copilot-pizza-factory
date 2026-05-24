using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.FrontOfHouse;
using PizzaFactory.Infrastructure.InMemory;
using PizzaFactory.Safety;

namespace PizzaFactory.FrontOfHouse.Tests;

public class GuestOrderServiceTests
{
    private static GuestOrderService Build(out InMemoryOrderRepository orders)
    {
        orders = new InMemoryOrderRepository();
        return new GuestOrderService(orders, new HeuristicContentGuard(), new FrontOfHouseOptions(), new OrderingGate());
    }

    [Fact]
    public async Task generates_a_pseudonym_and_places_a_guest_order()
    {
        var service = Build(out var orders);

        var result = await service.PlaceAsync(new GuestOrderRequest("Hawaii", 2));

        Assert.True(result.Accepted);
        Assert.False(string.IsNullOrWhiteSpace(result.DisplayName));
        Assert.Contains(' ', result.DisplayName); // "<Flavour> <Alias>"

        var placed = Assert.Single(await orders.ListAsync());
        Assert.Equal(OrderChannel.Guest, placed.Channel);
        Assert.Equal("Hawaii", placed.ItemName);
        Assert.Equal(result.DisplayName, placed.CustomerName);
    }

    [Fact]
    public async Task accepts_a_clean_name_override()
    {
        var service = Build(out _);
        var result = await service.PlaceAsync(new GuestOrderRequest("Margherita", 1, "Pizza Pete"));
        Assert.True(result.Accepted);
        Assert.Equal("Pizza Pete", result.DisplayName);
    }

    [Fact]
    public async Task blocks_an_offensive_name_override_and_places_nothing()
    {
        var service = Build(out var orders);

        var result = await service.PlaceAsync(new GuestOrderRequest("Diavolo", 1, "shit head"));

        Assert.False(result.Accepted);
        Assert.Contains("blocked", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await orders.ListAsync());
    }

    [Fact]
    public async Task rejects_items_not_on_the_menu()
    {
        var service = Build(out _);
        var result = await service.PlaceAsync(new GuestOrderRequest("Calzone", 1));
        Assert.False(result.Accepted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task rejects_out_of_range_amounts(int amount)
    {
        var service = Build(out _);
        var result = await service.PlaceAsync(new GuestOrderRequest("Funghi", amount));
        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task rejects_when_ordering_is_closed()
    {
        var orders = new InMemoryOrderRepository();
        var gate = new OrderingGate();
        gate.Close();
        var service = new GuestOrderService(orders, new HeuristicContentGuard(), new FrontOfHouseOptions(), gate);

        var result = await service.PlaceAsync(new GuestOrderRequest("Hawaii", 1));

        Assert.False(result.Accepted);
        Assert.Empty(await orders.ListAsync());
    }
}
