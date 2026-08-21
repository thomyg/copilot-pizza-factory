using System.Globalization;
using PizzaFactory.BackOffice;

namespace PizzaFactory.Web;

public sealed record NonnaChatRequest(string Message);

public sealed record NonnaChatResponse(bool Allowed, string Reply);

public sealed record RejectRequest(string? Reason);

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
            });
        });
    }
}
