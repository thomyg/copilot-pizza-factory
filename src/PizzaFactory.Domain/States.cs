namespace PizzaFactory.Domain;

/// <summary>Where an order is in its lifecycle.</summary>
public enum OrderState
{
    Created,
    Started,
    Ready,
    Delivered,
}

/// <summary>Where a single pizza is on its journey across the factory floor.</summary>
public enum PizzaState
{
    OrderAccepted,
    Preparing,
    Baking,
    Ready,
    Out,
}

/// <summary>Where a batch of dough is in the resting process.</summary>
public enum DoughState
{
    Waiting,
    Resting,
    Ready,
    Processed,
}

/// <summary>How an order reached the factory. <see cref="Guest"/> is the public, audience-facing channel.</summary>
public enum OrderChannel
{
    Online,
    Restaurant,
    Planned,
    Bot,
    Guest,
}
