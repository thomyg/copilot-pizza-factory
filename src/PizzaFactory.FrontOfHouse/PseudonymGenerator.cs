namespace PizzaFactory.FrontOfHouse;

/// <summary>
/// Generates fun, zero-PII pizza pseudonyms for guests (e.g. "Anchovy Anonymous"), so the default
/// public experience collects no personal data. Guests may override — that override is moderated.
/// </summary>
public static class PseudonymGenerator
{
    private static readonly string[] Flavours =
        ["Anchovy", "Pepperoni", "Margherita", "Mozzarella", "Calzone", "Capricciosa",
         "Diavolo", "Funghi", "Basil", "Oregano", "Prosciutto", "Quattro"];

    private static readonly string[] Aliases =
        ["Anonymous", "Picasso", "Newton", "Einstein", "Bandit", "Maverick",
         "Wanderer", "Voyager", "Nomad", "Champion", "Legend", "Maestro"];

    public static string Generate() =>
        $"{Flavours[Random.Shared.Next(Flavours.Length)]} {Aliases[Random.Shared.Next(Aliases.Length)]}";
}
