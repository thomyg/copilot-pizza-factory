/**
 * View + data contracts for the Trattoria Command cockpit.
 *
 * These mirror the shapes the real Copilot Pizza Factory simulation exposes
 * (MaitreD, Bookkeeper, PreOrderBook, Procurement) so a live data service can
 * replace the rehearsal one without touching the UI.
 */

export type ViewKey = 'tonight' | 'report' | 'forecast' | 'preorders';

export interface IStockLevel {
  ingredient: string;
  grams: number;
  openingGrams: number;
  state: 'ok' | 'low' | 'crisis';
}

export interface IChannelSplit {
  web: number;
  chat: number;
  copilot: number;
  phone: number;
  walkIn: number;
}

export interface IFeedItem {
  /** Short local time label, e.g. "19:42". */
  at: string;
  text: string;
}

export interface ITonightSnapshot {
  serviceOpen: boolean;
  tablesSeated: number;
  tablesTotal: number;
  line: { ordered: number; preparing: number; baking: number; ready: number };
  guestsServed: number;
  /** undefined until the first review lands. */
  averageStars: number | undefined;
  stock: IStockLevel[];
  channels: IChannelSplit;
  feed: IFeedItem[];
}

export interface IDayHistory {
  /** e.g. "Sat 15 Aug" */
  label: string;
  orders: number;
  revenue: number;
  isToday: boolean;
}

export interface IBusinessReport {
  dateLabel: string;
  ordersToday: number;
  pizzasToday: number;
  revenueToday: number;
  /** Projected end-of-day revenue at the current pace. */
  paceProjection: number;
  topPizza: string;
  averageStars: number | undefined;
  channels: IChannelSplit;
  history: IDayHistory[];
}

export type RiskSeverity = 'high' | 'medium' | 'low';

export interface IForecastRisk {
  severity: RiskSeverity;
  title: string;
  detail: string;
  suggestion: string;
}

export interface IPreOrderEntry {
  pizza: string;
  amount: number;
  whenLabel: string;
  /** Hours from now until the order fires; used for risk derivation. */
  hoursOut: number;
  name: string;
}

/** Everything the cockpit needs, resolved in one call. */
export interface ITrattoriaSnapshot {
  tonight: ITonightSnapshot;
  report: IBusinessReport;
  risks: IForecastRisk[];
  preOrders: IPreOrderEntry[];
}

/** The house menu with prices — same as PriceList in the factory. */
export const MENU: ReadonlyArray<{ name: string; price: number }> = [
  { name: 'Margherita', price: 9.9 },
  { name: 'Diavolo', price: 12.9 },
  { name: 'Hawaii', price: 11.9 },
  { name: 'Prosciutto', price: 12.4 },
  { name: 'Funghi', price: 11.4 },
  { name: 'Al Tonno', price: 12.9 }
];
