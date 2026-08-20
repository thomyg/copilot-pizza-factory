# Nonna — Back Office (SharePoint Copilot App)

> ⚠️ **PREVIEW TECH.** SPFx 1.24.0-beta.2 Copilot components — same preview rules as
> Trattoria Command: isolated toolchain, not for production, labeled everywhere.

Nonna's Copilot presence. Ask *"who is working tonight?"*, *"Maria called in sick"*, or
*"which purchase orders need my approval?"* and Copilot renders **her desk**: the rota
with open seats glowing red, the orders waiting for a signature, and the invoice ledger
with its running total. Basil-green FORNO ROSSO, light and dark.

## One brain, one rule

Nonna lives **entirely inside Microsoft 365** — this Copilot agent, the Teams-facing
surfaces, and the SharePoint web parts (which live in `../GiuseppeCopilotApp` alongside
Giuseppe's, sharing the React infra: **Ask Nonna (live)** and **Nonna's Desk (live)**
talk to the real `/api/nonna` endpoints). She never opens the store backend, and her
agent belt holds only the back office: rota, absences, purchase orders, invoices. The
kitchen belongs to Giuseppe.

Why a second package: the SPFx toolchain supports **one declarative agent per `.sppkg`**
(build fails fast on more). Two personas, two packages, two names in the agent list —
which is what the house naming rule wanted anyway.

- **Tool levers**: `view` (`rota` | `approvals` | `invoices`) + `nonnaSays` (her
  handwritten note, written by the model).
- **Rehearsal data** mirrors TrattoriaSoft ERP 3000: same nine-person roster and rota
  rotation rules as `PizzaFactory.BackOffice.StaffBook`, one open seat from today's sick
  call, and the pineapple saga in the ledger (4 kg pending, A2A invoice on file).

## Build, validate, deploy

```bash
nvm use 22 && npm ci
npm run build      # tests + sharepoint/solution/nonna-copilot-app.sppkg
npx @microsoft/m365agentstoolkit-cli validate --package-file teams/nonna--back-office.zip
```

Ignore only the `RemoteMCPServerRuntimeSpec url` error (the `{{TENANT_MCP_URL}}`
placeholder the SharePoint sync stamps). Deploy exactly like Trattoria Command: app
catalog → upload `.sppkg` → **Enable** → **Add to Teams** → agent appears as
**Nonna — Back Office**. Workbench inner loop: `npm start` +
`copilotworkbench.aspx?debugManifestsFile=https://localhost:4321/temp/build/manifests.js`.
