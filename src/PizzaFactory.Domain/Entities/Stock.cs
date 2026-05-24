using System.Collections.Frozen;

namespace PizzaFactory.Domain.Entities;

/// <summary>
/// The factory's ingredient stock, in grams. Immutable: consuming or refilling returns a
/// new <see cref="Stock"/>, so the running floor never mutates a shared instance underfoot.
/// </summary>
public sealed class Stock
{
    private readonly FrozenDictionary<Ingredient, int> _grams;

    private Stock(IReadOnlyDictionary<Ingredient, int> grams) =>
        _grams = grams.ToFrozenDictionary();

    /// <summary>An empty warehouse — every ingredient at zero.</summary>
    public static Stock Empty { get; } = new(
        Enum.GetValues<Ingredient>().ToDictionary(i => i, _ => 0));

    /// <summary>The factory's standard opening stock (preserved from the original demo).</summary>
    public static Stock Initial() => new(new Dictionary<Ingredient, int>
    {
        [Ingredient.Flour] = 2500,
        [Ingredient.Water] = 5000,
        [Ingredient.Salt] = 1000,
        [Ingredient.Yeast] = 500,
        [Ingredient.TomatoSauce] = 3000,
        [Ingredient.Mozzarella] = 1300,
        [Ingredient.Ham] = 1100,
        [Ingredient.Salami] = 1300,
        [Ingredient.Pineapple] = 250,
        [Ingredient.Mushroom] = 500,
        [Ingredient.Tuna] = 600,
    });

    /// <summary>Grams currently in stock for an ingredient.</summary>
    public int GramsOf(Ingredient ingredient) => _grams.GetValueOrDefault(ingredient);

    /// <summary>
    /// Can the requested ingredients be served from current stock?
    /// On failure, <paramref name="missing"/> names the first ingredient that falls short.
    /// </summary>
    public bool CanFulfill(IEnumerable<IngredientQuantity> required, out Ingredient? missing)
    {
        foreach (var item in required)
        {
            if (GramsOf(item.Ingredient) < item.Grams)
            {
                missing = item.Ingredient;
                return false;
            }
        }

        missing = null;
        return true;
    }

    /// <summary>
    /// Try to take the required ingredients. Returns false (leaving stock unchanged) if any
    /// ingredient is short.
    /// </summary>
    public bool TryConsume(IEnumerable<IngredientQuantity> required, out Stock result, out Ingredient? missing)
    {
        var list = required as IReadOnlyCollection<IngredientQuantity> ?? [.. required];
        if (!CanFulfill(list, out missing))
        {
            result = this;
            return false;
        }

        var next = new Dictionary<Ingredient, int>(_grams);
        foreach (var item in list)
        {
            next[item.Ingredient] = next.GetValueOrDefault(item.Ingredient) - item.Grams;
        }

        result = new Stock(next);
        return true;
    }

    /// <summary>Add ingredients back into stock, returning the new stock level.</summary>
    public Stock Refill(IEnumerable<IngredientQuantity> additions)
    {
        var next = new Dictionary<Ingredient, int>(_grams);
        foreach (var item in additions)
        {
            next[item.Ingredient] = next.GetValueOrDefault(item.Ingredient) + item.Grams;
        }

        return new Stock(next);
    }
}
