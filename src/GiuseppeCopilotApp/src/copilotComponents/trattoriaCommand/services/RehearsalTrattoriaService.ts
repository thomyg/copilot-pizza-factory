import type {
  IBusinessReport,
  IChannelSplit,
  IDayHistory,
  IFeedItem,
  IForecastRisk,
  IPreOrderEntry,
  IStockLevel,
  ITonightSnapshot,
  ITrattoriaSnapshot
} from '../models/trattoria';
import { MENU } from '../models/trattoria';
import type { ITrattoriaDataService } from './ITrattoriaDataService';

/**
 * Deterministic rehearsal data mirroring the live Copilot Pizza Factory simulation:
 * same menu and prices as PriceList, the Bookkeeper's seeded 7-day history
 * (weekends 95–130 orders), Procurement's 300g restock / 150g crisis thresholds,
 * and the crystal-ball rules from Bookkeeper.ForecastAsync. Seeded by calendar
 * day + hour, so the cockpit is alive across the day but stable within a demo.
 */
export class RehearsalTrattoriaService implements ITrattoriaDataService {
  public getSnapshot(now: Date): Promise<ITrattoriaSnapshot> {
    const daySeed: number = dayNumber(now);
    const hourSeed: number = daySeed * 31 + now.getHours();

    const tonight: ITonightSnapshot = buildTonight(now, mulberry32(hourSeed));
    const preOrders: IPreOrderEntry[] = buildPreOrders(now);
    const report: IBusinessReport = buildReport(now, tonight, mulberry32(daySeed));
    const risks: IForecastRisk[] = deriveRisks(tonight, preOrders);

    return Promise.resolve({ tonight, report, risks, preOrders });
  }
}

/* ---------------------------------------------------------------- tonight */

const OPENING_STOCK: ReadonlyArray<{ ingredient: string; openingGrams: number }> = [
  { ingredient: 'Dough', openingGrams: 2000 },
  { ingredient: 'Tomato sauce', openingGrams: 1500 },
  { ingredient: 'Mozzarella', openingGrams: 1500 },
  { ingredient: 'Salami', openingGrams: 800 },
  { ingredient: 'Ham', openingGrams: 800 },
  { ingredient: 'Mushroom', openingGrams: 600 },
  { ingredient: 'Pineapple', openingGrams: 500 },
  { ingredient: 'Tuna', openingGrams: 500 }
];

const RESTOCK_THRESHOLD = 300;
const CRISIS_THRESHOLD = 150;

function buildTonight(now: Date, rand: () => number): ITonightSnapshot {
  const occupancy: number = occupancyCurve(now.getHours() + now.getMinutes() / 60);
  const serviceOpen: boolean = occupancy > 0.05;
  const tablesTotal = 17;
  const tablesSeated: number = serviceOpen
    ? Math.min(tablesTotal, Math.round(tablesTotal * occupancy * (0.85 + rand() * 0.3)))
    : 0;

  const inFlight: number = Math.round(tablesSeated * (0.6 + rand() * 0.5)) + Math.round(rand() * 3);
  const line = {
    ordered: Math.round(inFlight * 0.3),
    preparing: Math.round(inFlight * 0.25),
    baking: Math.round(inFlight * 0.2),
    ready: Math.max(0, inFlight - Math.round(inFlight * 0.75))
  };

  const dayProgress: number = Math.min(1, Math.max(0, (hoursSinceOpen(now)) / 12));
  const stock: IStockLevel[] = OPENING_STOCK.map((s) => {
    // Pineapple runs hot on purpose — the Hawaii gag is a house tradition.
    const burnRate: number = s.ingredient === 'Pineapple' ? 0.55 + rand() * 0.4 : 0.25 + rand() * 0.45;
    const grams: number = Math.max(0, Math.round(s.openingGrams * (1 - burnRate * dayProgress)));
    const state: IStockLevel['state'] =
      grams <= CRISIS_THRESHOLD ? 'crisis' : grams <= RESTOCK_THRESHOLD ? 'low' : 'ok';
    return { ingredient: s.ingredient, grams, openingGrams: s.openingGrams, state };
  });

  const guestsServed: number = Math.round(60 * dayProgress * (0.8 + rand() * 0.4));
  const averageStars: number | undefined =
    guestsServed > 0 ? Math.round((3.9 + rand() * 0.9) * 10) / 10 : undefined;

  return {
    serviceOpen,
    tablesSeated,
    tablesTotal,
    line,
    guestsServed,
    averageStars,
    stock,
    channels: channelSplit(guestsServed + inFlight, rand),
    feed: buildFeed(now, rand)
  };
}

