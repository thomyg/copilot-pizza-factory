// Pizza Factory API — placeholder service for the skeleton.
// Wired with Aspire ServiceDefaults (OpenTelemetry, health checks, resilience,
// service discovery). Real factory endpoints / MCP wiring arrive in later phases.

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

var app = builder.Build();

// /health and /alive (development only) from ServiceDefaults.
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "🍕 Pizza Factory API — ovens preheating. (skeleton)")
   .WithName("Root");

app.Run();
