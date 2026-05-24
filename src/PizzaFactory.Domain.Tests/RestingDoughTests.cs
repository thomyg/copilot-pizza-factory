using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Domain.Tests;

public class RestingDoughTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FromRecipe_starts_in_waiting_state()
    {
        var dough = RestingDough.FromRecipe(RecipeCatalog.NapolitanDough);
        Assert.Equal(DoughState.Waiting, dough.State);
        Assert.Equal("Napolitan", dough.Type);
    }

    [Fact]
    public void BeginResting_sets_finish_time_and_returns_a_new_instance()
    {
        var waiting = RestingDough.FromRecipe(RecipeCatalog.NapolitanDough);

        var resting = waiting.BeginResting(T0);

        Assert.Equal(DoughState.Resting, resting.State);
        Assert.Equal(T0, resting.StartedAt);
        Assert.Equal(T0 + RecipeCatalog.NapolitanDough.RestingTime, resting.FinishesAt);
        Assert.Equal(DoughState.Waiting, waiting.State); // immutability
    }

    [Fact]
    public void IsReady_is_false_before_finish_and_true_at_or_after()
    {
        var resting = RestingDough.FromRecipe(RecipeCatalog.NapolitanDough).BeginResting(T0);
        var finish = resting.FinishesAt!.Value;

        Assert.False(resting.IsReady(finish - TimeSpan.FromSeconds(1)));
        Assert.True(resting.IsReady(finish));
        Assert.True(resting.IsReady(finish + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Waiting_dough_is_never_ready()
    {
        var waiting = RestingDough.FromRecipe(RecipeCatalog.NapolitanDough);
        Assert.False(waiting.IsReady(T0.AddYears(1)));
    }
}
