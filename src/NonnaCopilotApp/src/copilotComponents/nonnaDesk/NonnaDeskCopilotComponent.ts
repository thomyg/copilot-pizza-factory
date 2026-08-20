import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { BaseCopilotComponent } from '@microsoft/sp-copilot-component';

import NonnaDeskApp from './components/NonnaDeskApp';
import type { INonnaDeskCopilotComponentProperties } from './NonnaDeskCopilotComponentProperties';
import { backOfficeSnapshot } from './RehearsalBackOffice';

export default class NonnaDeskCopilotComponent extends BaseCopilotComponent<INonnaDeskCopilotComponentProperties> {
  protected render(): void {
    const element: React.ReactElement = React.createElement(NonnaDeskApp, {
      view: this.properties.view ?? 'rota',
      nonnaSays: this.properties.nonnaSays,
      snapshot: backOfficeSnapshot(new Date()),
      theme: this.hostContext.theme,
      displayMode: this.hostContext.displayMode
    });

    ReactDOM.render(element, this.context.domElement);
  }

  protected onTeardown(reason: string | undefined): Promise<void> {
    ReactDOM.unmountComponentAtNode(this.context.domElement);
    return super.onTeardown(reason);
  }
}
