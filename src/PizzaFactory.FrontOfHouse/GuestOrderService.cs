using PizzaFactory.Domain;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Domain.Entities;
using PizzaFactory.Domain.Recipes;
using PizzaFactory.Safety;

namespace PizzaFactory.FrontOfHouse;

/// <summary>
/// Headless intake behind the public order page: resolve a (moderated) display name, validate the
/// order against the menu, and place it as a Guest order. The UI (Blazor) and the Window dashboard
/// sit on top of this.
/// </summary>
public sealed class GuestOrderService(IOrderRepository orders, IContentGuard guard, FrontOfHouseOptions options, OrderingGate gate)
{
    public async Task<GuestOrderResult> PlaceAsync(GuestOrderRequest request, CancellationToken cancellationToken = default)
    {
        // Default identity is a generated pseudonym (zero PII). An override is untrusted public input -> moderate it.
        var displayName = PseudonymGenerator.Generate();

        if (!gate.IsOpen)
        {
            return GuestOrderResult.Rejected(displayName, "Ordering is closed right now — check back soon!");
        }
        if (!string.IsNullOrWhiteSpace(request.DisplayNameOverride))
        {
            var verdict = await guard.InspectAsync(request.DisplayNameOverride, cancellationToken);
            if (!verdict.Allowed)
            {
                return GuestOrderResult.Rejected(displayName, $"Name blocked: {verdict.Reason}");
            }

            displayName = request.DisplayNameOverride.Trim();
        }

        var recipe = RecipeCatalog.FindPizza(request.Pizza);
        if (recipe is null)
        {
            return GuestOrderResult.Rejected(displayName, $"'{request.Pizza}' is not on the menu.");
        }

        if (request.Amount < 1 || request.Amount > options.MaxPerOrder)
        {
            return GuestOrderResult.Rejected(displayName, $"Amount must be between 1 and {options.MaxPerOrder}.");
        }

        var order = await orders.AddAsync(
            Order.Create(recipe.Name, request.Amount, OrderChannel.Guest, displayName), cancellationToken);

        return GuestOrderResult.Ok(displayName, order.Id);
    }
}
