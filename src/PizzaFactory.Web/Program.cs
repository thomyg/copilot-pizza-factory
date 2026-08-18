using PizzaFactory.Factory;
using PizzaFactory.FrontOfHouse;
using PizzaFactory.Giuseppe;
using PizzaFactory.Infrastructure;
using PizzaFactory.Safety;
using PizzaFactory.Trattoria;
using PizzaFactory.Web;
using PizzaFactory.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Store: real Cosmos when configured (key-less), else in-memory.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Cosmos:Endpoint"]))
{
    builder.Services.AddCosmosPizzaFactoryStore(builder.Configuration);
}
else
{
    builder.Services.AddInMemoryPizzaFactoryStore();
}

// The Window hosts the running factory: content guard, public intake, the floor, and the live feed.
builder.Services.AddHeuristicContentGuard();
builder.Services.AddFrontOfHouse();
builder.Services.AddPizzaFactoryFloor();

// The dining room: 17 tables, online orders, pre-orders. Starts closed — the Play button opens it.
builder.Services.AddTrattoria();

// The front desk hands Giuseppe the reservations book + dining room status as chat tools.
builder.Services.AddSingleton<PizzaFactory.Giuseppe.Tools.IGiuseppeToolSource, FrontDeskToolSource>();

// The storefront concierge: SAME agent machinery, customer hat — composed with the customer
// tool belt ONLY (menu, order, reserve, status). It cannot leak the business report or touch
// the factory because those tools are not in its hands. Personas are voice; tool belts are
// authorization.
builder.Services.AddSingleton<StorefrontToolSource>();
builder.Services.AddSingleton(sp =>
{
    var chat = sp.GetService<Microsoft.Extensions.AI.IChatClient>();
    return new StorefrontConcierge(chat is null ? null : new GiuseppeAgent(
        chat,
        sp.GetRequiredService<IContentGuard>(),
        [sp.GetRequiredService<StorefrontToolSource>()],
        logger: sp.GetService<ILogger<GiuseppeAgent>>(),
        personaOverride: StorefrontConcierge.Persona(sp.GetRequiredService<TimeProvider>())));
});
builder.Services.AddSingleton<FactorySnapshotProvider>();

// The Engine Room's steering levers (sabotage, rush hour, restock) — same repos as the floor.
builder.Services.AddSingleton<DemoDirector>();

// Trust & Safety feed for the Window (Bouncer counter + escalation ticker).
builder.Services.AddSingleton<WindowEventLog>();
builder.Services.AddSingleton<WindowEscalationSink>();
builder.Services.AddSingleton<LoggingEscalationSink>();

// Self-healing supply chain when the external Supplier agent is configured (A2A, key-less).
var supplierEndpoint = builder.Configuration["Supplier:Endpoint"];
if (!string.IsNullOrWhiteSpace(supplierEndpoint))
{
    builder.Services.AddSupplierGateway(new Uri(supplierEndpoint));
    builder.Services.AddSingleton<SupplierEscalationSink>();
}

// Compose the escalation sink: log + Window ticker (+ supplier self-heal when configured).
builder.Services.AddSingleton<IEscalationSink>(sp =>
{
    var sinks = new List<IEscalationSink>
    {
        sp.GetRequiredService<LoggingEscalationSink>(),
        sp.GetRequiredService<WindowEscalationSink>(),
    };
    if (sp.GetService<SupplierEscalationSink>() is { } supplierSink)
    {
        sinks.Add(supplierSink);
    }

    return new CompositeEscalationSink(sinks);
});

// Giuseppe (the AI concierge) when an Azure OpenAI deployment is configured (key-less).
// Config-driven: Giuseppe:FactoryMcpUrl adds ordering/stock tools, WorkIq:Mode selects
// workplace context (Rehearsal default; Live = Work IQ MCP with rehearsal fallback).
builder.Services.AddGiuseppe(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.MapDefaultEndpoints();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
