import * as React from 'react';
import * as ReactDOM from 'react-dom';

import {
  type IPropertyPaneConfiguration,
  PropertyPaneChoiceGroup,
  PropertyPaneTextField,
  PropertyPaneToggle
} from '@microsoft/sp-property-pane';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import TrattoriaHero, { type IHeroLink } from './components/TrattoriaHero';
import { FACTORY_API_BASE, FactoryHttp } from '../../factoryApi';

export interface ITrattoriaHeroWebPartProps {
  eyebrow: string;
  headline: string;
  lede: string;
  showLinks: boolean;
  /** 'trattoria' or 'enterprise' — the same system, told to a different room. */
  vocabulary: string;
}

/**
 * The page's front door, in the demo's own brand rather than SharePoint's.
 *
 * SharePoint's text web part cannot carry FORNO ROSSO — no display serif, no
 * charred surface, no ember. This web part can, and it earns the space by
 * quoting the running factory: tonight's tables, orders and revenue, refreshed
 * every fifteen seconds from the same snapshot API the cockpit reads.
 */
export default class TrattoriaHeroWebPart extends BaseClientSideWebPart<ITrattoriaHeroWebPartProps> {
  private _http: FactoryHttp | undefined;

  /** The four surfaces of the factory that live outside SharePoint. */
  private static readonly Links: ReadonlyArray<IHeroLink> = [
    { label: 'The Window', hint: 'Live business dashboard', url: `${FACTORY_API_BASE}/` },
    { label: 'Cinema mode', hint: 'The perpetuum mobile, projected', url: `${FACTORY_API_BASE}/cinema` },
    { label: 'The Engine Room', hint: 'Break it on purpose', url: `${FACTORY_API_BASE}/engine-room` },
    { label: 'The Storefront', hint: "Trattoria Giuseppe's public site", url: `${FACTORY_API_BASE}/storefront` }
  ];

  public render(): void {
    if (!this._http) {
      this._http = new FactoryHttp(this.context.aadHttpClientFactory);
    }

    ReactDOM.render(
      React.createElement(TrattoriaHero, {
        eyebrow: this.properties.eyebrow ?? 'Copilot Pizza Factory',
        headline: this.properties.headline ?? 'The trattoria that runs itself',
        lede: this.properties.lede ?? '',
        links: this.properties.showLinks === false ? [] : TrattoriaHeroWebPart.Links,
        vocabulary: this.properties.vocabulary === 'enterprise' ? 'enterprise' : 'trattoria',
        http: this._http
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
              groupName: 'Trattoria Hero',
              groupFields: [
                PropertyPaneTextField('eyebrow', { label: 'Eyebrow' }),
                PropertyPaneTextField('headline', { label: 'Headline' }),
                PropertyPaneTextField('lede', { label: 'Lede', multiline: true, rows: 5 }),
                PropertyPaneToggle('showLinks', { label: 'Show links to the live surfaces' }),
                PropertyPaneChoiceGroup('vocabulary', {
                  label: 'Vocabulary',
                  options: [
                    { key: 'trattoria', text: 'Trattoria — the story' },
                    { key: 'enterprise', text: 'Enterprise — the process' }
                  ]
                })
              ]
            }
          ]
        }
      ]
    };
  }
}
