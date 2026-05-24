using PizzaFactory.Domain;
using PizzaFactory.Domain.Entities;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>Cosmos persistence shape for a <see cref="Pizza"/> (partition "pizza").</summary>
internal sealed class PizzaDocument
{
    public string Id { get; set; } = "";
    public string PartitionKey { get; set; } = "pizza";
    public string Name { get; set; } = "";
    public string OrderId { get; set; } = "";
    public PizzaState State { get; set; }
    public string? CustomerName { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }

    public static PizzaDocument From(Pizza p) => new()
    {
        Id = p.Id,
        PartitionKey = "pizza",
        Name = p.Name,
        OrderId = p.OrderId,
        State = p.State,
        CustomerName = p.CustomerName,
        StartedAt = p.StartedAt,
        ReadyAt = p.ReadyAt,
    };

    public Pizza ToPizza() => new()
    {
        Id = Id,
        Name = Name,
        OrderId = OrderId,
        State = State,
        CustomerName = CustomerName,
        StartedAt = StartedAt,
        ReadyAt = ReadyAt,
    };
}

/// <summary>Cosmos persistence shape for a <see cref="RestingDough"/> (partition "dough").</summary>
internal sealed class RestingDoughDocument
{
    public string Id { get; set; } = "";
    public string PartitionKey { get; set; } = "dough";
    public string Type { get; set; } = "";
    public TimeSpan RestingTime { get; set; }
    public DoughState State { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishesAt { get; set; }

    public static RestingDoughDocument From(RestingDough d) => new()
    {
        Id = d.Id,
        PartitionKey = "dough",
        Type = d.Type,
        RestingTime = d.RestingTime,
        State = d.State,
        StartedAt = d.StartedAt,
        FinishesAt = d.FinishesAt,
    };

    public RestingDough ToRestingDough() => new()
    {
        Id = Id,
        Type = Type,
        RestingTime = RestingTime,
        State = State,
        StartedAt = StartedAt,
        FinishesAt = FinishesAt,
    };
}
