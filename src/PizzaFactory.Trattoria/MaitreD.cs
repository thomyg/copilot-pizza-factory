using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Trattoria;

/// <summary>A table plus whoever is sitting at it right now (null = free).</summary>
public sealed record TableView(Table Table, Party? Party);

/// <summary>Point-in-time view of the dining room for the dashboard.</summary>
public sealed record TrattoriaSnapshot(
    bool IsOpen,
    IReadOnlyList<TableView> Tables,
    int PartiesServed,
    int Walkouts,
    double? AverageStars,
    IReadOnlyList<PartyFeedback> RecentFeedback);

/// <summary>
/// The maître d': opens and closes service, seats arriving parties, takes their orders (REAL
/// orders on the real factory), serves them when the pass says ready, and collects the review
/// on the way out. The verdict is honest: parties that waited past the grumpy threshold leave
/// worse reviews — so sabotaging the pantry from the Engine Room visibly costs you stars.
/// </summary>
public sealed class MaitreD(
    IOrderRepository orders,
    IPizzaRepository pizzas,
    TrattoriaOptions options,
    TrattoriaFeed feed,
    ILogger<MaitreD>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<MaitreD>.Instance;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Random _random = options.RandomSeed is { } seed ? new Random(seed) : Random.Shared;
    private readonly Dictionary<int, Party> _seated = [];   // table id -> party
    private readonly List<PartyFeedback> _recentFeedback = [];
    private bool _isOpen;
    private int _served;
    private int _walkouts;
    private int _pendingArrivals;

    public bool IsOpen => _isOpen;

    public void OpenService(DateTimeOffset now)
    {
        _isOpen = true;
        feed.Post(now, "🔔 Service is OPEN — chairs down, oven hot, Giuseppe humming.");
    }

    public void CloseService(DateTimeOffset now)
    {
        _isOpen = false;
        feed.Post(now, "🌙 Service is closed — current guests finish, nobody new is seated.");
    }

    /// <summary>A bus parks outside: the next ticks seat this many extra parties. Chaos-console fuel.</summary>
    public void BusTour(DateTimeOffset now, int parties = 6)
    {
        Interlocked.Add(ref _pendingArrivals, parties);
        feed.Post(now, $"🚌 A tour bus just parked outside — {parties} parties heading for the door!");
    }

    public TrattoriaSnapshot Snapshot()
    {
        lock (_recentFeedback)
        {
            var tables = FloorPlan.Tables
                .Select(t => new TableView(t, _seated.GetValueOrDefault(t.Id)))
                .ToList();

            double? avg = _recentFeedback.Count > 0 ? _recentFeedback.Average(f => f.Stars) : null;
            return new TrattoriaSnapshot(_isOpen, tables, _served, _walkouts, avg, [.. _recentFeedback]);
        }
    }

    public async Task StepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await SeatArrivalsAsync(now);
            await ProgressPartiesAsync(now, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task SeatArrivalsAsync(DateTimeOffset now)
    {
        var arrivals = Interlocked.Exchange(ref _pendingArrivals, 0);
        if (_isOpen && _random.NextDouble() < options.ArrivalChancePerTick)
        {
            arrivals++;
        }

        for (var i = 0; i < arrivals; i++)
        {
            // Party sizes lean small — mostly twos and fours, the odd big family.
            var size = _random.Next(1, 10) switch
            {
                <= 4 => 2,
                <= 7 => _random.Next(3, 5),
                8 => _random.Next(5, 7),
                _ => _random.Next(6, 9),
            };

            var table = FloorPlan.Tables
                .Where(t => t.Seats >= size && !_seated.ContainsKey(t.Id))
                .OrderBy(t => t.Seats)
                .FirstOrDefault();

            var name = NameBook.PartyNames.Pick(_random);
            if (table is null)
            {
                _walkouts++;
                feed.Post(now, $"😤 {Cap(name)} (party of {size}) found no free table and left, sighing operatically.");
                continue;
            }

            var party = new Party
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = name,
                Size = size,
                TableId = table.Id,
                SinceUtc = now,
            };
            _seated[table.Id] = party;
            feed.Post(now, $"🚪 {Cap(name)} (party of {size}) seated at table {table.Id}.");
        }

        return Task.CompletedTask;
    }

    private async Task ProgressPartiesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var party in _seated.Values.ToList())
        {
            var elapsed = now - party.SinceUtc;
            switch (party.State)
            {
                case PartyState.Seated when elapsed >= options.OrderingDelay:
                    await PlaceOrderAsync(party, now, cancellationToken);
                    break;

                case PartyState.WaitingForFood:
                    await TryServeAsync(party, now, cancellationToken);
                    break;

                case PartyState.Eating when elapsed >= options.EatingDuration:
                    _seated[party.TableId] = party.Advance(PartyState.Paying, now);
                    break;

                case PartyState.Paying when elapsed >= options.PayingDuration:
                    Depart(party, now);
                    break;
            }
        }
    }

    private async Task PlaceOrderAsync(Party party, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pizza = RecipeCatalog.Menu[_random.Next(RecipeCatalog.Menu.Count)];
        var amount = Math.Max(1, (party.Size + 1) / 2);
        var order = await orders.AddAsync(
            Order.Create(pizza, amount, OrderChannel.Restaurant, $"Table {party.TableId} · {party.Name}"),
            cancellationToken);

        string? wish = null;
        if (_random.NextDouble() < options.WishChance)
        {
            wish = NameBook.Wishes.Pick(_random);
            feed.Post(now, $"💬 Table {party.TableId}: {wish}");
        }

        _seated[party.TableId] = party.Advance(PartyState.WaitingForFood, now) with
        {
            OrderId = order.Id,
            OrderedAtUtc = now,
            Wish = wish,
        };
        feed.Post(now, $"📝 Table {party.TableId} ordered {amount}× {pizza}.");
    }

    private async Task TryServeAsync(Party party, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ready = (await orders.GetByStateAsync(OrderState.Ready, cancellationToken))
            .FirstOrDefault(o => o.Id == party.OrderId);
        if (ready is null)
        {
            return;
        }

        await orders.UpdateAsync(ready.MarkDelivered(), cancellationToken);
        foreach (var pie in await pizzas.GetByStateAsync(PizzaState.Ready, int.MaxValue, cancellationToken))
        {
            if (pie.OrderId == ready.Id)
            {
                await pizzas.UpdateAsync(pie.SendOut(), cancellationToken);
            }
        }

        _seated[party.TableId] = party.Advance(PartyState.Eating, now);
        feed.Post(now, $"🍕 Table {party.TableId} served — {ready.Amount}× {ready.ItemName}. Buon appetito!");
    }

    private void Depart(Party party, DateTimeOffset now)
    {
        var waited = party.OrderedAtUtc is { } ordered
            ? now - ordered - options.EatingDuration - options.PayingDuration
            : TimeSpan.Zero;

        var feedback = MakeFeedback(waited);
        lock (_recentFeedback)
        {
            _recentFeedback.Insert(0, feedback);
            while (_recentFeedback.Count > 20)
            {
                _recentFeedback.RemoveAt(_recentFeedback.Count - 1);
            }
        }

        _seated.Remove(party.TableId);
        _served++;
        feed.Post(now, $"{new string('⭐', feedback.Stars)} Table {party.TableId} ({party.Name}): “{feedback.Comment}”");
        _logger.LogDebug("Party {Name} departed table {Table} with {Stars} stars", party.Name, party.TableId, feedback.Stars);
    }

    private PartyFeedback MakeFeedback(TimeSpan waitedForFood)
    {
        if (waitedForFood > options.GrumpyThreshold)
        {
            return new PartyFeedback(_random.Next(1, 3), NameBook.GrumpyReviews.Pick(_random));
        }

        return _random.Next(1, 11) switch
        {
            <= 2 => new PartyFeedback(3, NameBook.NeutralReviews.Pick(_random)),
            <= 6 => new PartyFeedback(4, NameBook.HappyReviews.Pick(_random)),
            _ => new PartyFeedback(5, NameBook.HappyReviews.Pick(_random)),
        };
    }

    private static string Cap(string name) => char.ToUpperInvariant(name[0]) + name[1..];
}
