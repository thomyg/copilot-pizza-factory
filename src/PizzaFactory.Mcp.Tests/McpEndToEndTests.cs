using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;

namespace PizzaFactory.Mcp.Tests;

/// <summary>
/// End-to-end over the real MCP protocol: a genuine MCP client (HttpClientTransport) talks to the
/// server hosted in-memory by WebApplicationFactory. Replaces the manual curl smoke and exercises
/// the same client path Giuseppe uses.
/// </summary>
public class McpEndToEndTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<McpClient> ConnectAsync()
    {
        var http = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                Name = "e2e-test",
            },
            http);
        return await McpClient.CreateAsync(transport);
    }

    [Fact]
    public async Task lists_the_expected_tools()
    {
        await using var client = await ConnectAsync();

        var names = (await client.ListToolsAsync()).Select(t => t.Name).ToHashSet();

        Assert.Contains("create_order", names);
        Assert.Contains("get_stock", names);
        Assert.Contains("station_status", names);
    }

    [Fact]
    public async Task create_order_and_list_round_trip_over_the_protocol()
    {
        await using var client = await ConnectAsync();

        var created = await client.CallToolAsync(
            "create_order", new Dictionary<string, object?> { ["pizza"] = "Hawaii", ["amount"] = 2 });
        Assert.NotEqual(true, created.IsError);

        var listed = await client.CallToolAsync("list_orders", new Dictionary<string, object?>());
        Assert.NotEqual(true, listed.IsError);
    }
}
