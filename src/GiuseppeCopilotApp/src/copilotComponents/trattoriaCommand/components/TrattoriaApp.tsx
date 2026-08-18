import * as React from 'react';

import { Spinner } from '@fluentui/react-components';
import type { SPCopilotDisplayMode, SPCopilotTheme } from '@microsoft/sp-copilot-component';

import type { ITrattoriaSnapshot, ViewKey } from '../models/trattoria';
import type { ITrattoriaDataService } from '../services/ITrattoriaDataService';
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
}

/** Root: resolves the snapshot once, then renders inline or fullscreen. */
const TrattoriaApp: React.FunctionComponent<ITrattoriaAppProps> = (props) => {
  const [snapshot, setSnapshot] = React.useState<ITrattoriaSnapshot | undefined>(undefined);

  React.useEffect(() => {
    let cancelled = false;
    props.dataService
      .getSnapshot(new Date())
      .then((s) => {
        if (!cancelled) {
          setSnapshot(s);
        }
      })
      .catch(() => {
        /* rehearsal service cannot fail; a live service would surface an error card here */
      });
    return () => {
      cancelled = true;
    };
  }, [props.dataService]);

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
