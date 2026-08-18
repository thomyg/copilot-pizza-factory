// Pizza Factory — Aspire "control tower".
// Orchestrates the factory's services and lights up the dashboard / OpenTelemetry.
// Skeleton: one placeholder API service. Real services (MCP servers, agents,
// Cosmos DB, the Blazor Web App) get wired in here as the rebuild progresses.

var builder = DistributedApplication.CreateBuilder(args);

// Optional: set Cosmos:Endpoint in AppHost config/env to run the factory on real Cosmos;
// otherwise services fall back to their in-memory store. Key-less (managed identity / az login).
var cosmosEndpoint = builder.Configuration["Cosmos:Endpoint"];

builder.AddProject<Projects.PizzaFactory_ApiService>("apiservice");

// MCP server exposing the factory's Orders + Inventory tools over Streamable HTTP.
var mcp = builder.AddProject<Projects.PizzaFactory_Mcp>("mcp");
if (!string.IsNullOrWhiteSpace(cosmosEndpoint))
{
    mcp.WithEnvironment("Cosmos__Endpoint", cosmosEndpoint);
}

// External Supplier agent (A2A): the factory's Procurement reaches it when stock runs low.
var supplier = builder.AddProject<Projects.PizzaFactory_Supplier>("supplier");

// The "Window" — Blazor Web App that RUNS the factory floor (perpetuum mobile) and shows it live.
var web = builder.AddProject<Projects.PizzaFactory_Web>("web")
    .WithReference(supplier)
    .WithEnvironment("Supplier__Endpoint", supplier.GetEndpoint("https"));
if (!string.IsNullOrWhiteSpace(cosmosEndpoint))
{
    web.WithEnvironment("Cosmos__Endpoint", cosmosEndpoint);
}

// Giuseppe (AI concierge): pass the Azure OpenAI deployment through if configured (key-less).
// He always gets the local factory MCP server as his ordering tools, so "order pizza for
// Friday's retro" places real orders on the factory floor you're watching in the dashboard.
var giuseppeEndpoint = builder.Configuration["Giuseppe:Endpoint"];
var giuseppeDeployment = builder.Configuration["Giuseppe:Deployment"];
if (!string.IsNullOrWhiteSpace(giuseppeEndpoint) && !string.IsNullOrWhiteSpace(giuseppeDeployment))
{
    web.WithReference(mcp)
       .WithEnvironment("Giuseppe__Endpoint", giuseppeEndpoint)
       .WithEnvironment("Giuseppe__Deployment", giuseppeDeployment)
       .WithEnvironment("Giuseppe__FactoryMcpUrl", ReferenceExpression.Create($"{mcp.GetEndpoint("http")}/mcp"));

    // Real Responsible-AI guard (Azure AI Content Safety + Prompt Shields) when configured —
    // otherwise the web app falls back to the offline heuristic Bouncer.
    var contentSafetyEndpoint = builder.Configuration["ContentSafety:Endpoint"];
    if (!string.IsNullOrWhiteSpace(contentSafetyEndpoint))
    {
        web.WithEnvironment("ContentSafety__Endpoint", contentSafetyEndpoint);
    }

    // Local dev credential wiring: the Azure OpenAI resource may live in a different tenant than
    // az login's default. Set Azure:TenantId (user-secrets) so DefaultAzureCredential asks the CLI
    // for the RIGHT tenant — otherwise every Giuseppe chat dies with a 400 tenant mismatch.
    var azureTenantId = builder.Configuration["Azure:TenantId"];
    if (!string.IsNullOrWhiteSpace(azureTenantId))
    {
        web.WithEnvironment("AZURE_TENANT_ID", azureTenantId)
           .WithEnvironment("AZURE_TOKEN_CREDENTIALS", "AzureCliCredential");
    }

    // Workplace context: Rehearsal (default) unless overridden — set WorkIq:Mode=Live to light up
    // the real Work IQ MCP integration (workiq CLI stdio, falls back to rehearsal on any failure).
    var workIqMode = builder.Configuration["WorkIq:Mode"];
    if (!string.IsNullOrWhiteSpace(workIqMode))
    {
        web.WithEnvironment("WorkIq__Mode", workIqMode);
    }
}

builder.Build().Run();
