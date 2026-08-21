namespace PizzaFactory.Factory;

/// <summary>Tuning for the autonomous factory floor (capacities, buffers, tick cadence).</summary>
public sealed class FactoryOptions
{
    public const string SectionName = "Factory";

    public int FridgeCapacity { get; set; } = 4;          // doughs resting simultaneously
    public int DoughBuffer { get; set; } = 6;             // keep this many doughs in flight (waiting+resting+ready)
    public int PrepCapacity { get; set; } = 8;            // pizzas being prepared simultaneously
    public int OvenCapacity { get; set; } = 8;            // pizzas baking simultaneously
    public int RestockThresholdGrams { get; set; } = 300; // restock an ingredient at/below this
    public int RestockAmountGrams { get; set; } = 1000;
    public int CrisisThresholdGrams { get; set; } = 150;  // escalate (human decision) at/below this

    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long a plated ticket may stand at the pass before the kitchen writes it off.
    /// Guests and pickup tickets live in memory while orders live in the store, so a restart
    /// strands food nobody remembers ordering; without this it sits at Ready forever.
    /// </summary>
    public TimeSpan AbandonedAfter { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How often the pass is swept. Reads the whole order book, so not every tick.</summary>
    public TimeSpan PassSweepInterval { get; set; } = TimeSpan.FromSeconds(30);
}
