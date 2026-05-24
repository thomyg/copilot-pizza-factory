using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PizzaFactory.Factory;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the autonomous factory floor (Dough Master + Pizzaiolo + Procurement) and the
    /// background worker that ticks them. Requires a store to be registered first
    /// (AddInMemoryPizzaFactoryStore or AddCosmosPizzaFactoryStore).
    /// </summary>
    public static IServiceCollection AddPizzaFactoryFloor(this IServiceCollection services, Action<FactoryOptions>? configure = null)
    {
        var options = new FactoryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEscalationSink, LoggingEscalationSink>();
        services.AddSingleton<DoughMaster>();
        services.AddSingleton<Pizzaiolo>();
        services.AddSingleton<Procurement>();
        services.AddSingleton<CrisisWatch>();
        services.AddHostedService<FactoryWorker>();
        return services;
    }

    /// <summary>
    /// Makes the crisis self-healing: escalations are fanned out to the logging sink AND the supplier
    /// sink (orders the missing ingredient over the A2A gateway and restocks). Requires a supplier
    /// gateway (AddSupplierGateway) and a store to be registered.
    /// </summary>
    public static IServiceCollection AddSupplierSelfHeal(this IServiceCollection services)
    {
        services.AddSingleton<LoggingEscalationSink>();
        services.AddSingleton<SupplierEscalationSink>();
        services.AddSingleton<IEscalationSink>(sp => new CompositeEscalationSink(
        [
            sp.GetRequiredService<LoggingEscalationSink>(),
            sp.GetRequiredService<SupplierEscalationSink>(),
        ]));
        return services;
    }
}
