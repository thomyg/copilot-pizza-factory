import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { type IPropertyPaneConfiguration, PropertyPaneTextField, PropertyPaneToggle } from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import ChatBoard from '../../copilotComponents/trattoriaCommand/components/boards/ChatBoard';

export interface INonnaChatWebPartProps {
  apiUrl: string;
  darkMode: boolean;
}

/**
 * Ask Nonna (live): the back office's agent on a SharePoint page. Same chat board as
 * Giuseppe's, different brain behind the URL — Nonna's belt holds the rota and the
 * purchase ledger, and nothing of the kitchen.
 */
export default class NonnaChatWebPart extends BaseClientSideWebPart<INonnaChatWebPartProps> {
  public render(): void {
    ReactDOM.render(
      React.createElement(ChatBoard, {
        apiUrl: this.properties.apiUrl,
        theme: this.properties.darkMode ? 'dark' : 'light',
        title: '🧾 Ask Nonna',
        subtitle: 'the back office — rota, purchase orders, invoices',
        emptyHint: 'Try "Maria is sick tonight — handle it", "What needs my attention?", or "Show me the invoices."',
        placeholder: 'Ask Nonna…',
        busyText: 'Nonna is checking the ledger…',
        failText: 'Nonna is unreachable — is the factory running (and the API URL configured)?'
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
          header: { description: 'Point this at the running factory — the chat talks to the real Nonna.' },
          groups: [
            {
              groupName: 'Ask Nonna',
              groupFields: [
                PropertyPaneTextField('apiUrl', { label: 'Chat API URL (…/api/nonna/chat)' }),
                PropertyPaneToggle('darkMode', { label: 'Dark mode' })
              ]
            }
          ]
        }
      ]
    };
  }
}
