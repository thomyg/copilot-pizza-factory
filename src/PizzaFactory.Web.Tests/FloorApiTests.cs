using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.BackOffice;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Floor;

namespace PizzaFactory.Web.Tests;

public sealed class FloorApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task the_snapshot_shows_the_whole_house()
    {
        var client = factory.CreateClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/floor/snapshot"));
        var root = doc.RootElement;

        Assert.Equal(17, root.GetProperty("dining").GetProperty("tables").GetArrayLength());
        Assert.Equal(Enum.GetValues<Ingredient>().Length, root.GetProperty("stock").GetArrayLength());
        Assert.Equal(9, root.GetProperty("staff").GetArrayLength());
        Assert.True(root.GetProperty("kitchen").GetProperty("ovenCapacity").GetInt32() > 0);
        Assert.True(root.GetProperty("office").GetProperty("budget").GetProperty("budgetEur").GetDecimal() > 0);
        Assert.True(root.TryGetProperty("seq", out _));
    }

    [Fact]
    public async Task a_viewer_catches_up_on_events_since_its_snapshot()
    {
        var client = factory.CreateClient();
        var bus = factory.Services.GetRequiredService<FloorEventBus>();
        var since = bus.LastSeq;
        factory.Services.GetRequiredService<PurchaseBook>().Request(Ingredient.Tuna, 2500);

        var events = await client.GetFromJsonAsync<JsonElement[]>($"/api/floor/events?since={since}");

        var requested = Assert.Single(events!, e => e.GetProperty("kind").GetString() == FloorEventKinds.PurchaseOrderRequested);
        Assert.Equal("Office", requested.GetProperty("area").GetString());
        Assert.Equal("Autonomous", requested.GetProperty("channel").GetString());
    }

    /// <summary>The Copilot path end to end: a header on the approve call, a channel on the stage.</summary>
    [Fact]
    public async Task approving_from_copilot_is_shown_as_copilot()
    {
        var client = factory.CreateClient();
        var purchases = factory.Services.GetRequiredService<PurchaseBook>();
        var bus = factory.Services.GetRequiredService<FloorEventBus>();
        purchases.Request(Ingredient.Mushroom, 2800);
        var id = purchases.Orders(PurchaseOrderState.PendingApproval).First(o => o.Ingredient == Ingredient.Mushroom).Id;
        var since = bus.LastSeq;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/nonna/purchase-orders/{id}/approve");
        request.Headers.Add(FloorChannelScope.HeaderName, "copilot");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var e = Assert.Single(bus.Since(since), x => x.Kind == FloorEventKinds.PurchaseOrderApproved);
        Assert.Equal(FloorChannel.Copilot, e.Channel);
        Assert.Equal("Capo", e.Actor);
    }
}
