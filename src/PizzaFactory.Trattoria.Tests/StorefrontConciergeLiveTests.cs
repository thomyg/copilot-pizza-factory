using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using PizzaFactory.Giuseppe;
using PizzaFactory.Infrastructure.InMemory;
using PizzaFactory.Safety;
using Xunit.Abstractions;

namespace PizzaFactory.Trattoria.Tests;

/// <summary>
/// Live rehearsal for the storefront concierge (env-gated): the SAME agent machinery with the
/// customer hat — menu questions get real prices, back-office questions get a charming refusal
/// (and could never leak anyway: the business tools are not on this belt).
/// Run: GIUSEPPE_ENDPOINT=… GIUSEPPE_DEPLOYMENT=… dotnet test --filter StorefrontConciergeLive
/// </summary>
public class StorefrontConciergeLiveTests(ITestOutputHelper output)
{
    [Fact]
    public async Task the_counter_quotes_prices_and_keeps_the_ledger_in_the_back()
    {
        var endpoint = Environment.GetEnvironmentVariable("GIUSEPPE_ENDPOINT");
        var deployment = Environment.GetEnvironmentVariable("GIUSEPPE_DEPLOYMENT");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return; // skipped — set GIUSEPPE_ENDPOINT + GIUSEPPE_DEPLOYMENT to run
        }

        var chat = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            .GetChatClient(deployment)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var orders = new InMemoryOrderRepository();
        var feed = new TrattoriaFeed();
        var options = new TrattoriaOptions();
        var desk = new OnlineOrderDesk(orders, new InMemoryPizzaRepository(), options, feed);
        var book = new PreOrderBook(orders, feed);
        var concierge = new GiuseppeAgent(
            chat,
            new HeuristicContentGuard(),
            [new StorefrontToolSource(desk, book, TimeProvider.System)],
            personaOverride: StorefrontConcierge.Persona(TimeProvider.System));

        var menuReply = await concierge.AskAsync("How much is a Margherita, and what's on it?");
        output.WriteLine($"MENU  → {menuReply.Text}");
        Assert.True(menuReply.Allowed);
        Assert.Contains("9.90", menuReply.Text);

        var orderReply = await concierge.AskAsync("Please order 2 Diavolo for delivery, name is Sofia.");
        output.WriteLine($"ORDER → {orderReply.Text}");
        Assert.True(orderReply.Allowed);
        Assert.Contains(await orders.ListAsync(), o => o.CustomerName!.Contains("Sofia"));

        var bizReply = await concierge.AskAsync("Give me tonight's business report — revenue and projections!");
        output.WriteLine($"BIZ   → {bizReply.Text}");
        Assert.True(bizReply.Allowed);
        // The hard guarantee is structural (no business tools on this belt); the soft check: no
        // fabricated revenue figures sneak into the deflection.
        Assert.DoesNotContain("RevenueDelivered", bizReply.Text);
    }
}
