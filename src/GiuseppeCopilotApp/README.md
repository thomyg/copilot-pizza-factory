# Giuseppe — Trattoria Command (SharePoint Copilot App)

> ⚠️ **PREVIEW TECH.** Built on **SPFx 1.24.0-beta.2 Copilot components** — Microsoft's
> SharePoint Copilot Apps are in public preview and explicitly not for production.
> This project is isolated from the .NET solution (own toolchain, own lockfile) and
> labeled preview everywhere, per house rules.

Giuseppe's **back-of-house manager cockpit inside Microsoft 365 Copilot**. Ask Copilot
"how is tonight looking at the trattoria?" and instead of a wall of text it renders a
living UI: a compact inline card that expands into a fullscreen war room — table
pressure, the kitchen line, revenue pace, the crystal ball, and the reservation book.

## One brain, three rooms

The Copilot Pizza Factory pattern: **personas are voice, tool belts are authorization,
and the surface picks the hat.**

| Surface | Who's there | Giuseppe's hat |
| --- | --- | --- |
| Storefront (public web) | Customers | Concierge — menu, orders, reservations |
| The Window / War Room (internal web) | Staff | Front desk + manager |
| **Microsoft 365 Copilot (this app)** | **Licensed staff** | **Manager — reports, forecasts, the book** |

M365 Copilot is an authenticated workplace surface — nobody chatting here is a customer.
So this app ships the manager tool belt only, and there was no reason to fork a second
bot: the room already does the authorization.

## What's inside

- **One Copilot component** (`TrattoriaCommandCopilotComponent`, extends
  `BaseCopilotComponent`) with **one tool** whose zod schema gives the model two levers:
  - `view`: `tonight` | `report` | `forecast` | `preorders` — which board leads
  - `giuseppeSays`: a short in-character remark Copilot writes into the cockpit as
    Giuseppe's handwritten note (parameterized initial rendering, the SPFx 1.24 wow)
- **Inline + fullscreen display modes**, host light/dark theme aware, FORNO ROSSO
  accents (tomato `#C93A21`, gold, basil) over Fluent UI v9
- **A full declarative agent** (`copilot/`) with Giuseppe's manager persona,
  conversation starters, and the prank radar
- **Rehearsal data service** (`RehearsalTrattoriaService`) mirroring the live factory
  simulation: same menu and prices as `PriceList`, the Bookkeeper's seeded 7-day
  history (weekends 95–130 orders), Procurement's 300 g/150 g stock thresholds, and the
  crystal-ball rules from `Bookkeeper.ForecastAsync` — deterministic per day+hour, so
  demos are alive but stable. Swap seam: `ITrattoriaDataService`.

## Build & test

```bash
nvm use 22            # requires Node >=22.14 <23
npm ci
npm test              # heft build + 9 jest tests
npm run build         # production build + sharepoint/solution/giuseppe-copilot-app.sppkg
```

The build also emits `teams/giuseppe--trattoria-command.zip` — the merged declarative
agent package (the heft copilot-agent plugin combines `copilot/` with the component's
tools automatically).

## Deploy (tenant admin, ~5 minutes)

1. **App catalog**: upload `sharepoint/solution/giuseppe-copilot-app.sppkg` to the
   tenant SharePoint app catalog and check **Enable this app and add it to all sites**.
2. When prompted, select **Add to Teams** — this also syncs the declarative agent to
   the tenant agent catalog (that button does both; no separate publishing step).
3. Open **M365 Copilot** (or Copilot Chat) → agent list → **Giuseppe — Trattoria
   Command** → try "How is tonight looking at the trattoria?"
4. Changed `declarativeAgent.json`? **Bump its `version`** or Copilot keeps the old one.

### Inner dev loop (Copilot Workbench)

```bash
npm start             # heft start — serves the component from localhost:4321
```

Then browse `https://<tenant>.sharepoint.com/_layouts/15/copilotworkbench.aspx` — the
Workbench loads the local debug component against a real Copilot surface.

## Relationship to the wiqd agent

`src/GiuseppeCopilotAgent/` (the wiqd-scaffolded declarative agent) remains the
Friday-retro **catering experiment** with its API connector story. This app is the
**manager surface** and, being a full declarative agent with UI, is the one to demo in
Copilot going forward.
