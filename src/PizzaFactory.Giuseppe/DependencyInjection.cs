using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PizzaFactory.Giuseppe.Tools;
using PizzaFactory.Giuseppe.WorkContext;

namespace PizzaFactory.Giuseppe;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Giuseppe from configuration ("Giuseppe:*" + "WorkIq:*"): Azure OpenAI chat with
    /// function invocation, factory MCP tools when Giuseppe:FactoryMcpUrl is set, and workplace
    /// context per WorkIq:Mode (Rehearsal default; Live = Work IQ MCP with rehearsal fallback).
    /// Key-less throughout (DefaultAzureCredential — az login locally, managed identity in Azure).
    /// Requires a content guard to be registered. No-ops when Giuseppe:Endpoint/Deployment are absent.
    /// </summary>
    public static IServiceCollection AddGiuseppe(this IServiceCollection services, IConfiguration configuration)
    {
        if (GiuseppeOptions.From(configuration) is not { } options)
        {
            return services;
        }

        services.AddSingleton(options);
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        services.AddSingleton(CreateChatClient);

        if (options.FactoryMcpUrl is not null)
        {
            services.AddSingleton<IGiuseppeToolSource>(sp => new McpToolSource(
                new McpToolSourceOptions
                {
                    Name = "factory",
                    Endpoint = options.FactoryMcpUrl,
                    BearerTokenProvider = options.FactoryMcpScope is { } scope
                        ? TokenProvider(sp.GetRequiredService<TokenCredential>(), scope)
                        : null,
                },
                fallback: null,
                sp.GetService<ILogger<McpToolSource>>()));
        }

        switch (options.WorkIqMode)
        {
            case WorkIqMode.Rehearsal:
                services.AddSingleton<IGiuseppeToolSource>(_ => new WorkContextToolSource(new RehearsalWorkContext()));
                break;

            case WorkIqMode.Live:
                // PREVIEW: Microsoft Work IQ (GA APIs, but our integration path — the workiq CLI stdio
                // server / universal MCP endpoint — is early). Thin seam per house rules; the rehearsal
                // fallback keeps the storyline alive when the live side misbehaves on stage.
                services.AddSingleton<IGiuseppeToolSource>(sp => new McpToolSource(
                    options.WorkIqUrl is { } url
                        ? new McpToolSourceOptions
                        {
                            Name = "work-iq",
                            Endpoint = url,
                            BearerTokenProvider = TokenProvider(sp.GetRequiredService<TokenCredential>(), options.WorkIqScope),
                        }
                        : new McpToolSourceOptions
                        {
                            Name = "work-iq",
                            Command = options.WorkIqCommand,
                            Arguments = options.WorkIqArguments,
                        },
                    fallback: new WorkContextToolSource(new RehearsalWorkContext()),
                    sp.GetService<ILogger<McpToolSource>>()));
                break;

            case WorkIqMode.Off:
                break;
        }

        services.AddSingleton(sp => new GiuseppeAgent(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<Safety.IContentGuard>(),
            sp.GetServices<IGiuseppeToolSource>(),
            logger: sp.GetService<ILogger<GiuseppeAgent>>()));

        return services;
    }


    private static IChatClient CreateChatClient(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<GiuseppeOptions>();
        var credential = sp.GetRequiredService<TokenCredential>();

        // IncludeDetailedErrors: this is a demo — surfacing the real tool error to the model (and
        // therefore the transcript) beats a generic "something failed" both on stage and in debugging.
        return new AzureOpenAIClient(options.Endpoint, credential)
            .GetChatClient(options.Deployment)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation(
                sp.GetService<ILoggerFactory>(),
                client => client.IncludeDetailedErrors = true)
            .Build(sp);
    }

    private static Func<CancellationToken, Task<string?>> TokenProvider(TokenCredential credential, string scope) =>
        async cancellationToken =>
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken);
            return token.Token;
        };
}
