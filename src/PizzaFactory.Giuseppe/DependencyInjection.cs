using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace PizzaFactory.Giuseppe;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Giuseppe on an Azure OpenAI deployment, key-less (DefaultAzureCredential —
    /// az login locally, managed identity in Azure). Requires a content guard to be registered.
    /// </summary>
    public static IServiceCollection AddGiuseppe(this IServiceCollection services, Uri endpoint, string deployment)
    {
        services.AddSingleton(_ => new AzureOpenAIClient(endpoint, new DefaultAzureCredential()).GetChatClient(deployment));
        services.AddSingleton<GiuseppeAgent>();
        return services;
    }
}
