using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using PizzaFactory.Giuseppe.Tools;
using PizzaFactory.Giuseppe.WorkContext;
using PizzaFactory.Safety;

namespace PizzaFactory.Giuseppe.Tests;

/// <summary>
/// The golden-demo rehearsal harness: the exact "order pizza for Friday's retro" flow, end to end —
/// live Azure OpenAI (key-less), the find_meeting workplace tool, and a REAL factory MCP server over
/// Streamable HTTP. Env-gated so CI stays offline; run it before going on stage:
///   GIUSEPPE_ENDPOINT=… GIUSEPPE_DEPLOYMENT=… FACTORY_MCP_URL=http://localhost:5000/mcp dotnet test …
/// </summary>
public class FridayRetroGoldenDemoTests
{
    [Fact]
    public async Task giuseppe_caters_the_friday_retro_end_to_end()
    {
        var endpoint = Environment.GetEnvironmentVariable("GIUSEPPE_ENDPOINT");
        var deployment = Environment.GetEnvironmentVariable("GIUSEPPE_DEPLOYMENT");
        var factoryMcpUrl = Environment.GetEnvironmentVariable("FACTORY_MCP_URL");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) ||
            string.IsNullOrWhiteSpace(factoryMcpUrl))
        {
            return; // skipped — set GIUSEPPE_ENDPOINT, GIUSEPPE_DEPLOYMENT, FACTORY_MCP_URL to run
        }

        var chat = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            .GetChatClient(deployment)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        await using var factoryTools = new McpToolSource(new McpToolSourceOptions
        {
            Name = "factory",
            Endpoint = new Uri(factoryMcpUrl),
        });

        // WORKIQ_MODE=Live runs the real thing: the `workiq` CLI's stdio MCP server, the presenter's
        // actual Microsoft 365 calendar — with the rehearsal data as automatic on-stage fallback.
        var live = string.Equals(Environment.GetEnvironmentVariable("WORKIQ_MODE"), "Live", StringComparison.OrdinalIgnoreCase);
        var rehearsal = new WorkContextToolSource(new RehearsalWorkContext());
        await using var workIqTools = live
            ? new McpToolSource(
                new McpToolSourceOptions
                {
                    Name = "work-iq",
                    Command = Environment.GetEnvironmentVariable("WORKIQ_COMMAND") ?? "workiq",
                    Arguments = ["mcp"],
                },
                fallback: rehearsal)
            : null;

        var giuseppe = new GiuseppeAgent(
            chat,
            new HeuristicContentGuard(),
            workIqTools is null ? [factoryTools, rehearsal] : [factoryTools, workIqTools]);

        var reply = await giuseppe.AskAsync(
            "Ciao Giuseppe! We need pizza for the team retro on Friday — please sort it out.");

        Assert.True(reply.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(reply.Text));
        Assert.Contains("retro", reply.Text, StringComparison.OrdinalIgnoreCase);

        // Independently verify through the factory MCP server that orders actually landed.
        await using var verifyClient = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = "verify",
                Endpoint = new Uri(factoryMcpUrl),
            }));
        var orders = await verifyClient.CallToolAsync("list_orders", new Dictionary<string, object?>());
        var ordersJson = string.Join("", orders.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text));
        Assert.False(string.IsNullOrWhiteSpace(ordersJson));
        Assert.NotEqual("[]", ordersJson.Trim());
    }
}
