using PizzaFactory.Domain.Floor;

namespace PizzaFactory.Giuseppe.Tools;

/// <summary>
/// Stamps each call to the factory MCP server with the channel of the conversation that caused
/// it — read at send time, because the MCP client is shared across conversations. A host that
/// only ever serves one surface (the Teams bot) names it as the fallback.
/// </summary>
public sealed class FloorChannelHeaderHandler(string? fallback) : DelegatingHandler
{
    private static readonly FloorChannel[] Surfaces = [FloorChannel.Copilot, FloorChannel.Teams, FloorChannel.SharePoint, FloorChannel.Web];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var channel = FloorChannelScope.Current is { } live && Surfaces.Contains(live)
            ? live.ToString().ToLowerInvariant()
            : fallback;
        if (channel is not null && !request.Headers.Contains(FloorChannelScope.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(FloorChannelScope.HeaderName, channel);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
