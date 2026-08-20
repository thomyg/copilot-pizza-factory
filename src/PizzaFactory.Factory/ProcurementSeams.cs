using PizzaFactory.Domain;

namespace PizzaFactory.Factory;

/// <summary>
/// The back office's veto over big spending. Procurement asks before applying a refill:
/// true = proceed now (auto-approved), false = the order is held for a human decision and
/// the refill must NOT be applied yet. When no gate is registered the factory behaves as
/// before — fully autonomous.
/// </summary>
public interface IPurchaseGate
{
    bool RequestRefill(Ingredient ingredient, int grams, string? note = null);
}

/// <summary>Paper trail for restocks that bypass procurement (the A2A supplier self-heal).</summary>
public interface ISupplierLedger
{
    void RecordExternalDelivery(string supplier, Ingredient ingredient, int grams);
}
