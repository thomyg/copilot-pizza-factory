using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Trattoria;

public enum FulfilmentMode
{
    Takeaway,
    Delivery,
}

public enum TicketState
{
    Cooking,
    Done,
}

/// <summary>An online order as the counter sees it: who, over which channel, and how it leaves.</summary>
public sealed record OnlineTicket(
    string OrderId,
    OrderChannel Channel,
    FulfilmentMode Mode,
    string Customer,
    string Pizza,
    int Amount,
    DateTimeOffset PlacedAt,
    TicketState State,
    DateTimeOffset? ReadyAt = null);

/// <summary>
/// The counter by the door: takes online orders (web, chat, Copilot, phone) for takeaway or
/// delivery, places them as REAL orders on the factory, and hands them over — courier or
/// customer — shortly after the pass calls them ready.
/// </summary>
public sealed class OnlineOrderDesk(
    IOrderRepository orders,
    IPizzaRepository pizzas,
    TrattoriaOptions options,
    TrattoriaFeed feed)
{
    private static readonly OrderChannel[] Channels =
        [OrderChannel.Online, OrderChannel.Bot, OrderChannel.Copilot, OrderChannel.Phone];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Random _random = options.RandomSeed is { } seed ? new Random(seed + 1) : Random.Shared;
    private readonly List<OnlineTicket> _tickets = [];
    private bool _isOpen;

    public void Open() => _isOpen = true;

    public void Close() => _isOpen = false;

    public IReadOnlyList<OnlineTicket> Tickets
    {
        get { lock (_tickets) { return [.. _tickets]; } }
    }

    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_isOpen && _random.NextDouble() < options.OnlineOrderChancePerTick)
            {
                await PlaceRandomOrderAsync(now, cancellationToken);
            }

            await HandOverReadyOrdersAsync(now, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Places one online order — also the entry point for the Engine Room to force one.</summary>
    public async Task<OnlineTicket> PlaceRandomOrderAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var channel = Channels[_random.Next(Channels.Length)];
        var mode = _random.NextDouble() < 0.5 ? FulfilmentMode.Takeaway : FulfilmentMode.Delivery;
        var customer = NameBook.OnlineCustomers.Pick(_random);
        var pizza = RecipeCatalog.Menu[_random.Next(RecipeCatalog.Menu.Count)];
        var amount = _random.Next(1, 4);

        var order = await orders.AddAsync(
            Order.Create(pizza, amount, channel, $"{customer} ({mode})"), cancellationToken);

        var ticket = new OnlineTicket(order.Id, channel, mode, customer, pizza, amount, now, TicketState.Cooking);
        lock (_tickets)
        {
            _tickets.Insert(0, ticket);
            while (_tickets.Count > 20)
            {
                _tickets.RemoveAt(_tickets.Count - 1);
            }
        }

        feed.Post(now, $"{ChannelEmoji(channel)} {ModeWord(mode)} order via {channel}: {amount}× {pizza} for {customer}.");
        return ticket;
    }

    private async Task HandOverReadyOrdersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var readyOrders = (await orders.GetByStateAsync(OrderState.Ready, cancellationToken))
            .ToDictionary(o => o.Id);

        List<OnlineTicket> cooking;
        lock (_tickets)
        {
            cooking = [.. _tickets.Where(t => t.State == TicketState.Cooking)];
        }

        foreach (var ticket in cooking)
        {
            if (!readyOrders.TryGetValue(ticket.OrderId, out var order))
            {
                continue;
            }

            var stamped = ticket.ReadyAt is null ? ticket with { ReadyAt = now } : ticket;
            if (now - stamped.ReadyAt >= options.HandoverDelay)
            {
                await orders.UpdateAsync(order.MarkDelivered(), cancellationToken);
                foreach (var pie in await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue, cancellationToken))
                {
                    if (pie.OrderId == order.Id)
                    {
                        await pizzas.UpdateAsync(pie.SendOut(), cancellationToken);
                    }
                }

                stamped = stamped with { State = TicketState.Done };
                feed.Post(now, stamped.Mode == FulfilmentMode.Delivery
                    ? $"🛵 Courier gone — {stamped.Amount}× {stamped.Pizza} racing to {stamped.Customer}."
                    : $"🛍️ {stamped.Customer} picked up {stamped.Amount}× {stamped.Pizza}. Smelled the bag. Approved.");
            }

            lock (_tickets)
            {
                var index = _tickets.FindIndex(t => t.OrderId == stamped.OrderId);
                if (index >= 0)
                {
                    _tickets[index] = stamped;
                }
            }
        }
    }

    public static string ChannelEmoji(OrderChannel channel) => channel switch
    {
        OrderChannel.Online => "🌐",
        OrderChannel.Bot => "💬",
        OrderChannel.Copilot => "🤖",
        OrderChannel.Phone => "📞",
        OrderChannel.Planned => "📅",
        OrderChannel.Restaurant => "🍽️",
        _ => "🧾",
    };

    private static string ModeWord(FulfilmentMode mode) =>
        mode == FulfilmentMode.Delivery ? "Delivery" : "Takeaway";
}
