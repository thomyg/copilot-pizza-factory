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
var giuseppeEndpoint = builder.Configuration["Giuseppe:Endpoint"];
var giuseppeDeployment = builder.Configuration["Giuseppe:Deployment"];
if (!string.IsNullOrWhiteSpace(giuseppeEndpoint) && !string.IsNullOrWhiteSpace(giuseppeDeployment))
{
    web.WithEnvironment("Giuseppe__Endpoint", giuseppeEndpoint)
       .WithEnvironment("Giuseppe__Deployment", giuseppeDeployment);
}

builder.Build().Run();
