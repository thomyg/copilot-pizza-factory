using PizzaFactory.Factory;
using PizzaFactory.FrontOfHouse;
using PizzaFactory.Giuseppe;
using PizzaFactory.Infrastructure;
using PizzaFactory.Safety;
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
builder.Services.AddSingleton<FactorySnapshotProvider>();

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
var giuseppeEndpoint = builder.Configuration["Giuseppe:Endpoint"];
var giuseppeDeployment = builder.Configuration["Giuseppe:Deployment"];
if (!string.IsNullOrWhiteSpace(giuseppeEndpoint) && !string.IsNullOrWhiteSpace(giuseppeDeployment))
{
    builder.Services.AddGiuseppe(new Uri(giuseppeEndpoint), giuseppeDeployment);
}

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
