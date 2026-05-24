namespace PizzaFactory.Domain;

/// <summary>
/// Every raw material the factory tracks in stock — dough ingredients and pizza toppings alike.
/// Names are preserved from the original Pizza Factory demo.
/// </summary>
public enum Ingredient
{
    // Dough
    Flour,
    Water,
    Salt,
    Yeast,

    // Toppings
    TomatoSauce,
    Mozzarella,
    Ham,
    Salami,
    Pineapple,
    Mushroom,
    Tuna,
}

/// <summary>A quantity of a single ingredient, measured in grams.</summary>
public sealed record IngredientQuantity(Ingredient Ingredient, int Grams)
{
    public static IngredientQuantity Of(Ingredient ingredient, int grams) => new(ingredient, grams);
}
