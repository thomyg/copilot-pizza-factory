using Microsoft.Extensions.AI;

namespace PizzaFactory.Giuseppe.Tools;

/// <summary>
/// A provider of AI tools for Giuseppe's function-calling loop. Sources are additive —
/// the factory MCP server contributes ordering/stock tools, the work-context source
/// contributes meeting lookup — and each source degrades to an empty list on failure
/// so a broken integration never takes Giuseppe down.
/// </summary>
public interface IGiuseppeToolSource
{
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default);
}
