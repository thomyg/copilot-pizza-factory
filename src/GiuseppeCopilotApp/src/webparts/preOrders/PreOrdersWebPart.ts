import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { type IPropertyPaneConfiguration, PropertyPaneToggle } from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import PreOrderBoard from '../../copilotComponents/trattoriaCommand/components/boards/PreOrderBoard';
import { LiveTrattoriaService } from '../../copilotComponents/trattoriaCommand/services/LiveTrattoriaService';

export interface IPreOrdersWebPartProps {
  darkMode: boolean;
}

/** The reservation book on a SharePoint page — book pizzas for the team event without leaving the intranet. */
export default class PreOrdersWebPart extends BaseClientSideWebPart<IPreOrdersWebPartProps> {
  private readonly _dataService = new LiveTrattoriaService();

  public render(): void {
    ReactDOM.render(
      React.createElement(PreOrderBoard, {
        dataService: this._dataService,
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
              groupName: 'Reserve ahead',
              groupFields: [PropertyPaneToggle('darkMode', { label: 'Dark mode' })]
            }
          ]
        }
      ]
    };
  }
}
