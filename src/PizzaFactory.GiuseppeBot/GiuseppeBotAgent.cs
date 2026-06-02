using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using PizzaFactory.Giuseppe;

namespace PizzaFactory.GiuseppeBot;

/// <summary>
/// Surfaces <see cref="GiuseppeAgent"/> over the Bot Framework <c>/api/messages</c> protocol so Teams
/// (and any Azure Bot Service channel) can chat with Giuseppe. Routing only lives here — the actual
/// pizzaiolo brain (persona + content guard) stays in <see cref="GiuseppeAgent"/>.
///
/// <para><paramref name="giuseppe"/> is optional: when Azure OpenAI is not configured the bot still
/// builds and answers with a friendly "off the clock" line, so the endpoint never 500s in local dev.</para>
/// </summary>
public sealed class GiuseppeBotAgent : AgentApplication
{
    private const string OffTheClock =
        "Ciao! Giuseppe is still warming up the oven — the kitchen's AI isn't wired up here yet. " +
        "Come back when the ovens are hot! 🍕";

    private readonly GiuseppeAgent? _giuseppe;

    public GiuseppeBotAgent(AgentApplicationOptions options, GiuseppeAgent? giuseppe = null)
        : base(options)
    {
        _giuseppe = giuseppe;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private static async Task WelcomeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var member in turnContext.Activity.MembersAdded ?? [])
        {
            // Don't greet ourselves when the bot is the member being added to the conversation.
            if (member.Id == turnContext.Activity.Recipient?.Id)
            {
                continue;
            }

            await turnContext.SendActivityAsync(
                "Benvenuto! I'm Giuseppe, your pizzaiolo. Tell me what you're craving and I'll sort you out. 🍕",
                cancellationToken: cancellationToken);
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var text = turnContext.Activity.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_giuseppe is null)
        {
            await turnContext.SendActivityAsync(OffTheClock, cancellationToken: cancellationToken);
            return;
        }

        var reply = await _giuseppe.AskAsync(text, cancellationToken);
        await turnContext.SendActivityAsync(reply.Text, cancellationToken: cancellationToken);
    }
}
