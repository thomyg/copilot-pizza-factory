import * as React from 'react';

import { Spinner } from '@fluentui/react-components';
import type { SPCopilotDisplayMode, SPCopilotTheme } from '@microsoft/sp-copilot-component';

import type { ITrattoriaSnapshot, ViewKey } from '../models/trattoria';
import type { ITrattoriaDataService } from '../services/ITrattoriaDataService';
import { useSnapshot } from '../services/useSnapshot';
import FullscreenCockpit from './FullscreenCockpit';
import InlineCard from './InlineCard';
import TrattoriaTheme from './TrattoriaTheme';

export interface ITrattoriaAppProps {
  view: ViewKey;
  giuseppeSays?: string;
  dataService: ITrattoriaDataService;
  theme?: SPCopilotTheme;
  displayMode?: SPCopilotDisplayMode;
  availableDisplayModes?: SPCopilotDisplayMode[];
  onRequestFullscreen: () => void;
  /** Seconds between refreshes. 0 disables polling (tests, print, a frozen stage). */
  refreshSeconds?: number;
}

/**
 * Root: reads the snapshot, then keeps reading it.
 *
 * A cockpit that resolves once is a screenshot with extra steps — the whole
 * claim of this demo is that the numbers move on their own, so the panel has to
 * move with them. Refreshes replace the snapshot in place: no spinner after the
 * first paint, because a board that blinks every twenty seconds is worse than a
 * board that is a few seconds stale.
 */
const TrattoriaApp: React.FunctionComponent<ITrattoriaAppProps> = (props) => {
  const snapshot: ITrattoriaSnapshot | undefined = useSnapshot(props.dataService, props.refreshSeconds);

  const canExpand: boolean =
    (props.availableDisplayModes ?? []).indexOf('fullscreen') >= 0 &&
    props.displayMode !== 'fullscreen';

  return (
    <TrattoriaTheme theme={props.theme}>
      {!snapshot ? (
        <Spinner size="tiny" label="Warming the oven…" style={{ padding: 16 }} />
      ) : props.displayMode === 'fullscreen' ? (
        <FullscreenCockpit view={props.view} giuseppeSays={props.giuseppeSays} snapshot={snapshot} />
      ) : (
        <InlineCard
          view={props.view}
          giuseppeSays={props.giuseppeSays}
          snapshot={snapshot}
          canExpand={canExpand}
          onExpand={props.onRequestFullscreen}
        />
      )}
    </TrattoriaTheme>
  );
};

export default TrattoriaApp;
