/**
 * Where the running Copilot Pizza Factory lives.
 *
 * Every surface in this package — the Copilot component, the web parts, the
 * Viva card — reads the same factory. One constant, so re-pointing the whole
 * package at another deployment is a one-line change plus a rebuild.
 *
 * The backend allows this SharePoint tenant's origin explicitly
 * (SharePointChat:AllowedOrigins), so browser fetches are same-policy-safe.
 */
export const FACTORY_API_BASE = 'https://trattoria-copilotpizzafactory.azurewebsites.net';

/** How long a cockpit waits for the factory before falling back to rehearsal data. */
export const FACTORY_TIMEOUT_MS = 6000;

/**
 * fetch with a hard timeout. The demo must never hang on a cold backend —
 * it degrades to rehearsal data instead, which is the whole point of the seam.
 */
export async function fetchWithTimeout(url: string, timeoutMs: number = FACTORY_TIMEOUT_MS): Promise<Response> {
  const controller: AbortController = new AbortController();
  const timer: ReturnType<typeof setTimeout> = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { signal: controller.signal, headers: { accept: 'application/json' } });
  } finally {
    clearTimeout(timer);
  }
}
