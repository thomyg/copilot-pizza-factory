import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { type IPropertyPaneConfiguration, PropertyPaneTextField, PropertyPaneToggle } from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import NonnaDeskBoard from '../../copilotComponents/trattoriaCommand/components/boards/NonnaDeskBoard';

export interface INonnaDeskWebPartProps {
  apiBase: string;
  darkMode: boolean;
}

/**
 * Nonna's Desk, live on a SharePoint page: pending purchase orders with real
 * approve/reject buttons, the rota with open seats, the invoices. The approve button
 * refills the factory's actual pantry two seconds later — human-in-the-loop, feelable.
 */
export default class NonnaDeskWebPart extends BaseClientSideWebPart<INonnaDeskWebPartProps> {
  public render(): void {
    ReactDOM.render(
      React.createElement(NonnaDeskBoard, {
        apiBase: this.properties.apiBase,
        theme: this.properties.darkMode ? 'dark' : 'light'
      }),
      this.domElement
    );
  }

  protected onDispose(): void {
    ReactDOM.unmountComponentAtNode(this.domElement);
  }

  protected getPropertyPaneConfiguration(): IPropertyPaneConfiguration {
    return {
      pages: [
        {
          groups: [
            {
              groupName: "Nonna's Desk",
              groupFields: [
                PropertyPaneTextField('apiBase', { label: 'API base (…/api/nonna)' }),
                PropertyPaneToggle('darkMode', { label: 'Dark mode' })
              ]
            }
          ]
        }
      ]
    };
  }
}
