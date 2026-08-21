using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PizzaFactory.Domain;

namespace PizzaFactory.Trattoria;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dining room: maître d', online order desk, pre-order book, live feed, and
    /// the worker that ticks them. Requires a store (order/pizza repositories) to be registered.
    /// The floor follows the ServiceWindow: nothing trades until a service is opened.
    /// </summary>
    public static IServiceCollection AddTrattoria(this IServiceCollection services, Action<TrattoriaOptions>? configure = null)
    {
        var options = new TrattoriaOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new ServiceWindowOptions());
        services.TryAddSingleton<ServiceWindow>();
        services.AddSingleton<TrattoriaFeed>();
        services.AddSingleton<MaitreD>();
        services.AddSingleton<OnlineOrderDesk>();
        services.AddSingleton<PreOrderBook>();
        services.AddSingleton<Bookkeeper>();
        services.AddHostedService<TrattoriaWorker>();
        return services;
    }
}
