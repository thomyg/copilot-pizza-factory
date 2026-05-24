# 🍕 Copilot Pizza Factory

An **AI-first demo** of a pizza factory that runs itself — a "perpetuum mobile" where autonomous
agents take orders, rest dough, bake pizzas, watch stock, and reorder from an external supplier when
they run low. Humans drop in only when judgment is needed.

It's built to work on **two flight levels at once**: a *business* story (a self-running operation that
heals its own supply chain and pulls people in only when it matters) and a *technical* story (MCP tool
servers, an Agent-to-Agent supplier, Responsible-AI guardrails, a chat agent, Cosmos DB, and .NET
Aspire orchestration — all key-less). Smart, a little playful, genuinely runnable.

Built on **.NET 10**.

## Run it locally (zero Azure required)

The whole factory runs in-memory with no cloud dependencies — perfect for a first look:

```bash
# The Aspire "control tower" (dashboard + all services)
dotnet run --project src/PizzaFactory.AppHost

# …or just the Window dashboard on its own
dotnet run --project src/PizzaFactory.Web
```

Open the **Window**: order a pizza, watch it cross the stations live, chat with Giuseppe (when a model
is configured), and see the "Bouncer" block bad input. With no Azure configured it falls back
gracefully — in-memory store, Giuseppe "off the clock", no external supplier.

Run the tests:

```bash
dotnet test src/PizzaFactory.sln
```

## What's inside

| Project | What it shows |
|---|---|
| `PizzaFactory.Domain` | The pizza domain — recipes, ingredients, immutable `Order`/`Pizza`/`Stock`/`Dough` + state machines. Persistence-agnostic. |
| `PizzaFactory.Infrastructure` | Repositories: in-memory **and** Cosmos DB (key-less, `DefaultAzureCredential`). |
| `PizzaFactory.Factory` | The **perpetuum mobile** — Dough Master / Pizzaiolo / Procurement background loops, `CrisisWatch`, and the self-healing supplier path. |
| `PizzaFactory.Mcp` | A **Model Context Protocol** server (Streamable HTTP) exposing 9 tools over the factory (orders, inventory, recipes, live telemetry). |
| `PizzaFactory.Safety` | **Responsible-AI guardrail** — offline heuristic + Azure AI Content Safety & Prompt Shields, behind one interface. |
| `PizzaFactory.FrontOfHouse` | Public guest intake — auto pseudonyms (zero-PII), moderation, an ordering **kill-switch**. |
| `PizzaFactory.Giuseppe` | The **AI concierge** — a guarded chat agent on Azure OpenAI. |
| `PizzaFactory.Supplier` | An **external Agent-to-Agent (A2A)** supplier — publishes an agent card and fulfils restock requests. |
| `PizzaFactory.Web` | The **"Window"** — a Blazor dashboard that hosts the running factory and shows it live (board, order form, Giuseppe chat, Trust & Safety feed). |
| `PizzaFactory.AppHost` / `ServiceDefaults` | **.NET Aspire** orchestration + OpenTelemetry. |

## The tech, in one breath

.NET 10 · .NET Aspire · Blazor (interactive Server) · Azure Cosmos DB · Model Context Protocol (MCP) ·
Agent-to-Agent (A2A) · Azure AI Content Safety + Prompt Shields · Azure OpenAI · **key-less throughout**
(managed identity / `az login`, no secrets in source) · ~87 tests.

## Optional: run on Azure (key-less)

Everything cloud-bound is config-driven and authenticated with managed identity — **no keys**. Set any
of these (e.g. via environment or the Aspire AppHost) to light up the real services; leave them unset
to stay fully local:

| Setting | Enables |
|---|---|
| `Cosmos:Endpoint` | Persist to Azure Cosmos DB instead of in-memory |
| `ContentSafety:Endpoint` | Cloud moderation + Prompt Shields (vs. the offline heuristic) |
| `Giuseppe:Endpoint` + `Giuseppe:Deployment` | Giuseppe on an Azure OpenAI deployment |
| `Supplier:Endpoint` | The external A2A supplier for self-healing restock |

## License

[MIT](LICENSE).
