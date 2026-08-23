using System.Globalization;
using PizzaFactory.BackOffice;

namespace PizzaFactory.Web;

public sealed record NonnaChatRequest(string Message);

public sealed record NonnaChatResponse(bool Allowed, string Reply);

public sealed record RejectRequest(string? Reason);

public sealed record TimeOffFiling(string Name, string Date, string? Slot, string? Reason);

public sealed record TimeOffDecision(string? Cover, string? Reason);

/// <summary>
/// Nonna's service hatch. She lives in Microsoft 365 — Copilot, Teams, SharePoint — and
/// never opens the store backend; these endpoints are how her M365 surfaces reach the
/// back office. Chat runs her agent (back-office belt only); the desk endpoints power the
/// SharePoint approvals/rota web parts. Reuses the SPFx CORS policy and rate limiter.
/// </summary>
public static class NonnaApi
{
    private const int MaxMessageLength = 2000;

    public static void MapNonnaApi(this WebApplication app)
    {
        var hasCors = !string.IsNullOrWhiteSpace(app.Configuration["SharePointChat:AllowedOrigins"]);
        var nonna = app.Services.GetService<Nonna>();
        var staff = app.Services.GetRequiredService<StaffBook>();
        var purchases = app.Services.GetRequiredService<PurchaseBook>();
        var timeOff = app.Services.GetRequiredService<TimeOffBook>();

        var group = app.MapGroup("/api/nonna").RequireRateLimiting(GiuseppeChatApi.ReadRateLimitPolicy);
        if (hasCors)
        {
            group = group.RequireCors(GiuseppeChatApi.CorsPolicy);
        }

        // Her chat still rides the strict budget — that one talks to a model.
        group.MapPost("/chat", async (NonnaChatRequest request, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
            {
                return Results.BadRequest(new NonnaChatResponse(false, "A message, tesoro — short and to the point."));
            }

            if (nonna?.Agent is null)
            {
                return Results.Ok(new NonnaChatResponse(
                    false, "Nonna is at the market — no model configured. The ledgers wait patiently. 🧾"));
            }

            var reply = await nonna.Agent.AskAsync(request.Message, cancellationToken);
            return Results.Ok(new NonnaChatResponse(reply.Allowed, reply.Text));
        });

        group.MapGet("/rota", (int days) =>
            Results.Ok(staff.Rota(staff.Today, Math.Clamp(days <= 0 ? 3 : days, 1, 7))
                .Select(e => new { date = e.Date.ToString("yyyy-MM-dd"), slot = e.Slot.ToString(), role = e.Role.ToString(), assignedTo = e.AssignedTo })));

        group.MapGet("/purchase-orders", () =>
            Results.Ok(purchases.Orders().Select(o => new
            {
                id = o.Id,
                ingredient = o.Ingredient.ToString(),
                grams = o.Grams,
                cost = o.Cost,
                supplier = o.Supplier,
                state = o.State.ToString(),
                note = o.Note,
                at = o.At,
                // Which rule fired, and the sentence that explains it. A back office that only
                // says no is not a process; one that says why is.
                decision = o.Decision.ToString(),
                why = purchases.Explain(o),
            })));

        group.MapPost("/purchase-orders/{id}/approve", (string id) =>
            purchases.Approve(id) is { } order
                ? Results.Ok(new { order.Id, state = order.State.ToString() })
                : Results.NotFound(new { error = $"No pending order '{id}'." }));

        group.MapPost("/purchase-orders/{id}/reject", (string id, RejectRequest request) =>
            purchases.Reject(id, string.IsNullOrWhiteSpace(request.Reason) ? "no reason given" : request.Reason!) is { } order
                ? Results.Ok(new { order.Id, state = order.State.ToString() })
                : Results.NotFound(new { error = $"No pending order '{id}'." }));

        group.MapGet("/invoices", () =>
            Results.Ok(purchases.Invoices().Select(i => new
            {
                id = i.Id,
                supplier = i.Supplier,
                ingredient = i.Ingredient.ToString(),
                grams = i.Grams,
                cost = i.Cost,
                at = i.At,
            })));

        // --- Time off: file, decide. Filing is free of consequence; deciding moves the rota.

        group.MapGet("/time-off", () =>
            Results.Ok(timeOff.Requests().Select(r => new
            {
                id = r.Id,
                name = r.Name,
                date = r.Date.ToString("yyyy-MM-dd"),
                slot = r.Slot?.ToString(),
                reason = r.Reason,
                state = r.State.ToString(),
                cover = r.ProposedCover,
                leavesAGap = r.LeavesAGap,
                note = r.Note,
                summary = TimeOffBook.Explain(r),
            })));

        group.MapPost("/time-off", (TimeOffFiling filing) =>
        {
            if (string.IsNullOrWhiteSpace(filing.Name) ||
                !DateOnly.TryParse(filing.Date, CultureInfo.InvariantCulture, out var date))
            {
                return Results.BadRequest(new { error = "A name and a date (yyyy-MM-dd), per favore." });
            }

            var slot = Enum.TryParse<ShiftSlot>(filing.Slot, ignoreCase: true, out var parsed) ? parsed : (ShiftSlot?)null;
            var request = timeOff.Request(filing.Name, date, slot, filing.Reason ?? "not stated");
            return Results.Ok(new { id = request.Id, cover = request.ProposedCover, summary = TimeOffBook.Explain(request) });
        });

        group.MapPost("/time-off/{id}/approve", (string id, TimeOffDecision? decision) =>
            timeOff.Approve(id, string.IsNullOrWhiteSpace(decision?.Cover) ? null : decision.Cover) is { } approved
                ? Results.Ok(new { approved.Id, state = approved.State.ToString(), note = approved.Note })
                : Results.NotFound(new { error = $"No pending request '{id}'." }));

        group.MapPost("/time-off/{id}/decline", (string id, TimeOffDecision? decision) =>
            timeOff.Decline(id, decision?.Reason ?? "no reason given") is { } declined
                ? Results.Ok(new { declined.Id, state = declined.State.ToString(), note = declined.Note })
                : Results.NotFound(new { error = $"No pending request '{id}'." }));

        // --- The money the approvals are spending.

        group.MapGet("/budget", () =>
        {
            var position = purchases.Position();
            return Results.Ok(new
            {
                period = position.Period,
                budgetEur = position.BudgetEur,
                committedEur = position.CommittedEur,
                remainingEur = position.RemainingEur,
                usedPercent = position.UsedPercent,
                isTight = position.IsTight,
                orders = position.OrdersCounted,
            });
        });

        // Her whole desk in one call, shaped as the SPFx cockpit's IBackOfficeSnapshot —
        // so Nonna in Microsoft 365 Copilot reads the REAL ERP instead of rehearsal data.
        group.MapGet("/desk", () =>
        {
            var rota = staff.Rota(staff.Today, 3);
            var absentToday = staff.Absences()
                .Where(a => a.Date == staff.Today)
                .Select(a => a.Name)
                .FirstOrDefault();

            return Results.Ok(new
            {
                rota = rota.Select(e => new
                {
                    dayLabel = e.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture),
                    slot = e.Slot.ToString(),
                    role = e.Role.ToString(),
                    assignedTo = e.AssignedTo,
                }),
                orders = purchases.Orders().Select(o => new
                {
                    id = o.Id,
                    ingredient = o.Ingredient.ToString(),
                    grams = o.Grams,
                    cost = o.Cost,
                    supplier = o.Supplier,
                    state = o.State.ToString(),
                    note = o.Note,
                    decision = o.Decision.ToString(),
                    why = purchases.Explain(o),
                }),
                invoices = purchases.Invoices().Select(i => new
                {
                    id = i.Id,
                    supplier = i.Supplier,
                    ingredient = i.Ingredient.ToString(),
                    grams = i.Grams,
                    cost = i.Cost,
                }),
                invoiceTotal = purchases.Invoices().Sum(i => i.Cost),
                absentToday = absentToday ?? "Nobody — full house today.",
                timeOff = timeOff.Requests().Take(6).Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    date = r.Date.ToString("yyyy-MM-dd"),
                    slot = r.Slot?.ToString(),
                    reason = r.Reason,
                    state = r.State.ToString(),
                    cover = r.ProposedCover,
                    leavesAGap = r.LeavesAGap,
                    summary = TimeOffBook.Explain(r),
                }),
                budget = Budget(purchases),
            });
        });
    }

    /// <summary>The month's position, shaped the same wherever it is read.</summary>
    private static object Budget(PurchaseBook purchases)
    {
        var position = purchases.Position();
        return new
        {
            period = position.Period,
            budgetEur = position.BudgetEur,
            committedEur = position.CommittedEur,
            remainingEur = position.RemainingEur,
            usedPercent = position.UsedPercent,
            isTight = position.IsTight,
        };
    }
}
