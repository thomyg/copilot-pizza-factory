import { BaseAdaptiveCardQuickView, type ISPFxAdaptiveCard } from '@microsoft/sp-adaptive-card-extension-base';

import type {
  ITonightAdaptiveCardExtensionProps,
  ITonightAdaptiveCardExtensionState
} from '../TonightAdaptiveCardExtension';

interface IQuickViewData {
  title: string;
  status: string;
  line: string;
  topRisk: string;
  suggestion: string;
  feed: Array<{ text: string }>;
}

export class QuickView extends BaseAdaptiveCardQuickView<
  ITonightAdaptiveCardExtensionProps,
  ITonightAdaptiveCardExtensionState,
  IQuickViewData
> {
  public get data(): IQuickViewData {
    const snapshot = this.state.snapshot;
    if (!snapshot) {
      return { title: '🍕 Tonight at the trattoria', status: 'Warming the oven…', line: '', topRisk: '', suggestion: '', feed: [] };
    }

    const t = snapshot.tonight;
    const risk = snapshot.risks[0];
    return {
      title: '🍕 Tonight at the trattoria',
      status: `${t.serviceOpen ? '● Service open' : '○ Service closed'} · ${t.tablesSeated}/${t.tablesTotal} tables · ${t.guestsServed} guests served`,
      line: `Kitchen line: ${t.line.ordered} ordered → ${t.line.preparing} preparing → ${t.line.baking} baking → ${t.line.ready} ready`,
      topRisk: risk ? `🔮 ${risk.title}: ${risk.detail}` : '',
      suggestion: risk ? `→ ${risk.suggestion}` : '',
      feed: t.feed.slice(0, 3).map((f) => ({ text: `${f.at} · ${f.text}` }))
    };
  }

  public get template(): ISPFxAdaptiveCard {
    return {
      $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
      type: 'AdaptiveCard',
      version: '1.5',
      body: [
        { type: 'TextBlock', text: '${title}', weight: 'Bolder', size: 'Large', wrap: true },
        { type: 'TextBlock', text: '${status}', wrap: true, spacing: 'Small' },
        { type: 'TextBlock', text: '${line}', wrap: true, isSubtle: true, spacing: 'Small' },
        { type: 'TextBlock', text: '${topRisk}', wrap: true, spacing: 'Medium' },
        { type: 'TextBlock', text: '${suggestion}', wrap: true, isSubtle: true, color: 'Good', spacing: 'Small' },
        {
          type: 'Container',
          spacing: 'Medium',
          items: [
            {
              type: 'TextBlock',
              text: 'From the floor',
              weight: 'Bolder',
              size: 'Small'
            },
            {
              type: 'Container',
              $data: '${feed}',
              items: [{ type: 'TextBlock', text: '${text}', wrap: true, size: 'Small', isSubtle: true }]
            }
          ]
        }
      ]
    };
  }
}
