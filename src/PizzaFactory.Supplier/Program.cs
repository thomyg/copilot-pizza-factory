using A2A;
using A2A.AspNetCore;

// Giuseppe's Pineapple Supplier — an EXTERNAL agent (separate service) that the factory's
// Procurement reaches over A2A when stock runs low. Publishes a real A2A agent card for
// discovery; the replenish exchange is a simple task endpoint (full A2A task/message transport
// is a follow-up while the A2A .NET server API is in preview).

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

var agentCard = new AgentCard
{
    Name = "Giuseppe's Pineapple Supplier",
    Description = "External supplier agent. Replenishes pizza ingredients on request and returns a confirmation + ETA.",
    Version = "1.0.0",
    DefaultInputModes = ["text"],
    DefaultOutputModes = ["text"],
    Capabilities = new AgentCapabilities { Streaming = false },
    Skills =
    [
        new AgentSkill
        {
            Id = "replenish",
            Name = "Replenish",
            Description = "Restock an ingredient by N grams; returns confirmation and an ETA in seconds.",
            Tags = ["supply", "restock", "ingredients"],
        },
    ],
};

// A2A discovery: serves the well-known agent card.
app.MapWellKnownAgentCard(agentCard, "");

// The replenish "task": the supplier always confirms; ETA grows a little with quantity (demo).
app.MapPost("/replenish", (ReplenishRequest request) =>
{
    var etaSeconds = 5 + (request.Grams / 1000);
    return Results.Ok(new ReplenishQuote(agentCard.Name, request.Ingredient, request.Grams, etaSeconds, Confirmed: true));
});

app.Run();

internal sealed record ReplenishRequest(string Ingredient, int Grams);
internal sealed record ReplenishQuote(string Supplier, string Ingredient, int Grams, int EtaSeconds, bool Confirmed);

// Exposed so WebApplicationFactory<Program> can host the supplier in integration tests.
public partial class Program;
