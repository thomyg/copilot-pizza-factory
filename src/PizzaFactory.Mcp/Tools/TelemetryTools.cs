using System.ComponentModel;
using ModelContextProtocol.Server;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Mcp.Tools;

/// <summary>
/// MCP tools for live floor telemetry — the data behind "How's the line?" in Copilot and the
/// Window dashboard.
/// </summary>
[McpServerToolType]
public sealed class TelemetryTools(IOrderRepository orders, IPizzaRepository pizzas, IRestingDoughRepository doughs)
{
    [McpServerTool(Name = "station_status")]
    [Description("Live snapshot of the factory floor: pizzas per station, open orders, and ready dough.")]
    public async Task<StationStatus> StationStatusAsync(CancellationToken cancellationToken = default)
    {
        async Task<int> Pizzas(PizzaState state) =>
            (await pizzas.GetByStateAsync(state, int.MaxValue, cancellationToken)).Count;

        var ordered = await Pizzas(PizzaState.OrderAccepted);
        var preparing = await Pizzas(PizzaState.Preparing);
        var baking = await Pizzas(PizzaState.Baking);
        var ready = await Pizzas(PizzaState.Ready);

        var created = (await orders.GetByStateAsync(OrderState.Created, cancellationToken)).Count;
        var started = (await orders.GetByStateAsync(OrderState.Started, cancellationToken)).Count;
        var doughReady = (await doughs.GetByStateAsync(DoughState.Ready, cancellationToken)).Count;

        return new StationStatus(ordered, preparing, baking, ready, created + started, doughReady);
    }
}