/** Lunch bump around 12–14, evening peak 18–22 — same rhythm as the MaitreD. */
export function occupancyCurve(hour: number): number {
  const lunch: number = bell(hour, 12.8, 1.4) * 0.55;
  const dinner: number = bell(hour, 19.8, 2.0) * 1.0;
  return Math.min(1, lunch + dinner);
}

function bell(x: number, center: number, width: number): number {
  const d: number = (x - center) / width;
  return Math.exp(-d * d);
}

function hoursSinceOpen(now: Date): number {
  const open = 11; // the trattoria opens for lunch
  return Math.max(0, now.getHours() + now.getMinutes() / 60 - open);
}

function channelSplit(total: number, rand: () => number): IChannelSplit {
  const web: number = Math.round(total * (0.2 + rand() * 0.1));
  const chat: number = Math.round(total * (0.12 + rand() * 0.08));
  const copilot: number = Math.round(total * (0.08 + rand() * 0.07));
  const phone: number = Math.round(total * (0.08 + rand() * 0.05));
  return { web, chat, copilot, phone, walkIn: Math.max(0, total - web - chat - copilot - phone) };
}

const FEED_LINES: readonly string[] = [
  '🍕 Table 12 ordered 2× Diavolo — "extra hot, we can take it."',
  '⭐ Nonna Lucia left 5 stars: "the dough sings."',
  '🛵 Web order for Bruno M. handed to the courier — still steaming.',
  '🧾 Copilot order: 3× Margherita for the marketing stand-up.',
  '🍍 A brave soul ordered Hawaii. The kitchen said nothing. Loudly.',
  '📞 Phone order: 2× Al Tonno, pickup in 20 — "tell Giuseppe it\'s Carla."',
  '🚪 Walk-ins at table 3 — two guests, one very good mood.',
  '⭐ 4 stars from table 9: "perfetto, but the wait built character."',
  '🔥 Oven running full — four pies in, crust weather is excellent.',
  '🥖 Dough Master reports the evening batch is resting nicely.'
];

function buildFeed(now: Date, rand: () => number): IFeedItem[] {
  const items: IFeedItem[] = [];
  let minutesAgo = 1 + Math.floor(rand() * 4);
  const start: number = Math.floor(rand() * FEED_LINES.length);
  for (let i = 0; i < 5; i++) {
    const at = new Date(now.getTime() - minutesAgo * 60000);
    items.push({
      at: `${pad(at.getHours())}:${pad(at.getMinutes())}`,
      text: FEED_LINES[(start + i * 3) % FEED_LINES.length]
    });
    minutesAgo += 3 + Math.floor(rand() * 9);
  }
  return items;
}

/* ----------------------------------------------------------------- report */

