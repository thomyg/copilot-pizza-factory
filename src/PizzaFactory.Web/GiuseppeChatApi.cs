using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PizzaFactory.Giuseppe;

namespace PizzaFactory.Web;

public sealed record GiuseppeChatRequest(string Message);

public sealed record GiuseppeChatResponse(bool Allowed, string Reply);

/// <summary>
/// The pro-code route: the SAME GiuseppeAgent that answers in the Window's chat drawer,
/// exposed as a small JSON API so the SPFx chat web part on SharePoint can talk to him.
/// Staff surface — he wears the front-desk + manager belt here, same as in the house.
/// Guarded (IContentGuard runs inside AskAsync), rate-limited, and CORS-restricted to
/// the origins named in SharePointChat:AllowedOrigins.
/// </summary>
public static class GiuseppeChatApi
{
    public const string CorsPolicy = "spfx-chat";
    public const string RateLimitPolicy = "giuseppe-chat";

    /// <summary>
    /// Read-only board traffic. The chat limit exists to protect a model that costs money per
    /// message; a snapshot costs a database read. Four polling web parts on one page already
    /// make about a dozen calls a minute per viewer, so sharing the chat's budget meant two
    /// people behind one NAT could 429 each other mid-demo.
    /// </summary>
    public const string ReadRateLimitPolicy = "trattoria-read";
    private const int MaxMessageLength = 2000;

    public static void AddGiuseppeChatApi(this WebApplicationBuilder builder)
    {
        var origins = Origins(builder.Configuration);
        if (origins.Length > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST")));
        }

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(ReadRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    public static void MapGiuseppeChatApi(this WebApplication app)
    {
        if (Origins(app.Configuration).Length > 0)
        {
            app.UseCors();
        }

        app.UseRateLimiter();

        var giuseppe = app.Services.GetService<GiuseppeAgent>();

        var endpoint = app.MapPost("/api/giuseppe/chat", async (GiuseppeChatRequest request, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
            {
                return Results.BadRequest(new GiuseppeChatResponse(false, "A message, per favore — between one character and a reasonable letter."));
            }

            if (giuseppe is null)
            {
                return Results.Ok(new GiuseppeChatResponse(
                    false, "Giuseppe is off the clock — no model configured. The wood oven still works, though. 🍕"));
            }

            var reply = await giuseppe.AskAsync(request.Message, cancellationToken);
            return Results.Ok(new GiuseppeChatResponse(reply.Allowed, reply.Text));
        }).RequireRateLimiting(RateLimitPolicy);

        if (Origins(app.Configuration).Length > 0)
        {
            endpoint.RequireCors(CorsPolicy);
        }
    }

    private static string[] Origins(IConfiguration configuration) =>
        (configuration["SharePointChat:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
