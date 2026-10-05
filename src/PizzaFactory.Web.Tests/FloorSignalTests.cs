using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Trattoria;

namespace PizzaFactory.Web.Tests;

/// <summary>The MCP server's "Giuseppe just ordered, via Copilot" reaching the stage.</summary>
public sealed class FloorSignalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private T Get<T>() where T : notnull => factory.Services.GetRequiredService<T>();

    [Fact]
    public async Task an_order_the_house_already_has_is_announced_with_its_channel()
    {
        var order = await Get<IOrderRepository>().AddAsync(Order.Create("Diavolo", 3, OrderChannel.Copilot, "Retro team"));
        var since = Get<FloorEventBus>().LastSeq;

        var response = await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(FloorEventKinds.OrderPlaced, order.Id, "Diavolo", 3, "Retro team", "copilot"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var e = Assert.Single(Get<FloorEventBus>().Since(since));
        Assert.Equal(FloorChannel.Copilot, e.Channel);
        Assert.Equal(order.Id, e.Subject);
        Assert.Equal(FloorArea.Street, e.Area);
    }

    /// <summary>Locally the MCP server keeps its own in-memory store; the house adopts the order so it gets baked.</summary>
    [Fact]
    public async Task an_order_the_house_has_never_seen_is_adopted_and_cooked()
    {
        var id = Guid.NewGuid().ToString("n");
        var since = Get<FloorEventBus>().LastSeq;

        var response = await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(FloorEventKinds.OrderPlaced, id, "Funghi", 2, "Standup", "teams"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var order = Assert.Single(await Get<IOrderRepository>().ListAsync(), o => o.Id == id);
        Assert.Equal(2, order.Amount);
        var e = Assert.Single(Get<FloorEventBus>().Since(since));
        Assert.Equal(FloorChannel.Teams, e.Channel);
    }

    [Fact]
    public async Task the_order_gets_a_ticket_so_a_courier_takes_it()
    {
        var id = Guid.NewGuid().ToString("n");

        await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(FloorEventKinds.OrderPlaced, id, "Hawaii", 1, "Night shift", "copilot"));

        var ticket = Assert.Single(Get<OnlineOrderDesk>().Tickets, t => t.OrderId == id);
        Assert.Equal(FulfilmentMode.Delivery, ticket.Mode);
    }

    [Fact]
    public async Task announcing_the_same_order_twice_is_harmless()
    {
        var id = Guid.NewGuid().ToString("n");
        var client = factory.CreateClient();
        var signal = new FloorSignal(FloorEventKinds.OrderPlaced, id, "Margherita", 1, null, "copilot");

        await client.PostAsJsonAsync(FloorSignal.Path, signal);
        await client.PostAsJsonAsync(FloorSignal.Path, signal);

        Assert.Single(await Get<IOrderRepository>().ListAsync(), o => o.Id == id);
        Assert.Single(Get<OnlineOrderDesk>().Tickets, t => t.OrderId == id);
    }

    [Theory]
    [InlineData("stock.received", "Margherita", 1)]
    [InlineData("order.placed", "Sushi", 1)]
    [InlineData("order.placed", "Margherita", 0)]
    [InlineData("order.placed", "Margherita", 25)]
    public async Task a_signal_that_is_not_a_real_order_is_refused(string kind, string pizza, int amount)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(kind, Guid.NewGuid().ToString("n"), pizza, amount, null, "copilot"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A channel is a stage label; "presenter" is the Engine Room's alone and cannot be claimed.</summary>
    [Fact]
    public async Task a_signal_cannot_claim_to_be_the_presenter()
    {
        var since = Get<FloorEventBus>().LastSeq;

        await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(FloorEventKinds.OrderPlaced, Guid.NewGuid().ToString("n"), "Margherita", 1, null, "presenter"));

        Assert.Equal(FloorChannel.Autonomous, Get<FloorEventBus>().Since(since).Single().Channel);
    }

    [Fact]
    public async Task a_customer_name_is_cut_to_something_a_banner_can_hold()
    {
        var id = Guid.NewGuid().ToString("n");

        await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(FloorEventKinds.OrderPlaced, id, "Margherita", 1, new string('A', 500) + "\u0007", "copilot"));

        var ticket = Get<OnlineOrderDesk>().Tickets.Single(t => t.OrderId == id);
        Assert.True(ticket.Customer.Length <= 40);
        Assert.DoesNotContain('\u0007', ticket.Customer);
    }
}

/// <summary>With a shared store a signal can announce an order, never create one.</summary>
public sealed class SharedStoreFloorSignalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task a_signal_for_an_order_nobody_placed_is_refused()
    {
        var shared = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton(new PizzaFactory.Web.Floor.FloorOptions { AdoptUnknownOrders = false })));
        var id = Guid.NewGuid().ToString("n");

        var response = await shared.CreateClient().PostAsJsonAsync(FloorSignal.Path,
            new FloorSignal(FloorEventKinds.OrderPlaced, id, "Margherita", 1, "Mallory", "copilot"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(await shared.Services.GetRequiredService<IOrderRepository>().ListAsync(), o => o.Id == id);
    }
}

/// <summary>Who may signal, and what a signal may never do.</summary>
public sealed class FloorSignalGuardTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string McpIdentity = "11111111-2222-3333-4444-555555555555";

    private WebApplicationFactory<Program> OnlyMcp() => factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        s.AddSingleton(new PizzaFactory.Web.Floor.FloorOptions { AdoptUnknownOrders = true, SignalCallerId = McpIdentity })));

    private static FloorSignal Signal(string? id = null) =>
        new(FloorEventKinds.OrderPlaced, id ?? Guid.NewGuid().ToString("n"), "Margherita", 1, "Mallory", "copilot");

    [Fact]
    public async Task a_signed_in_user_who_is_not_the_mcp_server_is_turned_away()
    {
        var response = await OnlyMcp().CreateClient().PostAsJsonAsync(FloorSignal.Path, Signal());

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task the_mcp_servers_identity_is_let_through()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, FloorSignal.Path) { Content = JsonContent.Create(Signal()) };
        request.Headers.Add("X-MS-CLIENT-PRINCIPAL-ID", McpIdentity);

        var response = await OnlyMcp().CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task the_customers_name_stays_off_the_stage()
    {
        var bus = factory.Services.GetRequiredService<FloorEventBus>();
        var since = bus.LastSeq;

        await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path, Signal());

        Assert.All(bus.Since(since), e => Assert.DoesNotContain("Mallory", e.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task a_delivered_order_cannot_be_replaced_or_replayed()
    {
        var orders = factory.Services.GetRequiredService<IOrderRepository>();
        var done = await orders.AddAsync(Order.Create("Funghi", 4, OrderChannel.Online).Start().MarkReady().MarkDelivered());
        var bus = factory.Services.GetRequiredService<FloorEventBus>();
        var since = bus.LastSeq;

        var response = await factory.CreateClient().PostAsJsonAsync(FloorSignal.Path, Signal(done.Id));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(bus.Since(since));
        var still = Assert.Single(await orders.ListAsync(), o => o.Id == done.Id);
        Assert.Equal("Funghi", still.ItemName);
        Assert.Equal(OrderState.Delivered, still.State);
    }
}

