namespace PizzaFactory.Domain.Recipes;

/// <summary>
/// An immutable pizza recipe: its toppings and how long it takes to prepare and bake.
/// </summary>
public sealed record PizzaRecipe(
    string Name,
    IReadOnlyList<IngredientQuantity> Toppings,
    TimeSpan PreparingTime,
    TimeSpan BakingTime)
{
    /// <summary>Total time from prep start to out-of-oven.</summary>
    public TimeSpan TotalTime => PreparingTime + BakingTime;
}
