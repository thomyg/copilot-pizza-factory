import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { BaseCopilotComponent } from '@microsoft/sp-copilot-component';

import NonnaDeskApp from './components/NonnaDeskApp';
import type { INonnaDeskCopilotComponentProperties } from './NonnaDeskCopilotComponentProperties';
import type { IBackOfficeSnapshot } from './RehearsalBackOffice';
import { backOfficeSnapshot } from './RehearsalBackOffice';
import { liveBackOfficeSnapshot } from './LiveBackOffice';
import { FactoryHttp } from '../../factoryApi';

export default class NonnaDeskCopilotComponent extends BaseCopilotComponent<INonnaDeskCopilotComponentProperties> {
  /**
   * Rehearsal first so the desk paints instantly, then the real ERP replaces it
   * the moment the factory answers. Copilot never shows an empty ledger.
   */
  private _snapshot: IBackOfficeSnapshot = backOfficeSnapshot(new Date());
  private _live: boolean = false;
  private _torndown: boolean = false;

  protected render(): void {
    if (!this._live) {
      this._loadLive();
    }

    const element: React.ReactElement = React.createElement(NonnaDeskApp, {
      view: this.properties.view ?? 'rota',
      nonnaSays: this.properties.nonnaSays,
      snapshot: this._snapshot,
      theme: this.hostContext.theme,
      displayMode: this.hostContext.displayMode
    });

    ReactDOM.render(element, this.context.domElement);
  }

  protected onTeardown(reason: string | undefined): Promise<void> {
    this._torndown = true;
    ReactDOM.unmountComponentAtNode(this.context.domElement);
    return super.onTeardown(reason);
  }

  /** Fire-and-forget: liveBackOfficeSnapshot already falls back on its own. */
  private _loadLive(): void {
    this._live = true;
    liveBackOfficeSnapshot(new Date(), new FactoryHttp(this.context.aadHttpClientFactory))
      .then((snapshot: IBackOfficeSnapshot) => {
        if (this._torndown) {
          return;
        }
        this._snapshot = snapshot;
        this.render();
      })
      .catch(() => {
        /* Already handled inside liveBackOfficeSnapshot; keep the rehearsal desk. */
      });
  }
}
