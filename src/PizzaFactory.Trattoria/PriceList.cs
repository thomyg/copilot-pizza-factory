namespace PizzaFactory.Trattoria;

/// <summary>
/// The menu's prices in EUR. Lives with the trattoria (front of business), not the factory —
/// the oven doesn't care what a Diavolo costs; the bookkeeper does.
/// </summary>
public static class PriceList
{
    private static readonly Dictionary<string, decimal> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Margherita"] = 9.90m,
        ["Diavolo"] = 12.90m,
        ["Hawaii"] = 11.90m,
        ["Prosciutto"] = 12.40m,
        ["Funghi"] = 11.40m,
        ["Al Tonno"] = 12.90m,
    };

    /// <summary>Price for a pizza; unknown names fall back to the house average.</summary>
    public static decimal Of(string pizza) => Prices.GetValueOrDefault(pizza, 11.90m);
}
