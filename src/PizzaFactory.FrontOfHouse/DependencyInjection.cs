using Microsoft.Extensions.DependencyInjection;

namespace PizzaFactory.FrontOfHouse;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the front-of-house intake. Requires a store (AddInMemory/AddCosmosPizzaFactoryStore)
    /// and a content guard (AddHeuristicContentGuard or AddAzureContentSafetyGuard) to be registered.
    /// </summary>
    public static IServiceCollection AddFrontOfHouse(this IServiceCollection services, Action<FrontOfHouseOptions>? configure = null)
    {
        var options = new FrontOfHouseOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<OrderingGate>();
        services.AddScoped<GuestOrderService>();
        return services;
    }
}
