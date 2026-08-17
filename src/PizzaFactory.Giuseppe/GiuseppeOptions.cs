using Microsoft.Extensions.Configuration;

namespace PizzaFactory.Giuseppe;

/// <summary>Where Giuseppe's workplace context comes from.</summary>
public enum WorkIqMode
{
    /// <summary>Deterministic demo data (find_meeting over <c>RehearsalWorkContext</c>). Default — works everywhere.</summary>
    Rehearsal,

    /// <summary>Live Microsoft Work IQ MCP server (preview-labeled), with rehearsal as automatic fallback.</summary>
    Live,

    /// <summary>No workplace tools at all.</summary>
    Off,
}

/// <summary>
/// Configuration for Giuseppe: the Azure OpenAI deployment he thinks with, the factory MCP server
/// he orders from, and the Work IQ integration he learns the guest's workplace from.
/// All auth is key-less (DefaultAzureCredential) — no secrets, per house rules.
/// </summary>
public sealed record GiuseppeOptions
{
    /// <summary>Default scope for the universal Work IQ MCP endpoint (HTTP mode).</summary>
    public const string DefaultWorkIqScope = "https://workiq.svc.cloud.microsoft/.default";

    public required Uri Endpoint { get; init; }
    public required string Deployment { get; init; }

    /// <summary>Our factory MCP server (orders/stock/recipes). Null → no factory tools.</summary>
    public Uri? FactoryMcpUrl { get; init; }

    /// <summary>Entra scope for the factory MCP when it sits behind Easy Auth (e.g. "api://…/.default"). Null → anonymous.</summary>
    public string? FactoryMcpScope { get; init; }

    public WorkIqMode WorkIqMode { get; init; } = WorkIqMode.Rehearsal;

    /// <summary>Stdio command for live Work IQ (the `workiq` CLI's MCP server). Used when <see cref="WorkIqUrl"/> is unset.</summary>
    public string WorkIqCommand { get; init; } = "workiq";

    public IReadOnlyList<string> WorkIqArguments { get; init; } = ["mcp"];

    /// <summary>HTTP endpoint of the universal Work IQ MCP server; wins over stdio when set.</summary>
    public Uri? WorkIqUrl { get; init; }

    public string WorkIqScope { get; init; } = DefaultWorkIqScope;

    /// <summary>Binds from "Giuseppe:*" and "WorkIq:*" sections. Returns null when Giuseppe isn't configured.</summary>
    public static GiuseppeOptions? From(IConfiguration configuration)
    {
        var endpoint = configuration["Giuseppe:Endpoint"];
        var deployment = configuration["Giuseppe:Deployment"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return null;
        }

        var factoryUrl = configuration["Giuseppe:FactoryMcpUrl"];
        var workIqUrl = configuration["WorkIq:Url"];

        return new GiuseppeOptions
        {
            Endpoint = new Uri(endpoint),
            Deployment = deployment,
            FactoryMcpUrl = string.IsNullOrWhiteSpace(factoryUrl) ? null : new Uri(factoryUrl),
            FactoryMcpScope = NullIfEmpty(configuration["Giuseppe:FactoryMcpScope"]),
            WorkIqMode = Enum.TryParse<WorkIqMode>(configuration["WorkIq:Mode"], ignoreCase: true, out var mode)
                ? mode
                : WorkIqMode.Rehearsal,
            WorkIqCommand = NullIfEmpty(configuration["WorkIq:Command"]) ?? "workiq",
            WorkIqUrl = string.IsNullOrWhiteSpace(workIqUrl) ? null : new Uri(workIqUrl),
            WorkIqScope = NullIfEmpty(configuration["WorkIq:Scope"]) ?? DefaultWorkIqScope,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
