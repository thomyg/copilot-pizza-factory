using PizzaFactory.Infrastructure;

// Pizza Factory MCP server — exposes Orders + Inventory tools over Streamable HTTP.
// Any MCP client (M365 Copilot, Copilot Studio, an Agent Framework agent, or a dev tool)
// can drive the factory through these tools.

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Use real Cosmos when an endpoint is configured (key-less, managed identity); otherwise in-memory.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Cosmos:Endpoint"]))
{
    builder.Services.AddCosmosPizzaFactoryStore(builder.Configuration);
}
else
{
    builder.Services.AddInMemoryPizzaFactoryStore();
}

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();

// Exposed so WebApplicationFactory<Program> can host the server in integration tests.
public partial class Program;
