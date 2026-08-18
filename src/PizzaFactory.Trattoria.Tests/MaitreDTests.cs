using PizzaFactory.Domain;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Trattoria.Tests;

public class MaitreDTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    private static (MaitreD MaitreD, InMemoryOrderRepository Orders, InMemoryPizzaRepository Pizzas, TrattoriaFeed Feed)
        Build(Action<TrattoriaOptions>? configure = null)
    {
        var options = new TrattoriaOptions
        {
            RandomSeed = 42,
            ArrivalChancePerTick = 1.0,
            OrderingDelay = TimeSpan.FromSeconds(1),
            EatingDuration = TimeSpan.FromSeconds(2),
            PayingDuration = TimeSpan.FromSeconds(1),
        };
        configure?.Invoke(options);

        var orders = new InMemoryOrderRepository();
        var pizzas = new InMemoryPizzaRepository();
        var feed = new TrattoriaFeed();
        return (new MaitreD(orders, pizzas, options, feed), orders, pizzas, feed);
    }

    [Fact]
    public void the_floor_has_seventeen_distinct_tables()
    {
        Assert.Equal(17, FloorPlan.Tables.Count);
        Assert.Equal(17, FloorPlan.Tables.Select(t => t.Id).Distinct().Count());
        Assert.All(FloorPlan.Tables, t => Assert.InRange(t.Seats, 2, 8));
        Assert.Contains(FloorPlan.Tables, t => t.Shape == TableShape.Round);
        Assert.Contains(FloorPlan.Tables, t => t.Shape == TableShape.Rect);
    }

    [Fact]
    public async Task nobody_is_seated_while_service_is_closed()
    {
        var (maitreD, orders, _, _) = Build();

        await maitreD.StepAsync(T0);

        Assert.All(maitreD.Snapshot().Tables, t => Assert.Null(t.Party));
        Assert.Empty(await orders.ListAsync());
    }

    [Fact]
    public async Task a_party_lives_the_full_evening_and_leaves_a_review()
    {
        var (maitreD, orders, _, _) = Build();
        maitreD.OpenService(T0);

        // Arrive + seat.
        await maitreD.StepAsync(T0);
        var seated = maitreD.Snapshot().Tables.Single(t => t.Party is not null);
        Assert.Equal(PartyState.Seated, seated.Party!.State);

        // Past the menu-studying phase: a REAL order lands on the factory.
        await maitreD.StepAsync(T0.AddSeconds(1.5));
        var order = Assert.Single(await orders.ListAsync());
        Assert.Equal(OrderChannel.Restaurant, order.Channel);
        Assert.Contains($"Table {seated.Table.Id}", order.CustomerName);

        // The kitchen finishes: order goes Ready -> the waiter serves it.
        await orders.UpdateAsync(order.MarkReady());
        await maitreD.StepAsync(T0.AddSeconds(2));
        Assert.Equal(PartyState.Eating, PartyAt(maitreD, seated.Table.Id).State);
        Assert.Equal(OrderState.Delivered, (await orders.ListAsync())[0].State);

        // Eat, pay, depart with a review; the table frees up.
        await maitreD.StepAsync(T0.AddSeconds(4.5));
        Assert.Equal(PartyState.Paying, PartyAt(maitreD, seated.Table.Id).State);

        await maitreD.StepAsync(T0.AddSeconds(6));
        var snapshot = maitreD.Snapshot();
        Assert.Null(snapshot.Tables.Single(t => t.Table.Id == seated.Table.Id).Party);
        Assert.Equal(1, snapshot.PartiesServed);
        Assert.NotNull(snapshot.AverageStars);
        Assert.Single(snapshot.RecentFeedback);
    }

    [Fact]
    public async Task slow_kitchens_earn_grumpy_reviews()
    {
        var (maitreD, orders, _, _) = Build(o => o.GrumpyThreshold = TimeSpan.Zero);
        maitreD.OpenService(T0);

        await maitreD.StepAsync(T0);
        await maitreD.StepAsync(T0.AddSeconds(1.5));                       // order placed
        var order = Assert.Single(await orders.ListAsync());
        await orders.UpdateAsync(order.MarkReady());
        await maitreD.StepAsync(T0.AddSeconds(60));                        // served late
        await maitreD.StepAsync(T0.AddSeconds(63));                        // eaten
        await maitreD.StepAsync(T0.AddSeconds(65));                        // paid + departed

        var feedback = Assert.Single(maitreD.Snapshot().RecentFeedback);
        Assert.InRange(feedback.Stars, 1, 2);
    }

    [Fact]
    public async Task a_bus_tour_overwhelms_the_floor_and_someone_walks_out()
    {
        var (maitreD, _, _, _) = Build(o => o.ArrivalChancePerTick = 0);
        maitreD.OpenService(T0);

        maitreD.BusTour(T0, parties: 30);
        await maitreD.StepAsync(T0);

        var snapshot = maitreD.Snapshot();
        var occupied = snapshot.Tables.Count(t => t.Party is not null);
        Assert.InRange(occupied, 1, 17);
        Assert.True(snapshot.Walkouts > 0, "with 30 parties and 17 tables, somebody must storm off");
    }

    private static Party PartyAt(MaitreD maitreD, int tableId) =>
        maitreD.Snapshot().Tables.Single(t => t.Table.Id == tableId).Party!;
}
