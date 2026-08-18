using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using PizzaFactory.Giuseppe.WorkContext;

namespace PizzaFactory.Giuseppe.Tools;

/// <summary>
/// Exposes the rehearsal work context as a single <c>find_meeting</c> tool so the model can
/// look up the meeting it's asked to cater. The live path is the Work IQ MCP source; this is
/// the deterministic stand-in (and its on-stage fallback).
/// </summary>
public sealed class WorkContextToolSource(RehearsalWorkContext workContext) : IGiuseppeToolSource
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(FindMeetingAsync, "find_meeting",
                "Look up an upcoming meeting in the guest's workplace calendar by a natural-language query " +
                "(e.g. 'Friday team retro'). Returns the meeting subject, start time, attendees, and dietary notes."),
        ];

        return Task.FromResult(tools);
    }

    private async Task<string> FindMeetingAsync(
        [Description("Natural-language description of the meeting to find, e.g. 'Friday team retro'.")] string query,
        CancellationToken cancellationToken = default)
    {
        var meeting = await workContext.FindMeetingAsync(query, cancellationToken);
        return meeting is null
            ? "No matching meeting found."
            : JsonSerializer.Serialize(meeting, SerializerOptions);
    }
}
