namespace PizzaFactory.Domain.Recipes;

/// <summary>
/// The factory's fixed menu — preserved from the original demo: Margherita, Diavolo, Hawaii,
/// Prosciutto, Funghi, Al Tonno, all built on the Napolitan dough.
/// Times are deliberately short so the "perpetuum mobile" line is watchable in a demo.
/// </summary>
public static class RecipeCatalog
{
    public static DoughRecipe NapolitanDough { get; } = new(
        Name: "Napolitan",
        Ingredients:
        [
            IngredientQuantity.Of(Ingredient.Flour, 500),
            IngredientQuantity.Of(Ingredient.Water, 325),
            IngredientQuantity.Of(Ingredient.Salt, 10),
            IngredientQuantity.Of(Ingredient.Yeast, 3),
        ],
        RestingTime: TimeSpan.FromSeconds(20));

    private static readonly IReadOnlyList<PizzaRecipe> _pizzas =
    [
        Pizza("Margherita", [Topping(Ingredient.TomatoSauce, 120), Topping(Ingredient.Mozzarella, 150)]),
        Pizza("Diavolo", [Topping(Ingredient.TomatoSauce, 120), Topping(Ingredient.Mozzarella, 150), Topping(Ingredient.Salami, 100)]),
        Pizza("Hawaii", [Topping(Ingredient.TomatoSauce, 120), Topping(Ingredient.Mozzarella, 150), Topping(Ingredient.Ham, 90), Topping(Ingredient.Pineapple, 80)]),
        Pizza("Prosciutto", [Topping(Ingredient.TomatoSauce, 120), Topping(Ingredient.Mozzarella, 150), Topping(Ingredient.Ham, 100)]),
        Pizza("Funghi", [Topping(Ingredient.TomatoSauce, 120), Topping(Ingredient.Mozzarella, 150), Topping(Ingredient.Mushroom, 90)]),
        Pizza("Al Tonno", [Topping(Ingredient.TomatoSauce, 120), Topping(Ingredient.Mozzarella, 150), Topping(Ingredient.Tuna, 100)]),
    ];

    /// <summary>All pizzas the factory can make.</summary>
    public static IReadOnlyList<PizzaRecipe> Pizzas => _pizzas;

    /// <summary>The menu as display names.</summary>
    public static IReadOnlyList<string> Menu { get; } = [.. _pizzas.Select(p => p.Name)];

    /// <summary>Look up a pizza recipe by name, case-insensitively.</summary>
    public static PizzaRecipe? FindPizza(string name) =>
        _pizzas.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Look up a pizza recipe, throwing when the name is not on the menu.</summary>
    public static PizzaRecipe GetPizza(string name) =>
        FindPizza(name) ?? throw new ArgumentException($"'{name}' is not on the menu.", nameof(name));

    private static PizzaRecipe Pizza(string name, IReadOnlyList<IngredientQuantity> toppings) =>
        new(name, toppings, PreparingTime: TimeSpan.FromSeconds(5), BakingTime: TimeSpan.FromSeconds(10));

    private static IngredientQuantity Topping(Ingredient ingredient, int grams) =>
        IngredientQuantity.Of(ingredient, grams);
}
