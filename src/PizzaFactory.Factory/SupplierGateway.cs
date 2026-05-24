using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.Domain;

namespace PizzaFactory.Factory;

/// <summary>A supplier's answer to a restock request: how much is confirmed and when it lands.</summary>
public sealed record RestockQuote(string Supplier, Ingredient Ingredient, int Grams, int EtaSeconds, bool Confirmed);

/// <summary>
/// The factory's view of the external supplier. The A2A specifics (agent-card discovery + the
/// replenish task) sit behind this seam, so the preview A2A bits can change without touching callers.
/// </summary>
public interface ISupplierGateway
{
    Task<RestockQuote> RequestRestockAsync(Ingredient ingredient, int grams, CancellationToken cancellationToken = default);
}

/// <summary>Reaches the external Supplier agent over HTTP (its A2A replenish task endpoint).</summary>
public sealed class HttpSupplierGateway(HttpClient http) : ISupplierGateway
{
    public async Task<RestockQuote> RequestRestockAsync(Ingredient ingredient, int grams, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            "/replenish", new { ingredient = ingredient.ToString(), grams }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var quote = await response.Content.ReadFromJsonAsync<SupplierQuote>(cancellationToken)
            ?? throw new InvalidOperationException("Empty supplier response.");

        return new RestockQuote(quote.Supplier, ingredient, quote.Grams, quote.EtaSeconds, quote.Confirmed);
    }

    private sealed record SupplierQuote(string Supplier, string Ingredient, int Grams, int EtaSeconds, bool Confirmed);
}

public static class SupplierGatewayExtensions
{
    /// <summary>Registers the A2A supplier gateway as a typed HttpClient pointed at the supplier agent.</summary>
    public static IServiceCollection AddSupplierGateway(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient<ISupplierGateway, HttpSupplierGateway>(client => client.BaseAddress = baseAddress);
        return services;
    }
}
