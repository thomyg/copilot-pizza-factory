using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.Domain.Abstractions;
using PizzaFactory.Infrastructure.Cosmos;
using PizzaFactory.Infrastructure.InMemory;

namespace PizzaFactory.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the in-memory factory store (orders + stock) as singletons. Good for local dev and tests.
    /// </summary>
    public static IServiceCollection AddInMemoryPizzaFactoryStore(this IServiceCollection services)
    {
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton<IStockRepository, InMemoryStockRepository>();
        services.AddSingleton<IPizzaRepository, InMemoryPizzaRepository>();
        services.AddSingleton<IRestingDoughRepository, InMemoryRestingDoughRepository>();
        return services;
    }

    /// <summary>
    /// Registers the Cosmos DB factory store with key-less auth (<see cref="DefaultAzureCredential"/> —
    /// az login locally, managed identity in Azure). Reads the "Cosmos" configuration section.
    /// </summary>
    public static IServiceCollection AddCosmosPizzaFactoryStore(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CosmosOptions.SectionName).Get<CosmosOptions>()
            ?? throw new InvalidOperationException($"Missing '{CosmosOptions.SectionName}' configuration section.");

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("Cosmos:Endpoint is required.");
        }

        services.AddSingleton(options);
        // Lazy: build the client on first use, not at registration — a Cosmos init failure must not
        // take down the whole host at startup (it did on Functions Flex Consumption).
        services.AddSingleton<CosmosClient>(_ => CreateCosmosClient(options));
        services.AddSingleton<IOrderRepository, CosmosOrderRepository>();
        services.AddSingleton<IStockRepository, CosmosStockRepository>();
        services.AddSingleton<IPizzaRepository, CosmosPizzaRepository>();
        services.AddSingleton<IRestingDoughRepository, CosmosRestingDoughRepository>();
        return services;
    }

    /// <summary>Builds a key-less <see cref="CosmosClient"/> using System.Text.Json (camelCase + string enums).</summary>
    public static CosmosClient CreateCosmosClient(CosmosOptions options)
    {
        var serializer = new SystemTextJsonCosmosSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        });

        var clientOptions = new CosmosClientOptions
        {
            ApplicationName = "PizzaFactory",
            Serializer = serializer,
            // Gateway mode: HTTPS-only and lazily connected — robust on serverless / restricted-egress
            // hosts (Functions Flex Consumption), where Direct-mode TCP setup can stall at startup.
            ConnectionMode = ConnectionMode.Gateway,
        };

        return new CosmosClient(options.Endpoint, new DefaultAzureCredential(), clientOptions);
    }
}
