import type { IBackOfficeSnapshot } from './RehearsalBackOffice';
import { backOfficeSnapshot } from './RehearsalBackOffice';
import { FACTORY_API_BASE, type FactoryHttp } from '../../factoryApi';

/**
 * The live route to TrattoriaSoft ERP 3000.
 *
 * `/api/nonna/desk` is shaped as IBackOfficeSnapshot on the server, so there is
 * no mapping layer here — only validation and a fallback. If the factory is
 * unreachable, Nonna serves her rehearsal ledger instead: the demo degrades,
 * it never breaks. What she shows is always a real snapshot of *something*
 * consistent — never a half-loaded desk.
 */
export async function liveBackOfficeSnapshot(
  now: Date,
  http: FactoryHttp,
  apiBase: string = FACTORY_API_BASE
): Promise<IBackOfficeSnapshot> {
  try {
    const payload: unknown = await http.getJson<unknown>(`${apiBase}/api/nonna/desk`);
    return isSnapshot(payload) ? payload : backOfficeSnapshot(now);
  } catch {
    return backOfficeSnapshot(now);
  }
}

/** Structural check — enough to be sure we are not rendering someone else's JSON. */
function isSnapshot(value: unknown): value is IBackOfficeSnapshot {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate: Partial<IBackOfficeSnapshot> = value as Partial<IBackOfficeSnapshot>;
  return (
    Array.isArray(candidate.rota) &&
    Array.isArray(candidate.orders) &&
    Array.isArray(candidate.invoices) &&
    typeof candidate.invoiceTotal === 'number'
  );
}
