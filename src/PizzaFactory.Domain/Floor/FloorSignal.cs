namespace PizzaFactory.Domain.Floor;

/// <summary>
/// What another process tells the stage. The MCP server sends one after Giuseppe places an
/// order on behalf of Copilot or Teams: the order itself already sits in the shared store,
/// this only says "it just happened, and through which channel" so the stage needn't poll.
/// </summary>
/// <param name="Kind">Only <see cref="FloorEventKinds.OrderPlaced"/> is accepted today.</param>
/// <param name="Channel">"copilot", "teams", "sharepoint" or "web"; anything else counts as the agents' own work.</param>
public sealed record FloorSignal(
    string Kind,
    string OrderId,
    string Pizza,
    int Amount,
    string? Customer = null,
    string? Channel = null)
{
    public const string Path = "/api/floor/signal";
}
