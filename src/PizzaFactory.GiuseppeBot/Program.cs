using Microsoft.Agents.Authentication.Msal;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using PizzaFactory.GiuseppeBot;
using PizzaFactory.Giuseppe;
using PizzaFactory.Safety;

// Giuseppe Bot — a Microsoft 365 Agents SDK host that surfaces the GiuseppeAgent over the Bot Framework
// /api/messages protocol, so Teams / Azure Bot Service channels can chat with our pizzaiolo.
// Auth is key-less (user-assigned managed identity) in Azure; disabled locally for the dev loop.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// The Agents SDK turn pipeline: CloudAdapter + AgentApplicationOptions + our routing agent (resolved from DI,
// so GiuseppeAgent is injected when registered below).
builder.AddAgentApplicationOptions();
builder.AddAgent<GiuseppeBotAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Channel/service authentication for the Bot Framework connector (MSAL; managed identity in Azure).
// AddDefaultMsalAuth wires the outbound token provider (how the bot calls the connector).
builder.Services.AddDefaultMsalAuth(builder.Configuration);

// Add AspNet token validation for Azure Bot Service and Entra (config: "TokenValidation").
// This is the canonical quickstart helper (AspNetExtensions.cs): it does correct multi-issuer
// Bot Framework + Entra validation via OpenID metadata — a naive JwtBearer validating only the
// Entra tenant would reject legitimate Teams / connector traffic. When "TokenValidation:Enabled"
// is false (Development), the helper no-ops so the emulator can POST without a token.
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

// Content guard: Azure AI Content Safety when configured, otherwise the offline heuristic guard
// (mirrors PizzaFactory.Web). Giuseppe needs an IContentGuard, so always register one.
if (!string.IsNullOrWhiteSpace(builder.Configuration["ContentSafety:Endpoint"]))
{
    builder.Services.AddAzureContentSafetyGuard(builder.Configuration);
}
else
{
    builder.Services.AddHeuristicContentGuard();
}

// Giuseppe (the AI concierge) when an Azure OpenAI deployment is configured (key-less). When it's not,
// GiuseppeBotAgent's optional GiuseppeAgent stays null and the bot replies "off the clock" — so the
// host still builds, starts, and returns 200 from /api/messages with no Azure dependency.
var giuseppeEndpoint = builder.Configuration["Giuseppe:Endpoint"];
var giuseppeDeployment = builder.Configuration["Giuseppe:Deployment"];
if (!string.IsNullOrWhiteSpace(giuseppeEndpoint) && !string.IsNullOrWhiteSpace(giuseppeDeployment))
{
    builder.Services.AddGiuseppe(new Uri(giuseppeEndpoint), giuseppeDeployment);
}

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapAgentRootEndpoint();
// In Development the quickstart's requireAuth:false lets the emulator POST without a token
// (the AddAgentAspNetAuthentication helper also no-ops when TokenValidation:Enabled is false).
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

app.Run();
