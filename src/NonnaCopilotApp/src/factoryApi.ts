/**
 * Where the running Copilot Pizza Factory lives.
 *
 * Nonna never opens the store backend herself — she reads TrattoriaSoft ERP 3000
 * through the factory's service hatch (/api/nonna/*). One constant, so re-pointing
 * this package at another deployment is a one-line change plus a rebuild.
 */
export const FACTORY_API_BASE = 'https://trattoria-copilotpizzafactory.azurewebsites.net';

/** How long her desk waits for the factory before falling back to rehearsal data. */
export const FACTORY_TIMEOUT_MS = 6000;

/**
 * fetch with a hard timeout. A demo must never hang on a cold backend — it
 * degrades to rehearsal data instead, which is the whole point of the seam.
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
