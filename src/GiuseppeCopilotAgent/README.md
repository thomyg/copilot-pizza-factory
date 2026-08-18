# Blank App

This is a blank Microsoft 365 app project created with Microsoft 365 Agents Toolkit.

## Get Started

This project contains the minimal structure for a Microsoft 365 app:

| File/Folder | Contents |
| - | - |
| `appPackage/manifest.json` | Teams app manifest — defines your app's metadata, icons, and capabilities. |
| `appPackage/color.png` | Color icon for your app (192x192). |
| `appPackage/outline.png` | Outline icon for your app (32x32). |
| `m365agents.yml` | Main project file — defines lifecycle tasks like provision and publish. |
| `m365agents.local.yml` | Overrides for local development. |

## Build Your App

This blank project is a starting point. Add capabilities by editing `manifest.json`:

- **Add a Bot** — define a `bots` section in the manifest
- **Add a Tab** — define a `staticTabs` or `configurableTabs` section
- **Add a Message Extension** — define a `composeExtensions` section
- **Add a Declarative Agent** — define a `copilotAgents.declarativeAgents` section

## Provision and Preview

1. Press `F5` or run `Provision` from the command palette to register your app
2. Launch `Preview in Copilot (Edge)` or `Preview in Copilot (Chrome)` to open your app in Microsoft 365 Copilot

## Learn More

- [Microsoft 365 Agents Toolkit documentation](https://learn.microsoft.com/microsoftteams/platform/toolkit/teams-toolkit-fundamentals)
- [Teams app manifest reference](https://learn.microsoft.com/microsoftteams/platform/resources/schema/manifest-schema)

## Connector auth: Entra SSO to the factory MCP

The `pizza-factory` connector calls the Entra-protected MCP on Azure Functions
(`https://func-mcp-copilotpizzafactory.azurewebsites.net/mcp`, Easy Auth app
`f702028e-db78-444c-ab17-fa9c9b39726a`). Without auth config, every tool call is a 401.
The manifest already declares `authorization: OAuthPluginVault` with
`${{MCP_AUTH_CONFIG_ID}}`; the server already answers 401s with the RFC 9728
`resource_metadata` challenge and serves PRM at
`/.well-known/oauth-protected-resource/mcp`. What remains is one-time tenant setup
(as a tenant-admin account of the demo tenant — the guest az login cannot do
directory writes):

1. **Teams Dev Portal** (https://dev.teams.microsoft.com/tools) → **Tools →
   Microsoft Entra SSO client ID registration → New client registration**:
   - Registration name: `pizza-factory-mcp-sso`
   - Base URL: `https://func-mcp-copilotpizzafactory.azurewebsites.net/mcp`
   - Restrict usage by org: your org · Restrict usage by app: Any Teams app
   - Client ID: `f702028e-db78-444c-ab17-fa9c9b39726a`
   - Scope: `api://f702028e-db78-444c-ab17-fa9c9b39726a/user_impersonation`

   Save → copy the **auth config ID** (a.k.a. Entra SSO registration ID) and the
   generated **Application ID URI**.
2. **Entra admin center** (https://entra.microsoft.com) → App registrations →
   the MCP app (`f702028e…`):
   - Manifest editor: `identifierUris` += the Application ID URI from step 1
     (keep the existing `api://f702028e…` — the UI shows only the first, that's fine).
   - Authentication → Web platform → Redirect URIs +=
     `https://teams.microsoft.com/api/platform/v1.0/oAuthConsentRedirect`
   - Expose an API → **Add a client application**:
     `ab3be6b7-f5df-413d-ac2d-abf1e3fd9c0b` (Microsoft Enterprise token store),
     authorized for the `user_impersonation` scope.
3. Paste the **auth config ID** into `env/.env.local` (`MCP_AUTH_CONFIG_ID=…`), and
   add the new Application ID URI to the Function App's Easy Auth
   `allowedAudiences` (ARM: `Microsoft.Web/sites/config/authsettingsV2` on
   `func-mcp-copilotpizzafactory`).
4. Re-provision: `wiqd plugin provision` (or `atk provision`) — then ask
   **Giuseppe Catering** in Copilot for the menu; the first tool call shows a
   sign-in prompt once, and `list_pizzas` should return the six pizzas.
