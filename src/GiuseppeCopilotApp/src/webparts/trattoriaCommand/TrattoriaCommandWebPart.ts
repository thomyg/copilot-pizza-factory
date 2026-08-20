import * as React from 'react';
import * as ReactDOM from 'react-dom';

import {
  type IPropertyPaneConfiguration,
  PropertyPaneDropdown,
  PropertyPaneTextField,
  PropertyPaneToggle
} from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import TrattoriaApp from '../../copilotComponents/trattoriaCommand/components/TrattoriaApp';
import type { ViewKey } from '../../copilotComponents/trattoriaCommand/models/trattoria';
import { LiveTrattoriaService } from '../../copilotComponents/trattoriaCommand/services/LiveTrattoriaService';

export interface ITrattoriaCommandWebPartProps {
  view: ViewKey;
  giuseppeSays: string;
  darkMode: boolean;
}

/**
 * The same cockpit that renders inside Microsoft 365 Copilot, hosted as a SharePoint
 * web part — one UX investment, second surface. Same React components, same rehearsal
 * data service; only the host class differs.
 */
export default class TrattoriaCommandWebPart extends BaseClientSideWebPart<ITrattoriaCommandWebPartProps> {
  private readonly _dataService = new LiveTrattoriaService();

  public render(): void {
    const element: React.ReactElement = React.createElement(TrattoriaApp, {
      view: this.properties.view ?? 'tonight',
      giuseppeSays: this.properties.giuseppeSays || undefined,
      dataService: this._dataService,
      theme: this.properties.darkMode ? 'dark' : 'light',
      displayMode: 'fullscreen',
      availableDisplayModes: ['fullscreen'],
      onRequestFullscreen: () => undefined
    });

    ReactDOM.render(element, this.domElement);
  }

  protected onDispose(): void {
    ReactDOM.unmountComponentAtNode(this.domElement);
  }

  protected getPropertyPaneConfiguration(): IPropertyPaneConfiguration {
    return {
      pages: [
        {
          header: { description: 'Steer the cockpit — same levers Copilot pulls via the tool schema.' },
          groups: [
            {
              groupName: 'Trattoria Command',
              groupFields: [
                PropertyPaneDropdown('view', {
                  label: 'Spotlight view',
                  options: [
                    { key: 'tonight', text: 'Tonight' },
                    { key: 'report', text: 'Business report' },
                    { key: 'forecast', text: 'Crystal ball' },
                    { key: 'preorders', text: 'Reservation book' }
                  ]
                }),
                PropertyPaneTextField('giuseppeSays', { label: "Giuseppe's note" }),
                PropertyPaneToggle('darkMode', { label: 'Dark mode' })
              ]
            }
          ]
        }
      ]
    };
  }
}
