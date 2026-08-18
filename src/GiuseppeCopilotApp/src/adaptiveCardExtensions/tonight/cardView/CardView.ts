import {
  BasePrimaryTextCardView,
  type IPrimaryTextCardParameters,
  type IQuickViewCardAction
} from '@microsoft/sp-adaptive-card-extension-base';

import type {
  ITonightAdaptiveCardExtensionProps,
  ITonightAdaptiveCardExtensionState
} from '../TonightAdaptiveCardExtension';
import { QUICK_VIEW_REGISTRY_ID } from '../TonightAdaptiveCardExtension';

export class CardView extends BasePrimaryTextCardView<
  ITonightAdaptiveCardExtensionProps,
  ITonightAdaptiveCardExtensionState
> {
  public get data(): IPrimaryTextCardParameters {
    const tonight = this.state.snapshot?.tonight;
    if (!tonight) {
      return { primaryText: '🍕 Tonight at the trattoria', description: 'Warming the oven…' };
    }

    const inFlight: number =
      tonight.line.ordered + tonight.line.preparing + tonight.line.baking + tonight.line.ready;
    const stars: string = tonight.averageStars ? ` · ⭐ ${tonight.averageStars.toFixed(1)}` : '';

    return {
      primaryText: `🍕 ${tonight.serviceOpen ? 'Service open' : 'Service closed'} — ${tonight.tablesSeated}/${tonight.tablesTotal} tables`,
      description: `${inFlight} pies in flight · ${tonight.guestsServed} guests served${stars}`
    };
  }

  public get onCardSelection(): IQuickViewCardAction {
    return {
      type: 'QuickView',
      parameters: { view: QUICK_VIEW_REGISTRY_ID }
    };
  }
}
