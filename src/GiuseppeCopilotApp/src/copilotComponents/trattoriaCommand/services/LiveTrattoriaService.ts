import type { ITrattoriaSnapshot } from '../models/trattoria';
import type { ITrattoriaDataService } from './ITrattoriaDataService';
import { RehearsalTrattoriaService } from './RehearsalTrattoriaService';
import { FACTORY_API_BASE, type FactoryHttp } from '../../../factoryApi';

/**
 * The live route: reads the REAL running factory over its snapshot API.
 *
 * `/api/trattoria/snapshot` is shaped as ITrattoriaSnapshot on the server, so
 * there is no mapping layer here — only validation and a fallback. The call
 * carries the signed-in user's token (the factory sits behind Entra), and if
 * the factory is asleep, unreachable, or answers something unexpected, the
 * cockpit quietly serves rehearsal data instead: a demo on stage degrades, it
 * never shows a spinner of shame.
 */
export class LiveTrattoriaService implements ITrattoriaDataService {
  private readonly _fallback: ITrattoriaDataService = new RehearsalTrattoriaService();

  public constructor(
    private readonly _http: FactoryHttp,
    private readonly _apiBase: string = FACTORY_API_BASE
  ) {}

  public async getSnapshot(now: Date): Promise<ITrattoriaSnapshot> {
    try {
      const payload: unknown = await this._http.getJson<unknown>(`${this._apiBase}/api/trattoria/snapshot`);
      return isSnapshot(payload) ? normalise(payload) : await this._fallback.getSnapshot(now);
    } catch {
      return this._fallback.getSnapshot(now);
    }
  }
}

/** Structural check — enough to be sure we are not rendering someone else's JSON. */
function isSnapshot(value: unknown): value is ITrattoriaSnapshot {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate: Partial<ITrattoriaSnapshot> = value as Partial<ITrattoriaSnapshot>;
  return (
    typeof candidate.tonight === 'object' &&
    candidate.tonight !== null &&
    typeof candidate.report === 'object' &&
    candidate.report !== null &&
    Array.isArray(candidate.risks) &&
    Array.isArray(candidate.preOrders)
  );
}

/**
 * JSON has no `undefined`. The UI distinguishes "no reviews yet" (undefined)
 * from "rated zero", so null has to become undefined on the way in.
 */
function normalise(snapshot: ITrattoriaSnapshot): ITrattoriaSnapshot {
  return {
    ...snapshot,
    tonight: { ...snapshot.tonight, averageStars: snapshot.tonight.averageStars ?? undefined },
    report: { ...snapshot.report, averageStars: snapshot.report.averageStars ?? undefined }
  };
}
