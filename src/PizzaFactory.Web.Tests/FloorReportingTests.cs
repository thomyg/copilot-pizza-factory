using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;
using PizzaFactory.Web.Floor;

namespace PizzaFactory.Web.Tests;

public sealed class FloorReportingTests
{
    [Theory]
    [InlineData(OrderChannel.Restaurant, FloorArea.Dining)]
    [InlineData(OrderChannel.Guest, FloorArea.Dining)]
    [InlineData(OrderChannel.Planned, FloorArea.Dining)]
    [InlineData(OrderChannel.Online, FloorArea.Street)]
    [InlineData(OrderChannel.Bot, FloorArea.Street)]
    [InlineData(OrderChannel.Copilot, FloorArea.Street)]
    public async Task a_new_order_is_reported_where_it_will_be_eaten(OrderChannel channel, FloorArea area)
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var orders = new FloorReportingOrderRepository(new InMemoryOrderRepository(), bus);

        var order = await orders.AddAsync(Order.Create("Margherita", 2, channel, "the Rossi family"));

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorEventKinds.OrderPlaced, e.Kind);
        Assert.Equal(area, e.Area);
        Assert.Equal(order.Id, e.Subject);
        Assert.Equal("Giuseppe", e.Actor);
    }

    /// <summary>
    /// The simulation books fake online orders with OrderChannel.Copilot. The stage must not
    /// claim Copilot did something unless a request from Copilot actually did it.
    /// </summary>
    [Fact]
    public async Task a_simulated_copilot_order_is_still_the_agents_own_work()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var orders = new FloorReportingOrderRepository(new InMemoryOrderRepository(), bus);

        await orders.AddAsync(Order.Create("Diavolo", 1, OrderChannel.Copilot));

        Assert.Equal(FloorChannel.Autonomous, bus.Since(0).Single().Channel);
    }

    [Fact]
    public async Task updates_are_not_news()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var inner = new InMemoryOrderRepository();
        var orders = new FloorReportingOrderRepository(inner, bus);
        var order = await orders.AddAsync(Order.Create("Funghi", 1, OrderChannel.Restaurant));

        await orders.UpdateAsync(order with { State = OrderState.Started });

        Assert.Single(bus.Since(0));
        Assert.Equal(OrderState.Started, (await inner.ListAsync()).Single().State);
    }

    [Fact]
    public async Task a_critical_silo_is_reported_in_the_storeroom()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        var sink = new FloorEscalationSink(bus);

        await sink.RaiseAsync(new Escalation(Ingredient.Pineapple, 120, "Pineapple critically low", DateTimeOffset.UtcNow));

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorEventKinds.StockLow, e.Kind);
        Assert.Equal(FloorArea.Storeroom, e.Area);
        Assert.Equal("Pineapple", e.Subject);
        Assert.Equal("Procurement", e.Actor);
    }

    [Theory]
    [InlineData("Copilot", "Copilot")]
    [InlineData("teams", "Teams")]
    [InlineData("presenter", "none")]
    [InlineData(null, "none")]
    public async Task a_request_carries_the_channel_its_surface_names(string? header, string expected)
    {
        using var host = await new HostBuilderFor(app =>
        {
            app.UseFloorChannel();
            app.Run(ctx => ctx.Response.WriteAsync(FloorChannelScope.Current?.ToString() ?? "none"));
        }).StartAsync();
        var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (header is not null)
        {
            request.Headers.Add(FloorChannelScope.HeaderName, header);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(expected, await response.Content.ReadAsStringAsync());
        Assert.Null(FloorChannelScope.Current);
    }

    private sealed class HostBuilderFor(Action<IApplicationBuilder> configure)
    {
        public async Task<IHost> StartAsync()
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web => web.UseTestServer().Configure(configure))
                .Build();
            await host.StartAsync();
            return host;
        }
    }
}
