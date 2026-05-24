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
}
