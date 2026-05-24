using PizzaFactory.Domain;
using PizzaFactory.Domain.Recipes;

namespace PizzaFactory.Domain.Tests;

public class RecipeCatalogTests
{
    [Fact]
    public void Menu_contains_the_six_classic_pizzas()
    {
        Assert.Equal(
            ["Margherita", "Diavolo", "Hawaii", "Prosciutto", "Funghi", "Al Tonno"],
            RecipeCatalog.Menu);
    }

    [Theory]
    [InlineData("hawaii")]
    [InlineData("HAWAII")]
    [InlineData("Hawaii")]
    public void GetPizza_is_case_insensitive(string name)
    {
        var recipe = RecipeCatalog.GetPizza(name);
        Assert.Equal("Hawaii", recipe.Name);
    }

    [Fact]
    public void GetPizza_throws_for_an_item_not_on_the_menu()
    {
        Assert.Throws<ArgumentException>(() => RecipeCatalog.GetPizza("Pineapple Surprise"));
    }

    [Fact]
    public void FindPizza_returns_null_for_unknown_item() =>
        Assert.Null(RecipeCatalog.FindPizza("Calzone"));

    [Fact]
    public void Hawaii_has_ham_and_pineapple()
    {
        var hawaii = RecipeCatalog.GetPizza("Hawaii");
        var ingredients = hawaii.Toppings.Select(t => t.Ingredient).ToList();

        Assert.Contains(Ingredient.Ham, ingredients);
        Assert.Contains(Ingredient.Pineapple, ingredients);
    }

    [Fact]
    public void PizzaRecipe_total_time_is_prep_plus_bake()
    {
        var margherita = RecipeCatalog.GetPizza("Margherita");
        Assert.Equal(margherita.PreparingTime + margherita.BakingTime, margherita.TotalTime);
    }

    [Fact]
    public void NapolitanDough_has_four_ingredients_and_a_resting_time()
    {
        Assert.Equal("Napolitan", RecipeCatalog.NapolitanDough.Name);
        Assert.Equal(4, RecipeCatalog.NapolitanDough.Ingredients.Count);
        Assert.True(RecipeCatalog.NapolitanDough.RestingTime > TimeSpan.Zero);
    }
}
