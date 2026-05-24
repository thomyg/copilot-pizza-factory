namespace PizzaFactory.Domain.Recipes;

/// <summary>
/// An immutable dough recipe: its ingredients and how long the dough must rest before use.
/// </summary>
public sealed record DoughRecipe(
    string Name,
    IReadOnlyList<IngredientQuantity> Ingredients,
    TimeSpan RestingTime);
