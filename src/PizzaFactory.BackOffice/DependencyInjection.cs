using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.Factory;

namespace PizzaFactory.BackOffice;

public static class DependencyInjection
{
    /// <summary>
    /// TrattoriaSoft ERP 3000: the staff book, the purchase ledger (wired into the factory's
    /// procurement as gate + supplier paper trail), and the delivery dock worker.
    /// </summary>
    public static IServiceCollection AddBackOffice(
        this IServiceCollection services,
        Action<BackOfficeOptions>? configure = null)
    {
        // Bound from configuration so the approval limit and the monthly budget can be tuned
        // per deployment — a demo wants a budget tight enough to hit, a real tenant does not.
        var options = new BackOfficeOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<StaffBook>();
        services.AddSingleton<TimeOffBook>();
        services.AddSingleton<PurchaseBook>();
        services.AddSingleton<IPurchaseGate>(sp => new PurchaseGateAdapter(sp.GetRequiredService<PurchaseBook>()));
        services.AddSingleton<ISupplierLedger>(sp => new SupplierLedgerAdapter(sp.GetRequiredService<PurchaseBook>()));
        services.AddHostedService<BackOfficeWorker>();
        return services;
    }

    private sealed class PurchaseGateAdapter(PurchaseBook book) : IPurchaseGate
    {
        public bool RequestRefill(Domain.Ingredient ingredient, int grams, string? note = null) =>
            book.Request(ingredient, grams, note);
    }

    private sealed class SupplierLedgerAdapter(PurchaseBook book) : ISupplierLedger
    {
        public void RecordExternalDelivery(string supplier, Domain.Ingredient ingredient, int grams) =>
            book.RecordExternalDelivery(supplier, ingredient, grams);
    }
}
