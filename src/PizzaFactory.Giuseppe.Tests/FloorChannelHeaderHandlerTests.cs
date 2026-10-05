using PizzaFactory.Domain.Floor;
using PizzaFactory.Giuseppe.Tools;

namespace PizzaFactory.Giuseppe.Tests;

/// <summary>
/// Giuseppe's calls to the factory MCP server carry the channel the conversation came from, so
/// an order typed into the SharePoint chat shows up on the stage as "via SharePoint".
/// </summary>
public sealed class FloorChannelHeaderHandlerTests
{
    private sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private static async Task<string?> SentChannelAsync(string? fallback)
    {
        var capture = new Capture();
        using var client = new HttpClient(new FloorChannelHeaderHandler(fallback) { InnerHandler = capture });
        await client.GetAsync(new Uri("http://factory.test/mcp"));
        return capture.Last!.Headers.TryGetValues(FloorChannelScope.HeaderName, out var values) ? values.Single() : null;
    }

    [Fact]
    public async Task the_conversations_channel_rides_along()
    {
        using (FloorChannelScope.Enter(FloorChannel.SharePoint))
        {
            Assert.Equal("sharepoint", await SentChannelAsync(fallback: null));
        }
    }

    [Fact]
    public async Task a_host_with_one_surface_names_it_by_default()
    {
        Assert.Equal("teams", await SentChannelAsync(fallback: "teams"));
    }

    [Fact]
    public async Task the_live_channel_beats_the_default()
    {
        using (FloorChannelScope.Enter(FloorChannel.Copilot))
        {
            Assert.Equal("copilot", await SentChannelAsync(fallback: "teams"));
        }
    }

    [Fact]
    public async Task without_a_channel_nothing_is_claimed()
    {
        Assert.Null(await SentChannelAsync(fallback: null));
    }

    /// <summary>The presenter's levers stay the presenter's; they are never passed on as a surface.</summary>
    [Fact]
    public async Task the_presenter_is_not_passed_on()
    {
        using (FloorChannelScope.Enter(FloorChannel.Presenter))
        {
            Assert.Null(await SentChannelAsync(fallback: null));
        }
    }
}
