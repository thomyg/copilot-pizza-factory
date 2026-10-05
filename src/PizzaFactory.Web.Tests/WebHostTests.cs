using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Web.Floor;

namespace PizzaFactory.Web.Tests;

/// <summary>The real host, in-memory store: the floor is wired the way the stage will meet it.</summary>
public sealed class WebHostTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task an_order_placed_through_the_host_reaches_the_floor_bus()
    {
        var orders = factory.Services.GetRequiredService<IOrderRepository>();
        var bus = factory.Services.GetRequiredService<FloorEventBus>();

        var order = await orders.AddAsync(Order.Create("Margherita", 1, OrderChannel.Guest));

        Assert.IsType<FloorReportingOrderRepository>(orders);
        Assert.Contains(bus.Since(0), e => e.Subject == order.Id && e.Kind == FloorEventKinds.OrderPlaced);
    }

    [Fact]
    public void the_house_shares_one_bus()
    {
        Assert.Same(
            factory.Services.GetRequiredService<FloorEventBus>(),
            factory.Services.GetRequiredService<IFloorEvents>());
    }

    [Fact]
    public void opening_the_service_is_reported()
    {
        var window = factory.Services.GetRequiredService<ServiceWindow>();
        var bus = factory.Services.GetRequiredService<FloorEventBus>();

        window.Open();

        Assert.Contains(bus.Since(0), e => e.Kind == FloorEventKinds.ServiceOpened);
    }
}
