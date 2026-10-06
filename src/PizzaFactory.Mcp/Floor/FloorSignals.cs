using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using PizzaFactory.Domain.Floor;

namespace PizzaFactory.Mcp.Floor;

/// <summary>Where the stage listens, and how to prove who is calling.</summary>
public sealed class FloorSignalOptions
{
    public const string SectionName = "Floor";

    /// <summary>The web app's <see cref="FloorSignal.Path"/>, absolute. Unset: no stage to tell.</summary>
    public Uri? SignalUrl { get; set; }

    /// <summary>Entra scope of the web app (e.g. "api://…/.default"). Set in Azure, where Easy Auth guards the app.</summary>
    public string? SignalScope { get; set; }
}

/// <summary>Tells the stage that something just happened here.</summary>
public interface IFloorSignalSender
{
    Task SendAsync(FloorSignal signal, CancellationToken cancellationToken = default);
}

/// <summary>No stage configured: nothing to tell.</summary>
public sealed class NullFloorSignalSender : IFloorSignalSender
{
    public Task SendAsync(FloorSignal signal, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Best effort, short fuse. The order is already in the shared store when this runs; the
/// signal only saves the stage a poll. A stage that is down, slow or misconfigured must never
/// cost a customer their pizza, so every failure is logged and swallowed.
/// </summary>
public sealed class HttpFloorSignalSender(
    HttpClient http,
    FloorSignalOptions options,
    ILogger<HttpFloorSignalSender> logger,
    TokenCredential? credential = null) : IFloorSignalSender
{
    public async Task SendAsync(FloorSignal signal, CancellationToken cancellationToken = default)
    {
        if (options.SignalUrl is null)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.SignalUrl) { Content = JsonContent.Create(signal) };
            if (!string.IsNullOrWhiteSpace(options.SignalScope))
            {
                var token = await (credential ?? throw new InvalidOperationException("Floor:SignalScope is set but no credential is registered."))
                    .GetTokenAsync(new TokenRequestContext([options.SignalScope]), cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            }

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Stage refused floor signal for {OrderId}: {Status}", signal.OrderId, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AuthenticationFailedException or CredentialUnavailableException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Stage unreachable; order {OrderId} stands, the stage will catch up on its next read", signal.OrderId);
        }
    }
}

/// <summary>
/// Which surface is calling. Our own clients say so: the Teams bot sends a header, the Copilot
/// plugin points at /mcp/copilot (it cannot add headers). Anything else is a plain MCP client.
/// </summary>
public sealed class CallerChannel(IHttpContextAccessor accessor)
{
    public string? Current
    {
        get
        {
            var context = accessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            var named = FloorChannelScope.Parse(context.Request.Headers[FloorChannelScope.HeaderName])
                ?? FloorChannelScope.Parse(context.Request.Path.Value?.TrimEnd('/').Split('/').LastOrDefault())
                ?? (CalledThroughCopilot(context.Request.Headers[EasyAuthPrincipalHeader]) ? FloorChannel.Copilot : null);
            return named?.ToString().ToLowerInvariant();
        }
    }

    /// <summary>Set by App Service authentication from the validated token; client copies are stripped.</summary>
    public const string EasyAuthPrincipalHeader = "X-MS-CLIENT-PRINCIPAL";

    /// <summary>
    /// Copilot plugins get their token from the Microsoft Enterprise token store. Copilot cannot
    /// add a header, and its SSO registration pins the URL to /mcp, so the token's client is the
    /// one honest signal that Copilot is calling.
    /// </summary>
    public const string CopilotTokenStoreAppId = "ab3be6b7-f5df-413d-ac2d-abf1e3fd9c0b";

    private static bool CalledThroughCopilot(string? principal)
    {
        if (string.IsNullOrWhiteSpace(principal))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(Convert.FromBase64String(principal));
            return doc.RootElement.TryGetProperty("claims", out var claims)
                && claims.EnumerateArray().Any(c =>
                    c.TryGetProperty("typ", out var typ) && typ.GetString() is "azp" or "appid"
                    && c.TryGetProperty("val", out var val)
                    && string.Equals(val.GetString(), CopilotTokenStoreAppId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}

public static class FloorSignalWiring
{
    /// <summary>Surfaces that get an MCP endpoint of their own, so the stage can say who asked.</summary>
    public static readonly string[] SurfaceRoutes = ["/mcp/copilot", "/mcp/teams"];

    public static IServiceCollection AddFloorSignals(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(FloorSignalOptions.SectionName).Get<FloorSignalOptions>() ?? new FloorSignalOptions();
        services.AddSingleton(options);
        if (!string.IsNullOrWhiteSpace(options.SignalScope))
        {
            // One credential for the process: it caches tokens, so an order costs no extra round trip.
            services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
        }

        services.AddHttpContextAccessor();
        services.AddSingleton<CallerChannel>();
        services.AddHttpClient<IFloorSignalSender, HttpFloorSignalSender>(http => http.Timeout = TimeSpan.FromSeconds(2));
        return services;
    }
}
