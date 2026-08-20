import { backOfficeSnapshot } from './RehearsalBackOffice';

const WEDNESDAY: Date = new Date(2026, 7, 19, 18, 0, 0);

describe('RehearsalBackOffice', () => {
  it('is deterministic for the same day', () => {
    expect(JSON.stringify(backOfficeSnapshot(WEDNESDAY)))
      .toEqual(JSON.stringify(backOfficeSnapshot(new Date(2026, 7, 19, 9, 0, 0))));
  });

  it('leaves exactly one open seat — the sick call, ready to demo', () => {
    const snapshot = backOfficeSnapshot(WEDNESDAY);
    const open = snapshot.rota.filter((e) => e.assignedTo === null);
    expect(open).toHaveLength(1);
    expect(open[0].slot).toBe('Dinner');
    expect(snapshot.rota.filter((e) => e.assignedTo === snapshot.absentToday && e.dayLabel === open[0].dayLabel)).toHaveLength(0);
  });

  it('keeps the fire rules: only certified pizzaioli on pizzaiolo seats', () => {
    const snapshot = backOfficeSnapshot(WEDNESDAY);
    for (const seat of snapshot.rota.filter((e) => e.role === 'Pizzaiolo')) {
      expect(['Luca', 'Sofia']).toContain(seat.assignedTo);
    }
  });

  it('mirrors the pineapple saga: one pending 4kg order, invoices totalled', () => {
    const snapshot = backOfficeSnapshot(WEDNESDAY);
    const pending = snapshot.orders.filter((o) => o.state === 'PendingApproval');
    expect(pending).toHaveLength(1);
    expect(pending[0].grams).toBe(4000);
    expect(snapshot.invoiceTotal).toBe(15.3);
  });
});
