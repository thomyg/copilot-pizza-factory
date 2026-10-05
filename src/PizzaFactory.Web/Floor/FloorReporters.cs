using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Factory;

namespace PizzaFactory.Web.Floor;

/// <summary>
/// Reports every new order to the floor, whoever placed it: a party at table 9, the guest form,
/// a pre-order coming due, the presenter's rush. Wrapping the repository means no ordering path
/// can forget to report — and none has to know there is a stage.
///
/// The channel deliberately comes from the request, not from <see cref="Order.Channel"/>: the
/// simulation books its fake online orders as <see cref="OrderChannel.Copilot"/>, and the stage
/// must only say "via Copilot" when Copilot actually asked.
/// </summary>
public sealed class FloorReportingOrderRepository(IOrderRepository inner, IFloorEvents floor) : IOrderRepository
{
    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken = default) =>
        inner.ListAsync(cancellationToken);

    public Task<IReadOnlyList<Order>> GetByStateAsync(OrderState state, CancellationToken cancellationToken = default) =>
        inner.GetByStateAsync(state, cancellationToken);

    public Task<Order> UpdateAsync(Order order, CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(order, cancellationToken);

    public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        var added = await inner.AddAsync(order, cancellationToken);
        floor.Publish(new FloorEventDraft(
            "Giuseppe",
            AreaOf(added.Channel),
            FloorEventKinds.OrderPlaced,
            added.Id,
            Describe(added)));
        return added;
    }

    /// <summary>
    /// What the stage says about an order. No customer name: on a projector, a name typed into
    /// Copilot or the storefront is usually a real person's.
    /// </summary>
    public static string Describe(Order order) =>
        $"{order.Amount}× {order.ItemName}{(AreaOf(order.Channel) == FloorArea.Street ? ", to go" : "")}";

    /// <summary>Where the pizza is going to be eaten: at a table, or out the door with a courier.</summary>
    public static FloorArea AreaOf(OrderChannel channel) => channel switch
    {
        OrderChannel.Restaurant or OrderChannel.Guest or OrderChannel.Planned => FloorArea.Dining,
        _ => FloorArea.Street,
    };
}

/// <summary>A silo in crisis, as the stage sees it: a red pin in the storeroom.</summary>
public sealed class FloorEscalationSink(IFloorEvents floor) : IEscalationSink
{
    public Task RaiseAsync(Escalation escalation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        floor.Publish(new FloorEventDraft(
            "Procurement",
            FloorArea.Storeroom,
            FloorEventKinds.StockLow,
            escalation.Ingredient.ToString(),
            $"{escalation.Ingredient} critically low: {escalation.Grams} g left"));
        return Task.CompletedTask;
    }
}
