using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;
using System.Threading.RateLimiting;
using PizzaFactory.Trattoria;
using PizzaFactory.Domain.Floor;

namespace PizzaFactory.Web.Floor;

/// <summary>Hooks the floor event bus into the host: one bus, and the caller's channel on every request.</summary>
public static class FloorWiring
{
    /// <summary>A signal per order, from one caller; a minute's worth is plenty.</summary>
    public const string SignalRateLimitPolicy = "floor-signal";

    public static IServiceCollection AddFloorEvents(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp => new FloorEventBus(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IFloorEvents>(sp => sp.GetRequiredService<FloorEventBus>());
        services.AddSingleton<FloorEscalationSink>();
        services.AddSingleton<FloorSnapshotBuilder>();
        services.AddSingleton(new FloorOptions { SignalCallerId = configuration["Floor:SignalCallerId"] });
        services.AddSingleton<FloorSignalLedger>();
        services.AddRateLimiter(options => options.AddPolicy(SignalRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) })));
        return services;
    }

    /// <summary>
    /// Wraps whichever order store is registered (Cosmos or in-memory) so every new order is
    /// reported. Call after the store is registered.
    /// </summary>
    public static IServiceCollection ReportOrdersToTheFloor(this IServiceCollection services)
    {
        var store = services.LastOrDefault(d => d.ServiceType == typeof(IOrderRepository))
            ?? throw new InvalidOperationException("Register the order store before reporting it to the floor.");
        services.Remove(store);
        services.AddSingleton<IOrderRepository>(sp => new FloorReportingOrderRepository(
            (IOrderRepository)(store.ImplementationInstance
                ?? store.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, store.ImplementationType!)),
            sp.GetRequiredService<IFloorEvents>()));
        return services;
    }

    /// <summary>The stage's reads: the whole house, and the events since a number.</summary>
    public static void MapFloorApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/floor").RequireRateLimiting(GiuseppeChatApi.ReadRateLimitPolicy);

        group.MapGet("/snapshot", async (FloorSnapshotBuilder builder, CancellationToken cancellationToken) =>
            Results.Ok(await builder.CurrentAsync(cancellationToken)));

        group.MapGet("/events", (long? since, FloorEventBus bus) =>
            Results.Ok(bus.Since(Math.Max(0, since ?? 0))));

        // The MCP server's "Giuseppe just ordered, via Copilot". In Azure, Easy Auth in front of
        // the app means only an authenticated caller (the MCP function's managed identity) gets here.
        group.MapPost("/signal", OrderSignalledAsync).RequireRateLimiting(SignalRateLimitPolicy);
    }

    private const int MaxCustomerLength = 40;

    /// <summary>
    /// Use for a house whose MCP server keeps its own store (local in-memory runs). With a
    /// shared store (Cosmos) the web app never creates an order from a signal.
    /// </summary>
    public static IServiceCollection AdoptSignalledOrders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new FloorOptions { AdoptUnknownOrders = true, SignalCallerId = configuration["Floor:SignalCallerId"] });
        return services;
    }

    private static async Task<IResult> OrderSignalledAsync(
        FloorSignal signal,
        HttpContext http,
        IOrderRepository orders,
        IFloorEvents floor,
        OnlineOrderDesk desk,
        FloorOptions options,
        FloorSignalLedger heard,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // Easy Auth proves "signed in to this tenant"; only the MCP server's identity may signal.
        // App Service strips client-sent X-MS-CLIENT-PRINCIPAL-* headers, so this one is the platform's.
        if (options.SignalCallerId is { Length: > 0 } caller
            && !string.Equals(http.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"], caller, StringComparison.OrdinalIgnoreCase))
        {
            // With a body, so the status-code pages do not re-execute it into the Blazor 404 page.
            return Results.Json(new { error = "Only the factory's MCP server may signal the floor." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (signal.Kind != FloorEventKinds.OrderPlaced
            || string.IsNullOrWhiteSpace(signal.OrderId) || signal.OrderId.Length > 64
            || signal.Amount is < 1 or > 24
            || RecipeCatalog.FindPizza(signal.Pizza) is not { } recipe)
        {
            return Results.BadRequest(new { error = "Only a real order.placed for a menu pizza, 1 to 24, is accepted." });
        }

        var channel = FloorChannelScope.Parse(signal.Channel) ?? FloorChannelScope.Current;
        if (!heard.FirstTime(signal.OrderId, channel))
        {
            return Results.Accepted();   // heard it already
        }

        using var scope = channel is { } c ? FloorChannelScope.Enter(c) : null;

        // The name typed into Copilot is often the user's own: it goes on the ticket, never on the stage.
        var customer = Clean(signal.Customer) ?? "a colleague";

        var order = await FindAsync(orders, signal.OrderId, cancellationToken);
        if (order is { State: OrderState.Delivered })
        {
            return Results.Accepted();   // long gone; nothing to show
        }

        if (order is null && !options.AdoptUnknownOrders)
        {
            // A shared store: the MCP server wrote the order before signalling, so an id we
            // cannot find is not an order. A signal is a notice, never a way to place one.
            return Results.NotFound(new { error = "No such order." });
        }

        if (order is null)
        {
            // A store of its own (local development): adopt the order so the floor bakes it.
            // The order repository reports it to the floor on the way in.
            order = await orders.AddAsync(
                Order.Create(recipe.Name, signal.Amount, channel == FloorChannel.Copilot ? OrderChannel.Copilot : OrderChannel.Bot, customer, clock)
                    with { Id = signal.OrderId },
                cancellationToken);
        }
        else
        {
            floor.Publish(new FloorEventDraft("Giuseppe", FloorReportingOrderRepository.AreaOf(order.Channel), FloorEventKinds.OrderPlaced,
                order.Id, FloorReportingOrderRepository.Describe(order)));
        }

        desk.Track(order, FulfilmentMode.Delivery, customer, clock.GetUtcNow());
        return Results.Accepted();
    }

    private static async Task<Order?> FindAsync(IOrderRepository orders, string id, CancellationToken cancellationToken)
    {
        foreach (var state in (OrderState[])[OrderState.Created, OrderState.Started, OrderState.Ready, OrderState.Delivered])
        {
            if ((await orders.GetByStateAsync(state, cancellationToken)).FirstOrDefault(o => o.Id == id) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>A name typed into Copilot, made safe for a banner: printable, trimmed, short.</summary>
    private static string? Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var printable = new string([.. name.Where(ch => !char.IsControl(ch))]).Trim();
        return printable.Length == 0 ? null : printable.Length <= MaxCustomerLength ? printable : printable[..MaxCustomerLength].TrimEnd();
    }

    /// <summary>
    /// Enters the channel our surfaces name in <see cref="FloorChannelScope.HeaderName"/> for the
    /// rest of the request, so anything published while serving it — a PO Nonna approves, an order
    /// Giuseppe places — is shown on the stage as "via Copilot" rather than as the agents' own work.
    /// </summary>
    public static IApplicationBuilder UseFloorChannel(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var channel = FloorChannelScope.Parse(context.Request.Headers[FloorChannelScope.HeaderName]);
            if (channel is null)
            {
                await next(context);
                return;
            }

            using (FloorChannelScope.Enter(channel.Value))
            {
                await next(context);
            }
        });
}

/// <summary>How the floor treats signals from other processes.</summary>
public sealed record FloorOptions
{
    /// <summary>Create an order a signal names but the store lacks. Only for local development.</summary>
    public bool AdoptUnknownOrders { get; init; }

    /// <summary>Floor:SignalCallerId — the object id of the MCP server's managed identity. When set, only it may signal.</summary>
    public string? SignalCallerId { get; init; }
}

/// <summary>
/// Remembers which orders were already announced — so a repeated signal cannot replay a banner —
/// and through which surface, so the stage can mark that order all the way to the courier.
/// </summary>
public sealed class FloorSignalLedger
{
    private const int Capacity = 2000;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, FloorChannel?> _seen = [];
    private readonly Queue<string> _order = new();

    public bool FirstTime(string orderId, FloorChannel? channel = null)
    {
        lock (_gate)
        {
            if (!_seen.TryAdd(orderId, channel))
            {
                return false;
            }

            _order.Enqueue(orderId);
            while (_order.Count > Capacity)
            {
                _seen.Remove(_order.Dequeue());
            }

            return true;
        }
    }

    /// <summary>The Microsoft 365 surface (or the web) an order came through; null for the house's own.</summary>
    public string? SurfaceOf(string orderId)
    {
        lock (_gate)
        {
            return _seen.GetValueOrDefault(orderId) is { } c && c is not FloorChannel.Autonomous and not FloorChannel.Presenter
                ? c.ToString()
                : null;
        }
    }
}
