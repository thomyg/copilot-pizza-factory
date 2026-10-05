namespace PizzaFactory.Domain.Floor;

/// <summary>
/// The channel of the request currently being served, carried across awaits. The web host
/// enters a scope per request from the <see cref="HeaderName"/> header; everything that
/// publishes a floor event inside that request inherits it without threading a parameter
/// through every book and tool.
/// </summary>
public static class FloorChannelScope
{
    /// <summary>Set by our own surfaces (Copilot plugin, Teams bot, SharePoint web parts).</summary>
    public const string HeaderName = "X-PizzaFactory-Channel";

    private static readonly AsyncLocal<FloorChannel?> Ambient = new();

    public static FloorChannel? Current => Ambient.Value;

    public static IDisposable Enter(FloorChannel channel)
    {
        var previous = Ambient.Value;
        Ambient.Value = channel;
        return new Restore(previous);
    }

    /// <summary>
    /// Reads a channel from a header. The header is a label for the stage, not a credential,
    /// so only the outward-facing surfaces are accepted: a caller cannot claim to be the
    /// presenter or "the agents", and anything unrecognised is simply ignored.
    /// </summary>
    public static FloorChannel? Parse(string? header) =>
        header?.Trim().ToLowerInvariant() switch
        {
            "copilot" => FloorChannel.Copilot,
            "teams" => FloorChannel.Teams,
            "sharepoint" => FloorChannel.SharePoint,
            "web" => FloorChannel.Web,
            _ => null,
        };

    private sealed class Restore(FloorChannel? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
