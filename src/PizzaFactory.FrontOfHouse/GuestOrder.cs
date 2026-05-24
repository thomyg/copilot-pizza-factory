namespace PizzaFactory.FrontOfHouse;

/// <summary>A public order request from the Window/order page. DisplayNameOverride is optional, public, moderated.</summary>
public sealed record GuestOrderRequest(string Pizza, int Amount, string? DisplayNameOverride = null);

/// <summary>Outcome of a guest order: accepted (with the resolved display name + order id) or rejected with a reason.</summary>
public sealed record GuestOrderResult(bool Accepted, string DisplayName, string? OrderId, string? Reason)
{
    public static GuestOrderResult Ok(string displayName, string orderId) => new(true, displayName, orderId, null);

    public static GuestOrderResult Rejected(string displayName, string reason) => new(false, displayName, null, reason);
}

/// <summary>Front-of-house tuning.</summary>
public sealed class FrontOfHouseOptions
{
    public int MaxPerOrder { get; set; } = 5;
}
