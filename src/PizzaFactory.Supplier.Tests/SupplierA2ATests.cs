using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PizzaFactory.Domain;
using PizzaFactory.Factory;

namespace PizzaFactory.Supplier.Tests;

/// <summary>
/// The external Supplier agent over HTTP: it publishes an A2A agent card for discovery and answers
/// the replenish task. Drives it via the in-memory host, including the real HttpSupplierGateway.
/// </summary>
public class SupplierA2ATests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task publishes_an_a2a_agent_card()
    {
        var http = factory.CreateClient();

        var response = await http.GetAsync("/.well-known/agent-card.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("replenish", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Supplier", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task replenish_confirms_with_an_eta()
    {
        var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/replenish", new { ingredient = "Pineapple", grams = 250 });

        response.EnsureSuccessStatusCode();
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(quote.GetProperty("confirmed").GetBoolean());
        Assert.True(quote.GetProperty("etaSeconds").GetInt32() > 0);
    }

    [Fact]
    public async Task gateway_requests_restock_over_the_wire()
    {
        var gateway = new HttpSupplierGateway(factory.CreateClient());

        var quote = await gateway.RequestRestockAsync(Ingredient.Pineapple, 500);

        Assert.True(quote.Confirmed);
        Assert.Equal(Ingredient.Pineapple, quote.Ingredient);
        Assert.Equal(500, quote.Grams);
        Assert.True(quote.EtaSeconds > 0);
    }
}
