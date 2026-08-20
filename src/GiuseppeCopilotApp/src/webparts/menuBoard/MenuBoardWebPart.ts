import * as React from 'react';
import * as ReactDOM from 'react-dom';

import { type IPropertyPaneConfiguration, PropertyPaneToggle } from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import MenuBoard from '../../copilotComponents/trattoriaCommand/components/boards/MenuBoard';
import { LiveTrattoriaService } from '../../copilotComponents/trattoriaCommand/services/LiveTrattoriaService';

export interface IMenuBoardWebPartProps {
  darkMode: boolean;
}

/** The canteen play: the house menu with live pantry-derived availability badges. */
export default class MenuBoardWebPart extends BaseClientSideWebPart<IMenuBoardWebPartProps> {
  private readonly _dataService = new LiveTrattoriaService();

  public render(): void {
    ReactDOM.render(
      React.createElement(MenuBoard, {
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
              groupName: 'Menu board',
              groupFields: [PropertyPaneToggle('darkMode', { label: 'Dark mode' })]
            }
          ]
        }
      ]
    };
  }
}
