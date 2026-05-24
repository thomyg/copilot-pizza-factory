using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PizzaFactory.Safety.ContentSafety;

namespace PizzaFactory.Safety;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the offline heuristic content guard (no cloud dependency). Good for local dev and tests.
    /// </summary>
    public static IServiceCollection AddHeuristicContentGuard(this IServiceCollection services)
    {
        services.AddSingleton<IContentGuard, HeuristicContentGuard>();
        return services;
    }

    /// <summary>
    /// Registers the production guard: a composite of the heuristic guard (cheap length/PII/injection checks)
    /// and the Azure AI Content Safety guard (moderation + Prompt Shields), key-less via DefaultAzureCredential.
    /// Reads the "ContentSafety" configuration section.
    /// </summary>
    public static IServiceCollection AddAzureContentSafetyGuard(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AzureContentSafetyOptions.SectionName).Get<AzureContentSafetyOptions>()
            ?? throw new InvalidOperationException($"Missing '{AzureContentSafetyOptions.SectionName}' configuration section.");

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("ContentSafety:Endpoint is required.");
        }

        services.AddSingleton(options);
        services.AddSingleton<HeuristicContentGuard>();
        services.AddSingleton(sp => new AzureContentSafetyGuard(
            sp.GetRequiredService<AzureContentSafetyOptions>(), new DefaultAzureCredential()));
        services.AddSingleton<IContentGuard>(sp => new CompositeContentGuard(
        [
            sp.GetRequiredService<HeuristicContentGuard>(),
            sp.GetRequiredService<AzureContentSafetyGuard>(),
        ]));
        return services;
    }
}
