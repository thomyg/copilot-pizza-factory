using PizzaFactory.Domain;
using PizzaFactory.Domain.Floor;
using PizzaFactory.Factory;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Factory.Tests;

/// <summary>The presenter's levers show up on the stage as the presenter's, never as the agents' or Copilot's.</summary>
public sealed class DemoDirectorFloorTests
{
    private static (DemoDirector Director, FloorEventBus Bus) Rig()
    {
        var bus = new FloorEventBus(TimeProvider.System);
        return (new DemoDirector(new InMemoryOrderRepository(), new InMemoryStockRepository(), bus), bus);
    }

    [Fact]
    public async Task sabotage_is_reported_in_the_storeroom_as_the_presenters_doing()
    {
        var (director, bus) = Rig();

        using (FloorChannelScope.Enter(FloorChannel.Copilot))
        {
            await director.SabotageAsync(Ingredient.Pineapple);
        }

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorChannel.Presenter, e.Channel);
        Assert.Equal(FloorArea.Storeroom, e.Area);
        Assert.Equal(FloorEventKinds.StockChanged, e.Kind);
        Assert.Equal("Pineapple", e.Subject);
    }

    [Fact]
    public async Task sabotaging_an_empty_silo_reports_nothing()
    {
        var (director, bus) = Rig();
        await director.SabotageAsync(Ingredient.Tuna);
        var before = bus.Since(0).Count;

        await director.SabotageAsync(Ingredient.Tuna);

        Assert.Equal(before, bus.Since(0).Count);
    }

    [Fact]
    public async Task a_rush_is_one_event_not_a_hundred()
    {
        var (director, bus) = Rig();

        await director.RushHourAsync(40);

        var e = Assert.Single(bus.Since(0));
        Assert.Equal(FloorArea.Dining, e.Area);
        Assert.Contains("40", e.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task restock_and_adjust_are_reported()
    {
        var (director, bus) = Rig();

        await director.AdjustAsync(Ingredient.Flour, 500);
        await director.RestockAsync();

        Assert.Equal(2, bus.Since(0).Count);
    }
}