function buildReport(now: Date, tonight: ITonightSnapshot, rand: () => number): IBusinessReport {
  const history: IDayHistory[] = [];
  for (let back = 6; back >= 0; back--) {
    const day = new Date(now.getFullYear(), now.getMonth(), now.getDate() - back);
    const r: () => number = mulberry32(dayNumber(day));
    const weekend: boolean = day.getDay() === 0 || day.getDay() === 6;
    // Same ranges as the Bookkeeper's seeded backstory: weekends 95–130, weekdays 55–90.
    let orders: number = weekend ? 95 + Math.floor(r() * 36) : 55 + Math.floor(r() * 36);
    const avgTicket: number = 11.2 + r() * 1.2;
    if (back === 0) {
      const dayProgress: number = Math.min(1, Math.max(0.05, hoursSinceOpen(now) / 12));
      orders = Math.round(orders * dayProgress);
    }
    history.push({
      label: day.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' }),
      orders,
      revenue: Math.round(orders * avgTicket * 100) / 100,
      isToday: back === 0
    });
  }

  const today: IDayHistory = history[history.length - 1];
  const dayProgress: number = Math.min(1, Math.max(0.05, hoursSinceOpen(now) / 12));
  const topPizza: string = MENU[Math.floor(rand() * MENU.length)].name;

  return {
    dateLabel: now.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' }),
    ordersToday: today.orders,
    pizzasToday: Math.round(today.orders * (1.6 + rand() * 0.5)),
    revenueToday: today.revenue,
    paceProjection: Math.round((today.revenue / dayProgress) * 100) / 100,
    topPizza,
    averageStars: tonight.averageStars,
    channels: tonight.channels,
    history
  };
}

/* -------------------------------------------------------------- preorders */

function buildPreOrders(now: Date): IPreOrderEntry[] {
  const entries: Array<{ pizza: string; amount: number; name: string; when: Date }> = [
    { pizza: 'Diavolo', amount: 10, name: "Nonna's Bingo Club", when: nextWeekdayAt(now, 6, 18) },
    { pizza: 'Margherita', amount: 6, name: 'Team Retro (Friday)', when: nextWeekdayAt(now, 5, 12) },
    { pizza: 'Diavolo', amount: 12, name: 'AC Rosso Ultras — after the derby', when: nextWeekdayAt(now, 0, 20) },
    { pizza: 'Funghi', amount: 4, name: 'Book circle, chapter 12', when: inHours(now, 2.5) }
  ];

  return entries
    .sort((a, b) => a.when.getTime() - b.when.getTime())
    .map((e) => ({
      pizza: e.pizza,
      amount: e.amount,
      name: e.name,
      hoursOut: Math.round(((e.when.getTime() - now.getTime()) / 3600000) * 10) / 10,
      whenLabel: e.when.toLocaleDateString('en-GB', {
        weekday: 'short',
        day: 'numeric',
        month: 'short'
      }) + ` ${pad(e.when.getHours())}:${pad(e.when.getMinutes())}`
    }));
}

function nextWeekdayAt(now: Date, weekday: number, hour: number): Date {
  let days: number = (weekday - now.getDay() + 7) % 7;
  if (days === 0 && now.getHours() >= hour) {
    days = 7;
  }
  return new Date(now.getFullYear(), now.getMonth(), now.getDate() + days, hour, 0, 0);
}

function inHours(now: Date, hours: number): Date {
  return new Date(now.getTime() + hours * 3600000);
}

/* ------------------------------------------------------------------ risks */

/**
 * The crystal ball — same rules as Bookkeeper.ForecastAsync in the live factory:
 * stock vs committed demand, table pressure, big parties on the book.
 */
