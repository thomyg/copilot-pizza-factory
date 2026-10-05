using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Mcp.Floor;

namespace PizzaFactory.Mcp.Tests;

/// <summary>
/// After Giuseppe orders through the MCP server, the stage hears about it — and hears which
/// surface asked: Copilot calls /mcp/copilot, the Teams bot sends a header, anything else is plain.
/// </summary>
public sealed class FloorSignalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class Captured : IFloorSignalSender
    {
        public ConcurrentQueue<FloorSignal> Signals { get; } = new();

        public Task SendAsync(FloorSignal signal, CancellationToken cancellationToken = default)
        {
            Signals.Enqueue(signal);
            return Task.CompletedTask;
        }
    }

    private (WebApplicationFactory<Program> Host, Captured Signals) Rig()
    {
        var captured = new Captured();
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IFloorSignalSender>(captured)));
        return (host, captured);
    }

    private static async Task<McpClient> ConnectAsync(WebApplicationFactory<Program> host, string path, string? channelHeader = null)
    {
        var http = host.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, path),
                TransportMode = HttpTransportMode.StreamableHttp,
                Name = "floor-signal-test",
                AdditionalHeaders = channelHeader is null ? null : new Dictionary<string, string> { [FloorChannelScope.HeaderName] = channelHeader },
            },
            http);
        return await McpClient.CreateAsync(transport);
    }

    private static Task<ModelContextProtocol.Protocol.CallToolResult> OrderAsync(McpClient client) =>
        client.CallToolAsync("create_order", new Dictionary<string, object?> { ["pizza"] = "Diavolo", ["amount"] = 3, ["customerName"] = "Retro team" }).AsTask();

    [Fact]
    public async Task an_order_from_the_copilot_endpoint_is_signalled_as_copilot()
    {
        var (host, captured) = Rig();
        await using var client = await ConnectAsync(host, "mcp/copilot");

        var result = await OrderAsync(client);

        Assert.NotEqual(true, result.IsError);
        var signal = Assert.Single(captured.Signals);
        Assert.Equal(FloorEventKinds.OrderPlaced, signal.Kind);
        Assert.Equal("copilot", signal.Channel);
        Assert.Equal("Diavolo", signal.Pizza);
        Assert.Equal(3, signal.Amount);
        Assert.Equal("Retro team", signal.Customer);
    }

    [Fact]
    public async Task the_teams_bot_names_its_channel_in_a_header()
    {
        var (host, captured) = Rig();
        await using var client = await ConnectAsync(host, "mcp", channelHeader: "teams");

        await OrderAsync(client);

        Assert.Equal("teams", Assert.Single(captured.Signals).Channel);
    }

    [Fact]
    public async Task a_plain_mcp_client_is_signalled_without_a_channel()
    {
        var (host, captured) = Rig();
        await using var client = await ConnectAsync(host, "mcp");

        await OrderAsync(client);

        Assert.Null(Assert.Single(captured.Signals).Channel);
    }

    [Fact]
    public async Task reading_tools_signal_nothing()
    {
        var (host, captured) = Rig();
        await using var client = await ConnectAsync(host, "mcp/copilot");

        await client.CallToolAsync("list_orders", new Dictionary<string, object?>());
        await client.CallToolAsync("get_stock", new Dictionary<string, object?>());

        Assert.Empty(captured.Signals);
    }

    /// <summary>The stage being down must never cost a customer their pizza.</summary>
    [Fact]
    public async Task an_order_still_succeeds_when_the_stage_cannot_be_reached()
    {
        var host = factory.WithWebHostBuilder(b => b.UseSetting("Floor:SignalUrl", "http://127.0.0.1:1/api/floor/signal"));
        await using var client = await ConnectAsync(host, "mcp/copilot");

        var result = await OrderAsync(client);

        Assert.NotEqual(true, result.IsError);
    }
}
