namespace PizzaFactory.Trattoria;

/// <summary>
/// Pacing for the dining-room simulation. Defaults are tuned for a DEMO: a full table cycle
/// (arrive → order → eat → pay → leave) takes roughly one to two minutes, so the floor visibly
/// churns while you talk. Real restaurants are slower; demos are not restaurants.
/// </summary>
public sealed class TrattoriaOptions
{
    public const string SectionName = "Trattoria";

    /// <summary>Chance per tick that a new party walks in while service is open.</summary>
    public double ArrivalChancePerTick { get; set; } = 0.09;

    /// <summary>Chance per tick that an online order (web/chat/copilot/phone) comes in.</summary>
    public double OnlineOrderChancePerTick { get; set; } = 0.05;

    /// <summary>How long a seated party studies the menu before ordering.</summary>
    public TimeSpan OrderingDelay { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>How long a party eats once served.</summary>
    public TimeSpan EatingDuration { get; set; } = TimeSpan.FromSeconds(35);

    /// <summary>How long paying takes ("il conto, per favore" to chairs pushed back).</summary>
    public TimeSpan PayingDuration { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>Waiting longer than this for food sours the review.</summary>
    public TimeSpan GrumpyThreshold { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Ready takeaway/delivery orders leave the counter after this handover delay.</summary>
    public TimeSpan HandoverDelay { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>Chance that a party voices a special wish while ordering.</summary>
    public double WishChance { get; set; } = 0.35;

    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Optional fixed random seed — used by tests to make the theatre deterministic.</summary>
    public int? RandomSeed { get; set; }
}
