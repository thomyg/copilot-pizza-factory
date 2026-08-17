using Microsoft.Extensions.AI;
using PizzaFactory.Giuseppe.Tools;
using PizzaFactory.Giuseppe.WorkContext;

namespace PizzaFactory.Giuseppe.Tests;

public class McpToolSourceTests
{
    // Nothing listens on port 9 (discard) — connection fails fast, exercising the degrade path.
    private static readonly Uri DeadEndpoint = new("http://127.0.0.1:9/mcp");

    [Fact]
    public async Task returns_no_tools_when_the_server_is_unreachable()
    {
        await using var source = new McpToolSource(new McpToolSourceOptions
        {
            Name = "unreachable",
            Endpoint = DeadEndpoint,
        });

        var tools = await source.GetToolsAsync();

        Assert.Empty(tools);
    }

    [Fact]
    public async Task falls_back_to_the_rehearsal_source_when_the_server_is_unreachable()
    {
        await using var source = new McpToolSource(
            new McpToolSourceOptions { Name = "work-iq", Endpoint = DeadEndpoint },
            fallback: new WorkContextToolSource(new RehearsalWorkContext()));

        var tools = await source.GetToolsAsync();

        var tool = Assert.Single(tools);
        Assert.Equal("find_meeting", tool.Name);
    }

    [Fact]
    public async Task throws_on_misconfiguration_without_endpoint_or_command_only_when_used()
    {
        await using var source = new McpToolSource(new McpToolSourceOptions { Name = "empty" });

        // Misconfiguration still degrades to "no tools" — Giuseppe keeps talking either way.
        var tools = await source.GetToolsAsync();

        Assert.Empty(tools);
    }
}