export function deriveRisks(tonight: ITonightSnapshot, preOrders: IPreOrderEntry[]): IForecastRisk[] {
  const risks: IForecastRisk[] = [];

  const inFlight: number =
    tonight.line.ordered + tonight.line.preparing + tonight.line.baking + tonight.line.ready;

  for (const s of tonight.stock) {
    if (s.state === 'crisis') {
      risks.push({
        severity: 'high',
        title: `${s.ingredient} at crisis level`,
        detail: `${s.grams}g left (crisis threshold ${CRISIS_THRESHOLD}g) with ${inFlight} orders on the line.`,
        suggestion: 'Procurement should already be dialing the supplier — check the Engine Room.'
      });
    } else if (s.state === 'low') {
      const committed: IPreOrderEntry[] = preOrders.filter(
        (p) => p.hoursOut <= 3 && usesIngredient(p.pizza, s.ingredient)
      );
      risks.push({
        severity: committed.length > 0 ? 'high' : 'low',
        title: `${s.ingredient} running low`,
        detail:
          committed.length > 0
            ? `${s.grams}g left and "${committed[0].name}" fires ${committed[0].amount}× ${committed[0].pizza} in ${committed[0].hoursOut}h.`
            : `${s.grams}g left (restock threshold ${RESTOCK_THRESHOLD}g).`,
        suggestion:
          committed.length > 0
            ? 'Restock before the pre-order fires or call the guest with a charming plan B.'
            : 'Procurement will restock on the next pass; no drama yet.'
      });
    }
  }

  if (tonight.tablesSeated >= 15) {
    risks.push({
      severity: 'medium',
      title: 'Dining room near capacity',
      detail: `${tonight.tablesSeated}/${tonight.tablesTotal} tables seated — walk-ins will start bouncing.`,
      suggestion: 'Warn the door, push takeaway, and keep the espresso machine warm.'
    });
  }

  for (const p of preOrders) {
    if (p.amount >= 8 && p.hoursOut <= 24) {
      risks.push({
        severity: 'medium',
        title: `Big party incoming: ${p.name}`,
        detail: `${p.amount}× ${p.pizza} fires in ${p.hoursOut}h — that is a full oven cycle on its own.`,
        suggestion: 'Pre-stage dough and toppings an hour ahead; the oven does not negotiate.'
      });
    }
  }

  if (risks.length === 0) {
    risks.push({
      severity: 'low',
      title: 'Quiet water',
      detail: 'Stock healthy, tables comfortable, no big parties in the next hours.',
      suggestion: 'Enjoy it. In this house, quiet is a rumor.'
    });
  }

  const order: Record<string, number> = { high: 0, medium: 1, low: 2 };
  return risks.sort((a, b) => order[a.severity] - order[b.severity]);
}

const RECIPES: Record<string, string[]> = {
  Margherita: ['Dough', 'Tomato sauce', 'Mozzarella'],
  Diavolo: ['Dough', 'Tomato sauce', 'Mozzarella', 'Salami'],
  Hawaii: ['Dough', 'Tomato sauce', 'Mozzarella', 'Ham', 'Pineapple'],
  Prosciutto: ['Dough', 'Tomato sauce', 'Mozzarella', 'Ham'],
  Funghi: ['Dough', 'Tomato sauce', 'Mozzarella', 'Mushroom'],
  'Al Tonno': ['Dough', 'Tomato sauce', 'Mozzarella', 'Tuna']
};

function usesIngredient(pizza: string, ingredient: string): boolean {
  return (RECIPES[pizza] ?? []).indexOf(ingredient) >= 0;
}

/** Menu availability derived from the pantry: 'out' when a required topping is in crisis, 'low' when it runs low. */
export function pizzaAvailability(stock: IStockLevel[]): Record<string, 'ok' | 'low' | 'out'> {
  const result: Record<string, 'ok' | 'low' | 'out'> = {};
  for (const pizza of Object.keys(RECIPES)) {
    const needed: IStockLevel[] = stock.filter((s) => usesIngredient(pizza, s.ingredient));
    result[pizza] = needed.some((s) => s.state === 'crisis')
      ? 'out'
      : needed.some((s) => s.state === 'low')
        ? 'low'
        : 'ok';
  }
  return result;
}

/* ------------------------------------------------------------------ utils */

/** Days since epoch for the local calendar day — the seed the Bookkeeper uses. */
export function dayNumber(d: Date): number {
  return Math.floor(new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime() / 86400000);
}

/** Small, fast, deterministic PRNG — stands in for .NET's seeded Random. */
export function mulberry32(seed: number): () => number {
  let a: number = seed >>> 0;
  return () => {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t: number = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function pad(n: number): string {
  return n < 10 ? `0${n}` : `${n}`;
}
