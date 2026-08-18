import type { ITrattoriaSnapshot } from '../models/trattoria';

/**
 * The seam between the cockpit UI and its data.
 *
 * The shipped implementation is RehearsalTrattoriaService — deterministic,
 * time-aware demo data that mirrors the live Copilot Pizza Factory simulation.
 * A live implementation would call the factory's API (AadHttpClient against the
 * Azure Functions MCP host) and map to the same shapes; the UI never changes.
 */
export interface ITrattoriaDataService {
  getSnapshot(now: Date): Promise<ITrattoriaSnapshot>;
}
