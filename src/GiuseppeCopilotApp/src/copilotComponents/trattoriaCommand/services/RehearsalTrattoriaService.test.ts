import type { IPreOrderEntry, ITonightSnapshot, ITrattoriaSnapshot } from '../models/trattoria';
import { MENU } from '../models/trattoria';
import {
  RehearsalTrattoriaService,
  dayNumber,
  deriveRisks,
  mulberry32,
  occupancyCurve
} from './RehearsalTrattoriaService';

const service = new RehearsalTrattoriaService();

/** A Wednesday evening mid-service — the classic demo moment. */
const WEDNESDAY_2015: Date = new Date(2026, 7, 19, 20, 15, 0);

describe('RehearsalTrattoriaService', () => {
  it('is deterministic for the same moment', async () => {
    const a: ITrattoriaSnapshot = await service.getSnapshot(WEDNESDAY_2015);
    const b: ITrattoriaSnapshot = await service.getSnapshot(new Date(WEDNESDAY_2015.getTime()));
    expect(JSON.stringify(a)).toEqual(JSON.stringify(b));
  });

  it('keeps the seeded history honest: 7 days, weekends 95–130 orders', async () => {
    const snapshot: ITrattoriaSnapshot = await service.getSnapshot(WEDNESDAY_2015);
    expect(snapshot.report.history).toHaveLength(7);

    const saturday = snapshot.report.history.filter((d) => d.label.indexOf('Sat') === 0)[0];
    expect(saturday).toBeDefined();
    expect(saturday.orders).toBeGreaterThanOrEqual(95);
    expect(saturday.orders).toBeLessThanOrEqual(130);

    const today = snapshot.report.history[snapshot.report.history.length - 1];
    expect(today.isToday).toBe(true);
  });

  it('serves dinner: the evening peak beats the dead of night', () => {
    expect(occupancyCurve(20)).toBeGreaterThan(0.8);
    expect(occupancyCurve(4)).toBeLessThan(0.05);
    expect(occupancyCurve(13)).toBeGreaterThan(occupancyCurve(16));
  });

  it('quotes the house menu at house prices', () => {
    const margherita = MENU.filter((p) => p.name === 'Margherita')[0];
    expect(margherita.price).toBe(9.9);
    expect(MENU).toHaveLength(6);
  });

  it('mulberry32 is stable and dayNumber ignores the clock', () => {
    const r1: () => number = mulberry32(42);
    const r2: () => number = mulberry32(42);
    expect(r1()).toEqual(r2());
    expect(dayNumber(new Date(2026, 7, 19, 3, 0))).toEqual(dayNumber(new Date(2026, 7, 19, 23, 59)));
  });
});

describe('deriveRisks — the crystal ball', () => {
  const calmTonight = (overrides?: Partial<ITonightSnapshot>): ITonightSnapshot => ({
    serviceOpen: true,
    tablesSeated: 6,
    tablesTotal: 17,
    line: { ordered: 2, preparing: 1, baking: 1, ready: 0 },
    guestsServed: 20,
    averageStars: 4.4,
    stock: [{ ingredient: 'Dough', grams: 1500, openingGrams: 2000, state: 'ok' }],
    channels: { web: 3, chat: 2, copilot: 1, phone: 1, walkIn: 13 },
    feed: [],
    ...overrides
  });

  it('reports quiet water when nothing threatens the evening', () => {
    const risks = deriveRisks(calmTonight(), []);
    expect(risks).toHaveLength(1);
    expect(risks[0].severity).toBe('low');
    expect(risks[0].title).toBe('Quiet water');
  });

  it('escalates crisis stock to a high risk, sorted first', () => {
    const risks = deriveRisks(
      calmTonight({
        stock: [
          { ingredient: 'Mozzarella', grams: 900, openingGrams: 1500, state: 'ok' },
          { ingredient: 'Dough', grams: 100, openingGrams: 2000, state: 'crisis' }
        ],
        tablesSeated: 16
      }),
      []
    );
    expect(risks[0].severity).toBe('high');
    expect(risks[0].title).toContain('Dough');
  });

  it('links low stock to a committed pre-order within three hours', () => {
    const preOrders: IPreOrderEntry[] = [
      { pizza: 'Hawaii', amount: 6, whenLabel: 'tonight 21:00', hoursOut: 1.5, name: 'The Brave Table' }
    ];
    const risks = deriveRisks(
      calmTonight({
        stock: [{ ingredient: 'Pineapple', grams: 200, openingGrams: 500, state: 'low' }]
      }),
      preOrders
    );
    expect(risks[0].severity).toBe('high');
    expect(risks[0].detail).toContain('The Brave Table');
  });

  it('flags big parties and full rooms as medium pressure', () => {
    const preOrders: IPreOrderEntry[] = [
      { pizza: 'Diavolo', amount: 12, whenLabel: 'Sun 20:00', hoursOut: 20, name: 'AC Rosso Ultras' }
    ];
    const risks = deriveRisks(calmTonight({ tablesSeated: 16 }), preOrders);
    const titles: string = risks.map((r) => r.title).join(' | ');
    expect(titles).toContain('Dining room near capacity');
    expect(titles).toContain('AC Rosso Ultras');
    expect(risks.every((r) => r.severity === 'medium')).toBe(true);
  });
});
