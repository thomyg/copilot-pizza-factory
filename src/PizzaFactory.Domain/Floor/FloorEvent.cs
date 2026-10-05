using System.Text.Json.Serialization;

namespace PizzaFactory.Domain.Floor;

/// <summary>
/// Which way an action reached the house. The stage shows this — "Nonna, via Copilot" — so an
/// audience can see that something done in Microsoft 365 just moved the factory. It is a
/// channel, never a person: the stage is on a projector, and names do not belong there.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FloorChannel>))]
public enum FloorChannel
{
    /// <summary>The agents running the house on their own. The default for everything.</summary>
    Autonomous,
    Copilot,
    Teams,
    SharePoint,

    /// <summary>The Blazor app itself (guest order form, storefront chat).</summary>
    Web,

    /// <summary>The Engine Room's levers. Set by the app, never accepted from a header.</summary>
    Presenter,
}

/// <summary>The room an event belongs to, so the stage knows where to look.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FloorArea>))]
public enum FloorArea
{
    Storeroom,
    Kitchen,
    Dining,
    BreakRoom,
    Office,
    Street,
}

/// <summary>The kinds of thing that happen on the floor. Strings, so the stage's JS can switch on them.</summary>
public static class FloorEventKinds
{
    public const string ServiceOpened = "service.opened";
    public const string ServiceClosed = "service.closed";

    public const string OrderPlaced = "order.placed";
    public const string PartySeated = "party.seated";
    public const string PartyLeft = "party.left";

    public const string StockLow = "stock.low";
    public const string StockReceived = "stock.received";
    public const string StockChanged = "stock.changed";

    public const string PurchaseOrderRequested = "po.requested";
    public const string PurchaseOrderApproved = "po.approved";
    public const string PurchaseOrderRejected = "po.rejected";
    public const string PurchaseOrderBlocked = "po.blocked";
    public const string InvoiceFiled = "invoice.filed";

    public const string TimeOffRequested = "timeoff.requested";
    public const string TimeOffApproved = "timeoff.approved";
    public const string TimeOffDeclined = "timeoff.declined";
    public const string ShiftAssigned = "shift.assigned";
    public const string AbsenceReported = "absence.reported";

    public const string Escalation = "escalation";
    public const string AgentToolCall = "agent.tool";
}

/// <summary>What a publisher says happened. Number, time and (usually) channel are filled in by the bus.</summary>
/// <param name="Actor">An agent or role — "Nonna", "Procurement", "Capo". Never a real person's name.</param>
/// <param name="Subject">The thing it happened to: "PO-2041", "table-9", "Pineapple".</param>
public sealed record FloorEventDraft(
    string Actor,
    FloorArea Area,
    string Kind,
    string Subject,
    string Text,
    FloorChannel? Channel = null);

/// <summary>Something that happened on the floor, numbered so a viewer can catch up on what it missed.</summary>
public sealed record FloorEvent(
    long Seq,
    DateTimeOffset At,
    string Actor,
    FloorChannel Channel,
    FloorArea Area,
    string Kind,
    string Subject,
    string Text)
{
    /// <summary>A banner on a stage, not a log line.</summary>
    public const int MaxTextLength = 160;
}
