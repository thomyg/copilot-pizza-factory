import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { BaseCopilotComponent } from '@microsoft/sp-copilot-component';

import TrattoriaApp from './components/TrattoriaApp';
import type { ITrattoriaCommandCopilotComponentProperties } from './TrattoriaCommandCopilotComponentProperties';
import { RehearsalTrattoriaService } from './services/RehearsalTrattoriaService';

export default class TrattoriaCommandCopilotComponent extends BaseCopilotComponent<ITrattoriaCommandCopilotComponentProperties> {
  // The seam: swap for a live factory-API service without touching the UI.
  private readonly _dataService = new RehearsalTrattoriaService();

  protected render(): void {
    const element: React.ReactElement = React.createElement(TrattoriaApp, {
      view: this.properties.view ?? 'tonight',
      giuseppeSays: this.properties.giuseppeSays,
      dataService: this._dataService,
      theme: this.hostContext.theme,
      displayMode: this.hostContext.displayMode,
      availableDisplayModes: this.hostContext.availableDisplayModes,
      onRequestFullscreen: this._handleRequestFullscreen
    });

    ReactDOM.render(element, this.context.domElement);
  }

  protected onTeardown(reason: string | undefined): Promise<void> {
    ReactDOM.unmountComponentAtNode(this.context.domElement);
    return super.onTeardown(reason);
  }

  private _handleRequestFullscreen = (): void => {
    this.requestDisplayModeAsync('fullscreen').catch(() => {
      /* Host rejected or is unavailable; host context stays unchanged. */
    });
  };
}
