using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using PizzaFactory.Giuseppe.Tools;

namespace PizzaFactory.BackOffice;

/// <summary>
/// Nonna's tool belt — the back office and nothing but the back office. She reads the rota,
/// records absences, finds and assigns cover, and rules on purchase orders. She has no oven
/// tools and no order tools: Giuseppe runs the floor, Nonna runs the books. Tool belts are
/// authorization.
/// </summary>
public sealed class NonnaToolSource(StaffBook staff, PurchaseBook purchases, TimeOffBook timeOff, TimeProvider? clock = null) : IGiuseppeToolSource
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AITool> tools =
        [
            AIFunctionFactory.Create(StaffDirectory, "staff_directory",
                "The full roster: everyone's name, role (Pizzaiolo/Service/Courier) and whether they are oven-certified."),
            AIFunctionFactory.Create(GetRota, "get_rota",
                "The shift rota for the next days: who works which slot (Lunch/Dinner) in which role, and which seats are OPEN."),
            AIFunctionFactory.Create(ReportAbsence, "report_absence",
                "Record that someone is out on a date (whole day, or one slot). Returns the shifts that just became open."),
            AIFunctionFactory.Create(FindCover, "find_cover",
                "Qualified, available cover candidates for an open seat, best first (right role, oven-certified where required, not absent, not already working, fewest shifts this week)."),
            AIFunctionFactory.Create(AssignShift, "assign_shift",
                "Assign a staff member to an open seat on the rota. Validates qualification and availability."),
            AIFunctionFactory.Create(ListPurchaseOrders, "list_purchase_orders",
                "Purchase orders, newest first: pending approvals, approved, delivered, rejected — with grams, cost and supplier."),
            AIFunctionFactory.Create(ApprovePurchaseOrder, "approve_purchase_order",
                "Approve a PENDING purchase order by id (e.g. 'PO-1004'). Delivery and invoice follow automatically."),
            AIFunctionFactory.Create(RejectPurchaseOrder, "reject_purchase_order",
                "Reject a PENDING purchase order by id, with a short reason."),
            AIFunctionFactory.Create(ListInvoices, "list_invoices",
                "Supplier invoices, newest first — every delivery leaves one, including A2A self-heal restocks."),

            // Time off: filing is free of consequence, approving is the step that moves the rota.
            AIFunctionFactory.Create(RequestTimeOff, "request_time_off",
                "File a time-off request for someone. Works out qualified cover BEFORE anyone decides and returns the candidates; changes nothing on the rota."),
            AIFunctionFactory.Create(ListTimeOff, "list_time_off",
                "Time-off requests, newest first: who asked for what, whether cover exists, and what was decided."),
            AIFunctionFactory.Create(ApproveTimeOff, "approve_time_off",
                "Approve a PENDING time-off request by id (e.g. 'TO-500'). Records the absence and hands the shift to cover. Optionally name the substitute."),
            AIFunctionFactory.Create(DeclineTimeOff, "decline_time_off",
                "Decline a PENDING time-off request by id, with a short reason. The reason is kept."),

            // Money: the budget is a separate control from the approval limit.
            AIFunctionFactory.Create(BudgetPosition, "budget_position",
                "This month's supplies budget: what is committed, what is left, and how tight it is. Committed includes orders still awaiting a signature."),
        ];

        return Task.FromResult(tools);
    }

    private static string StaffDirectory() =>
        string.Join("\n", Roster.Members.Select(m =>
            $"{m.Name} — {m.Role}{(m.OvenCertified ? " (oven-certified)" : "")} — {m.Quirk}"));

    private string GetRota(
        [Description("How many days ahead, starting today (1-7). Default 3.")] int days = 3)
    {
        var entries = staff.Rota(staff.Today, Math.Clamp(days, 1, 7));
        var sb = new StringBuilder();
        foreach (var group in entries.GroupBy(e => (e.Date, e.Slot)))
        {
            sb.Append(group.Key.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture))
              .Append(' ').Append(group.Key.Slot).Append(": ");
            sb.AppendJoin(", ", group.Select(e => $"{e.Role}={(e.AssignedTo ?? "OPEN")}"));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ReportAbsence(
        [Description("Staff member's first name, e.g. 'Maria'.")] string name,
        [Description("Date, yyyy-MM-dd. Omit or empty = today.")] string? date = null,
        [Description("'Lunch' or 'Dinner'; omit for the whole day.")] string? slot = null)
    {
        var when = ParseDate(date);
        ShiftSlot? one = slot?.Trim().ToLowerInvariant() switch
        {
            "lunch" => ShiftSlot.Lunch,
            "dinner" => ShiftSlot.Dinner,
            _ => null,
        };

        var opened = staff.ReportAbsence(name, when, one);
        return opened.Count == 0
            ? $"Noted — {name} is out on {when:ddd d MMM}. No assigned shifts were affected."
            : $"Noted — {name} is out on {when:ddd d MMM}. Now OPEN: " +
              string.Join("; ", opened.Select(o => $"{o.Slot} {o.Role}")) +
              ". Use find_cover to fill the seat.";
    }

    private string FindCover(
        [Description("Date, yyyy-MM-dd. Omit or empty = today.")] string? date,
        [Description("'Lunch' or 'Dinner'.")] string slot,
        [Description("Role to cover: 'Pizzaiolo', 'Service' or 'Courier'.")] string role)
    {
        var candidates = staff.FindCover(ParseDate(date), ParseSlot(slot), ParseRole(role));
        return candidates.Count == 0
            ? "Nobody qualified is free — Nonna will make calls."
            : "Best candidates, in order: " + string.Join(", ", candidates.Select(c => c.Name)) + ".";
    }

    private string AssignShift(
        [Description("Staff member's first name.")] string name,
        [Description("Date, yyyy-MM-dd. Omit or empty = today.")] string? date,
        [Description("'Lunch' or 'Dinner'.")] string slot,
        [Description("Role: 'Pizzaiolo', 'Service' or 'Courier'.")] string role)
    {
        try
        {
            var entry = staff.Assign(name, ParseDate(date), ParseSlot(slot), ParseRole(role));
            return $"Done — {entry.AssignedTo} takes {entry.Slot} {entry.Role} on {entry.Date:ddd d MMM}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ex.Message;
        }
    }

    private string ListPurchaseOrders(
        [Description("Filter: 'pending', 'approved', 'delivered', 'rejected' or empty for all.")] string? state = null)
    {
        PurchaseOrderState? filter = state?.Trim().ToLowerInvariant() switch
        {
            "pending" => PurchaseOrderState.PendingApproval,
            "approved" => PurchaseOrderState.Approved,
            "delivered" => PurchaseOrderState.Delivered,
            "rejected" => PurchaseOrderState.Rejected,
            _ => null,
        };

        var orders = purchases.Orders(filter);
        return orders.Count == 0
            ? "No purchase orders on file" + (filter is null ? "." : $" in state '{state}'.")
            : string.Join("\n", orders.Take(15).Select(o =>
                $"{o.Id}: {o.Grams}g {o.Ingredient} — €{o.Cost} — {o.State} — {o.Supplier}{(o.Note is null ? "" : $" ({o.Note})")}"));
    }

    private string ApprovePurchaseOrder([Description("The order id, e.g. 'PO-1004'.")] string id)
    {
        var order = purchases.Approve(id);
        return order is null
            ? $"No pending order '{id}' — check list_purchase_orders."
            : $"Approved {order.Id}: {order.Grams}g {order.Ingredient} for €{order.Cost}. Delivery and invoice follow.";
    }

    private string RejectPurchaseOrder(
        [Description("The order id.")] string id,
        [Description("Short reason.")] string reason)
    {
        var order = purchases.Reject(id, reason);
        return order is null
            ? $"No pending order '{id}' — check list_purchase_orders."
            : $"Rejected {order.Id}. The pantry will have to cope; Procurement may file again.";
    }

    [Description("File a time-off request. Returns the request id, the cover the house found, and what it means.")]
    private string RequestTimeOff(
        [Description("Staff member's first name, e.g. 'Maria'.")] string name,
        [Description("Date, yyyy-MM-dd.")] string date,
        [Description("'Lunch' or 'Dinner'; omit for the whole day.")] string? slot = null,
        [Description("Short reason, e.g. 'wedding'. Optional.")] string? reason = null)
    {
        var request = timeOff.Request(
            name,
            ParseDate(date),
            string.IsNullOrWhiteSpace(slot) ? null : ParseSlot(slot),
            reason ?? "not stated");
        return $"{request.Id} — {TimeOffBook.Explain(request)}";
    }

    [Description("Time-off requests, newest first.")]
    private string ListTimeOff(
        [Description("Filter: 'Pending', 'Approved', 'Declined'. Omit for all.")] string? state = null)
    {
        var filter = Enum.TryParse<TimeOffState>(state, ignoreCase: true, out var parsed) ? parsed : (TimeOffState?)null;
        var requests = timeOff.Requests(filter);
        if (requests.Count == 0)
        {
            return "Nothing in the time-off book.";
        }

        var text = new StringBuilder();
        foreach (var r in requests.Take(12))
        {
            text.Append(CultureInfo.InvariantCulture, $"{r.Id} [{r.State}] {TimeOffBook.Explain(r)}");
            if (r.Note is not null)
            {
                text.Append(CultureInfo.InvariantCulture, $" — {r.Note}");
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    [Description("Approve a pending time-off request; records the absence and assigns cover.")]
    private string ApproveTimeOff(
        [Description("Request id, e.g. 'TO-500'.")] string id,
        [Description("Who should cover. Omit to take the first qualified candidate.")] string? cover = null)
    {
        var approved = timeOff.Approve(id, string.IsNullOrWhiteSpace(cover) ? null : cover.Trim());
        return approved is null
            ? $"No pending request '{id}'."
            : $"{approved.Id}: {approved.Note}.";
    }

    [Description("Decline a pending time-off request, keeping the reason.")]
    private string DeclineTimeOff(
        [Description("Request id, e.g. 'TO-500'.")] string id,
        [Description("Why it was declined.")] string reason)
    {
        var declined = timeOff.Decline(id, reason);
        return declined is null ? $"No pending request '{id}'." : $"{declined.Id}: {declined.Note}.";
    }

    [Description("This month's supplies budget position.")]
    private string BudgetPosition()
    {
        var position = purchases.Position();
        if (position.BudgetEur <= 0)
        {
            return "No supplies budget is configured, so nothing is being guarded against it.";
        }

        var mood = position.IsTight ? "That is tight." : "Comfortable so far.";
        return $"{position.Period}: €{position.CommittedEur:0.00} committed of €{position.BudgetEur:0.00} " +
               $"({position.UsedPercent}%), €{position.RemainingEur:0.00} left across {position.OrdersCounted} order(s). {mood}";
    }

    private string ListInvoices()
    {
        var invoices = purchases.Invoices();
        var total = invoices.Sum(i => i.Cost);
        return invoices.Count == 0
            ? "No invoices yet — a quiet ledger is a suspicious ledger."
            : string.Join("\n", invoices.Take(15).Select(i =>
                  $"{i.Id}: {i.Grams}g {i.Ingredient} — €{i.Cost} — {i.Supplier} — {i.At.ToLocalTime():HH:mm}")) +
              $"\nTotal on file: €{total}.";
    }

    private DateOnly ParseDate(string? date) =>
        string.IsNullOrWhiteSpace(date)
            ? staff.Today
            : DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : staff.Today;

    private static ShiftSlot ParseSlot(string slot) =>
        slot.Trim().Equals("lunch", StringComparison.OrdinalIgnoreCase) ? ShiftSlot.Lunch : ShiftSlot.Dinner;

    private static StaffRole ParseRole(string role) => role.Trim().ToLowerInvariant() switch
    {
        "pizzaiolo" => StaffRole.Pizzaiolo,
        "courier" => StaffRole.Courier,
        _ => StaffRole.Service,
    };
}
