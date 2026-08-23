using PizzaFactory.Domain.Abstractions;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>
/// A closed service on the wire. The date rides as a plain "yyyy-MM-dd" string because
/// DateOnly has no natural JSON shape and a sortable string is what queries want anyway.
/// </summary>
public sealed class ServiceDocument
{
    public string Id { get; set; } = "";
    public string PartitionKey { get; set; } = "service";
    public string Date { get; set; } = "";
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset ClosedAt { get; set; }
    public int Orders { get; set; }
    public int Pizzas { get; set; }
    public int Guests { get; set; }
    public decimal RevenueEur { get; set; }
    public double? AverageStars { get; set; }

    public static ServiceDocument From(ClosedService service) => new()
    {
        Id = service.Id,
        PartitionKey = "service",
        Date = service.Date.ToString("yyyy-MM-dd"),
        OpenedAt = service.OpenedAt,
        ClosedAt = service.ClosedAt,
        Orders = service.Orders,
        Pizzas = service.Pizzas,
        Guests = service.Guests,
        RevenueEur = service.RevenueEur,
        AverageStars = service.AverageStars,
    };

    public ClosedService ToClosedService() => new(
        Id,
        DateOnly.TryParse(Date, out var date) ? date : DateOnly.FromDateTime(ClosedAt.LocalDateTime),
        OpenedAt,
        ClosedAt,
        Orders,
        Pizzas,
        Guests,
        RevenueEur,
        AverageStars);
}
