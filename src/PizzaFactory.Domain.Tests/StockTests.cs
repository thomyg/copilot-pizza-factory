using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Domain.Tests;

public class StockTests
{
    private static IngredientQuantity[] HamAndCheese =>
        [IngredientQuantity.Of(Ingredient.Ham, 100), IngredientQuantity.Of(Ingredient.Mozzarella, 150)];

    [Fact]
    public void Initial_stock_matches_the_opening_warehouse()
    {
        var stock = Stock.Initial();
        Assert.Equal(250, stock.GramsOf(Ingredient.Pineapple));
        Assert.Equal(2500, stock.GramsOf(Ingredient.Flour));
    }

    [Fact]
    public void Empty_stock_is_zero_for_every_ingredient()
    {
        var empty = Stock.Empty;
        Assert.All(Enum.GetValues<Ingredient>(), i => Assert.Equal(0, empty.GramsOf(i)));
    }

    [Fact]
    public void CanFulfill_is_true_when_enough_is_in_stock()
    {
        var ok = Stock.Initial().CanFulfill(HamAndCheese, out var missing);
        Assert.True(ok);
        Assert.Null(missing);
    }

    [Fact]
    public void CanFulfill_reports_the_first_missing_ingredient()
    {
        var ok = Stock.Empty.CanFulfill(HamAndCheese, out var missing);
        Assert.False(ok);
        Assert.Equal(Ingredient.Ham, missing);
    }

    [Fact]
    public void TryConsume_reduces_stock_and_leaves_the_original_untouched()
    {
        var original = Stock.Initial();

        var consumed = original.TryConsume(HamAndCheese, out var result, out _);

        Assert.True(consumed);
        Assert.Equal(1000, result.GramsOf(Ingredient.Ham));      // 1100 - 100
        Assert.Equal(1150, result.GramsOf(Ingredient.Mozzarella)); // 1300 - 150
        Assert.Equal(1100, original.GramsOf(Ingredient.Ham));    // immutability: unchanged
    }

    [Fact]
    public void TryConsume_fails_and_keeps_stock_unchanged_when_short()
    {
        var stock = Stock.Empty;

        var consumed = stock.TryConsume(HamAndCheese, out var result, out var missing);

        Assert.False(consumed);
        Assert.Equal(Ingredient.Ham, missing);
        Assert.Same(stock, result);
    }

    [Fact]
    public void Refill_adds_to_existing_stock()
    {
        var refilled = Stock.Initial().Refill([IngredientQuantity.Of(Ingredient.Pineapple, 250)]);
        Assert.Equal(500, refilled.GramsOf(Ingredient.Pineapple));
    }
}
