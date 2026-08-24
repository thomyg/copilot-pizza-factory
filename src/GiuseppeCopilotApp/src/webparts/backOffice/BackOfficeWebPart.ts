import * as React from 'react';
import * as ReactDOM from 'react-dom';

import {
  type IPropertyPaneConfiguration,
  PropertyPaneChoiceGroup,
  PropertyPaneSlider
} from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import BackOffice from './components/BackOffice';
import { FactoryHttp } from '../../factoryApi';

export interface IBackOfficeWebPartProps {
  /** 'trattoria' or 'enterprise' — the same decisions, told to a different room. */
  vocabulary: string;
  refreshSeconds: number;
}

/**
 * The serious half of the demo on a SharePoint page.
 *
 * Everything else here shows a system running; this shows a system waiting for a person, which
 * is the harder and more interesting claim. Approving an absence really moves the roster, and a
 * requisition the budget refuses really cannot be signed through.
 */
export default class BackOfficeWebPart extends BaseClientSideWebPart<IBackOfficeWebPartProps> {
  private _http: FactoryHttp | undefined;

  public render(): void {
    if (!this._http) {
      this._http = new FactoryHttp(this.context.aadHttpClientFactory);
    }

    ReactDOM.render(
      React.createElement(BackOffice, {
        http: this._http,
        vocabulary: this.properties.vocabulary === 'enterprise' ? 'enterprise' : 'trattoria',
        refreshSeconds: this.properties.refreshSeconds ?? 8
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
              groupName: 'Back office',
              groupFields: [
                PropertyPaneChoiceGroup('vocabulary', {
                  label: 'Vocabulary',
                  options: [
                    { key: 'trattoria', text: 'Trattoria — the story' },
                    { key: 'enterprise', text: 'Enterprise — the process' }
                  ]
                }),
                PropertyPaneSlider('refreshSeconds', {
                  label: 'Refresh (seconds)',
                  min: 0,
                  max: 60,
                  step: 2
                })
              ]
            }
          ]
        }
      ]
    };
  }
}
