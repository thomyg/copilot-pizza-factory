import * as React from 'react';

import type { ITrattoriaSnapshot } from '../models/trattoria';
import type { ITrattoriaDataService } from './ITrattoriaDataService';

/** How often a surface re-reads the factory when nobody says otherwise. */
export const DEFAULT_REFRESH_SECONDS = 20;

/**
 * Reads the factory, then keeps reading it.
 *
 * A panel that resolves once is a screenshot with extra steps — the claim this
 * demo makes is that the numbers move on their own, so every surface has to move
 * with them. Refreshes replace the snapshot in place: `undefined` only ever means
 * "nothing yet", never "reloading", so a board someone is presenting from does not
 * blink or blank every twenty seconds.
 *
 * Pass `refreshSeconds: 0` to read once and stop (tests, print, a frozen stage).
 */
export function useSnapshot(
  dataService: ITrattoriaDataService,
  refreshSeconds: number = DEFAULT_REFRESH_SECONDS
): ITrattoriaSnapshot | undefined {
  const [snapshot, setSnapshot] = React.useState<ITrattoriaSnapshot | undefined>(undefined);

  React.useEffect(() => {
    let cancelled: boolean = false;

    const read = (): void => {
      dataService
        .getSnapshot(new Date())
        .then((s: ITrattoriaSnapshot) => {
          if (!cancelled) {
            setSnapshot(s);
          }
        })
        .catch(() => {
          // The live service already falls back to rehearsal data on its own, so a
          // rejection here is unforeseen. Keep the last good snapshot rather than
          // blanking a board mid-presentation.
        });
    };

    read();

    if (refreshSeconds <= 0) {
      return () => {
        cancelled = true;
      };
    }

    const timer: number = window.setInterval(read, refreshSeconds * 1000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [dataService, refreshSeconds]);

  return snapshot;
}
