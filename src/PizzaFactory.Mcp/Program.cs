using PizzaFactory.Infrastructure;

// Pizza Factory MCP server — exposes Orders + Inventory tools over Streamable HTTP.
// Any MCP client (M365 Copilot, Copilot Studio, an Agent Framework agent, or a dev tool)
// can drive the factory through these tools.

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// When hosted as an Azure Functions custom handler, Kestrel must listen on the port the Functions
// host forwards to. The mcp-custom-handler profile doesn't reliably export FUNCTIONS_CUSTOMHANDLER_PORT,
// so on Azure (WEBSITE_SITE_NAME present) fall back to the host.json port (8080). Locally (Aspire,
// tests) none of these are set, so the host keeps control of the URL — no effect.
var customHandlerPort = Environment.GetEnvironmentVariable("FUNCTIONS_CUSTOMHANDLER_PORT");
if (string.IsNullOrEmpty(customHandlerPort) &&
    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    customHandlerPort = "8080"; // matches host.json customHandler.port
}
if (!string.IsNullOrEmpty(customHandlerPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{customHandlerPort}");
}

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
    .WithHttpTransport(options => options.Stateless = true)  // serverless-friendly: no per-session state to pin to one instance
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();

// Exposed so WebApplicationFactory<Program> can host the server in integration tests.
public partial class Program;
