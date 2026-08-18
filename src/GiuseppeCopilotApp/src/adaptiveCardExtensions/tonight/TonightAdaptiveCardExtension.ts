import { BaseAdaptiveCardExtension } from '@microsoft/sp-adaptive-card-extension-base';

import type { ITrattoriaSnapshot } from '../../copilotComponents/trattoriaCommand/models/trattoria';
import { RehearsalTrattoriaService } from '../../copilotComponents/trattoriaCommand/services/RehearsalTrattoriaService';
import { CardView } from './cardView/CardView';
import { QuickView } from './quickView/QuickView';

export interface ITonightAdaptiveCardExtensionProps {
  title: string;
}

export interface ITonightAdaptiveCardExtensionState {
  snapshot?: ITrattoriaSnapshot;
}

export const CARD_VIEW_REGISTRY_ID = 'Tonight_CARD_VIEW';
export const QUICK_VIEW_REGISTRY_ID = 'Tonight_QUICK_VIEW';

/**
 * "Tonight at the trattoria" for the Viva Connections dashboard — the factory in
 * your pocket. Same rehearsal data service as the Copilot cockpit and the web parts.
 */
export default class TonightAdaptiveCardExtension extends BaseAdaptiveCardExtension<
  ITonightAdaptiveCardExtensionProps,
  ITonightAdaptiveCardExtensionState
> {
  public onInit(): Promise<void> {
    this.state = {};

    this.cardNavigator.register(CARD_VIEW_REGISTRY_ID, () => new CardView());
    this.quickViewNavigator.register(QUICK_VIEW_REGISTRY_ID, () => new QuickView());

    new RehearsalTrattoriaService()
      .getSnapshot(new Date())
      .then((snapshot) => this.setState({ snapshot }))
      .catch(() => undefined);

    return Promise.resolve();
  }

  protected renderCard(): string | undefined {
    return CARD_VIEW_REGISTRY_ID;
  }
}
