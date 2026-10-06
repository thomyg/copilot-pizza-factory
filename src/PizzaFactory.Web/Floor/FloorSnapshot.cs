namespace PizzaFactory.Web.Floor;

/// <summary>
/// Everything the stage draws, in one read: the line pizza by pizza, the pantry, the dining
/// room, the couriers' tickets, Nonna's desk and the brigade. <see cref="Seq"/> is the floor
/// bus's last event number at the time of the read, so a viewer knows which events the
/// snapshot already contains.
/// </summary>
public sealed record FloorSnapshot(
    long Seq,
    DateTimeOffset At,
    ServiceView Service,
    KitchenView Kitchen,
    IReadOnlyList<StockView> Stock,
    DiningView Dining,
    IReadOnlyList<TicketView> Tickets,
    OfficeView Office,
    IReadOnlyList<StaffView> Staff);

public sealed record ServiceView(bool Open, double? MinutesLeft);

public sealed record KitchenView(
    int FridgeCapacity,
    int PrepCapacity,
    int OvenCapacity,
    IReadOnlyList<PizzaView> Pizzas,
    IReadOnlyList<DoughView> Doughs);

/// <summary>One pizza on the line. Progress is computed by the viewer from StartedAt and the recipe's times.</summary>
public sealed record PizzaView(
    string Id,
    string Name,
    string OrderId,
    string State,
    DateTimeOffset? StartedAt,
    double PrepSeconds,
    double BakeSeconds,
    int? TableId,
    string? Surface = null);

public sealed record DoughView(string Id, string State, DateTimeOffset? StartedAt, DateTimeOffset? FinishesAt);

public sealed record StockView(string Ingredient, int Grams, int Opening, double Level);

public sealed record DiningView(IReadOnlyList<TableSnap> Tables, int GuestsServed, int Walkouts, double? AverageStars);

public sealed record TableSnap(int Id, int Seats, string Shape, double X, double Y, double W, double H, PartyView? Party);

public sealed record PartyView(
    string Name,
    int Size,
    string State,
    DateTimeOffset Since,
    string? OrderId,
    DateTimeOffset? OrderedAt,
    string? Wish);

public sealed record TicketView(
    string OrderId,
    string Mode,
    string Customer,
    string Pizza,
    int Amount,
    string State,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ReadyAt,
    string? Surface = null,
    string? Guest = null);

public sealed record OfficeView(
    BudgetView Budget,
    int AutoApproveLimitGrams,
    IReadOnlyList<PurchaseOrderView> PurchaseOrders,
    IReadOnlyList<InvoiceView> Invoices,
    IReadOnlyList<TimeOffView> TimeOff,
    IReadOnlyList<RotaView> Rota);

public sealed record BudgetView(string Period, decimal BudgetEur, decimal CommittedEur, decimal RemainingEur, double UsedPercent, bool IsTight);

public sealed record PurchaseOrderView(
    string Id,
    string Ingredient,
    int Grams,
    decimal Cost,
    string Supplier,
    string State,
    string Decision,
    DateTimeOffset At,
    string Why);

public sealed record InvoiceView(string Id, string Supplier, string Ingredient, int Grams, decimal Cost, DateTimeOffset At);

public sealed record TimeOffView(
    string Id,
    string Name,
    string Day,
    string? Slot,
    string Reason,
    string State,
    IReadOnlyList<string> Cover,
    bool LeavesAGap,
    DateTimeOffset At);

public sealed record RotaView(string Day, string Slot, string Role, string? AssignedTo);

public sealed record StaffView(string Name, string Role, bool OvenCertified, string Quirk, bool AbsentToday);
