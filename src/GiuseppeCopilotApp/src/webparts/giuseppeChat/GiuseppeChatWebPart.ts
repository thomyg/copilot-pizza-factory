import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { type IPropertyPaneConfiguration, PropertyPaneTextField, PropertyPaneToggle } from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import { FactoryHttp } from '../../factoryApi';
import ChatBoard from '../../copilotComponents/trattoriaCommand/components/boards/ChatBoard';

export interface IGiuseppeChatWebPartProps {
  apiUrl: string;
  darkMode: boolean;
}

/**
 * The pro-code route: real Giuseppe on a SharePoint page. The web part POSTs to the
 * factory's guarded chat API (PizzaFactory.Web /api/giuseppe/chat) — the same
 * tool-calling agent, wearing the staff belt, CORS-restricted to this tenant.
 * Deployed for real, the API sits behind Microsoft Entra like every other surface;
 * swap fetch for AadHttpClient and the wiring stays identical.
 */
export default class GiuseppeChatWebPart extends BaseClientSideWebPart<IGiuseppeChatWebPartProps> {
  public render(): void {
    ReactDOM.render(
      React.createElement(ChatBoard, {
        apiUrl: this.properties.apiUrl,
        http: new FactoryHttp(this.context.aadHttpClientFactory),
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
          header: { description: 'Point this at the running factory — the chat talks to the real GiuseppeAgent.' },
          groups: [
            {
              groupName: 'Ask Giuseppe',
              groupFields: [
                PropertyPaneTextField('apiUrl', { label: 'Chat API URL (…/api/giuseppe/chat)' }),
                PropertyPaneToggle('darkMode', { label: 'Dark mode' })
              ]
            }
          ]
        }
      ]
    };
  }
}
