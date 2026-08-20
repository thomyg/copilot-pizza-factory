/**
 * Rehearsal data mirroring TrattoriaSoft ERP 3000: the same nine-person roster, the same
 * deterministic weekly rota rotation as StaffBook, the same pineapple saga in the ledger.
 * Deterministic per calendar day, so demos are alive but stable.
 */

export type DeskView = 'rota' | 'approvals' | 'invoices';

export interface IRehearsalRotaEntry {
  dayLabel: string;
  slot: 'Lunch' | 'Dinner';
  role: 'Pizzaiolo' | 'Service' | 'Courier';
  assignedTo: string | null;
}

export interface IRehearsalOrder {
  id: string;
  ingredient: string;
  grams: number;
  cost: number;
  supplier: string;
  state: 'PendingApproval' | 'Delivered';
  note: string;
}

export interface IRehearsalInvoice {
  id: string;
  supplier: string;
  ingredient: string;
  grams: number;
  cost: number;
}

export interface IBackOfficeSnapshot {
  rota: IRehearsalRotaEntry[];
  orders: IRehearsalOrder[];
  invoices: IRehearsalInvoice[];
  invoiceTotal: number;
  absentToday: string;
}

const PIZZAIOLI: readonly string[] = ['Luca', 'Sofia']; // oven-certified only — Giulia waits her turn
const SERVICE: readonly string[] = ['Elena', 'Maria', 'Paolo', 'Rosa'];
const COURIERS: readonly string[] = ['Antonio', 'Marco'];

function dayNumber(d: Date): number {
  return Math.floor(new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime() / 86400000);
}

function pick(pool: readonly string[], date: Date, slotIndex: number, seat: number): string {
  return pool[(dayNumber(date) + slotIndex + seat) % pool.length];
}

export function backOfficeSnapshot(now: Date): IBackOfficeSnapshot {
  const rota: IRehearsalRotaEntry[] = [];
  const absentToday: string = SERVICE[(dayNumber(now) + 1) % SERVICE.length];

  for (let d = 0; d < 2; d++) {
    const date = new Date(now.getFullYear(), now.getMonth(), now.getDate() + d);
    const label: string = date.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
    for (const [slotIndex, slot] of (['Lunch', 'Dinner'] as const).map((sl, i) => [i, sl] as const)) {
      const seats: Array<[IRehearsalRotaEntry['role'], number]> =
        slot === 'Lunch' ? [['Pizzaiolo', 1], ['Service', 1]] : [['Pizzaiolo', 1], ['Service', 2], ['Courier', 1]];
      for (const [role, count] of seats) {
        for (let seat = 0; seat < count; seat++) {
          const pool = role === 'Pizzaiolo' ? PIZZAIOLI : role === 'Service' ? SERVICE : COURIERS;
          let name: string | null = pick(pool, date, slotIndex, seat);
          // Today's sick call leaves her seat open — the demo's cover question, ready to ask.
          if (d === 0 && slot === 'Dinner' && name === absentToday) {
            name = null;
          }
          rota.push({ dayLabel: label, slot, role, assignedTo: name });
        }
      }
    }
  }

  const orders: IRehearsalOrder[] = [
    {
      id: 'PO-1003', ingredient: 'Pineapple', grams: 4000, cost: 18.4,
      supplier: 'Fruttivendolo Marittimo S.r.l.', state: 'PendingApproval',
      note: 'emergency replenishment (silo empty)'
    },
    {
      id: 'PO-1002', ingredient: 'Pineapple', grams: 1000, cost: 4.6,
      supplier: "Giuseppe's Pineapple Supplier", state: 'Delivered', note: 'A2A self-heal delivery'
    },
    {
      id: 'PO-1001', ingredient: 'Mozzarella', grams: 1000, cost: 8.9,
      supplier: 'Fruttivendolo Marittimo S.r.l.', state: 'Delivered', note: 'auto-approved (within limit)'
    }
  ];

  const invoices: IRehearsalInvoice[] = [
    { id: 'INV-PO-1002', supplier: "Giuseppe's Pineapple Supplier", ingredient: 'Pineapple', grams: 1000, cost: 4.6 },
    { id: 'INV-PO-1001', supplier: 'Fruttivendolo Marittimo S.r.l.', ingredient: 'Mozzarella', grams: 1000, cost: 8.9 },
    { id: 'INV-PO-1000', supplier: 'Fruttivendolo Marittimo S.r.l.', ingredient: 'Flour', grams: 1000, cost: 1.8 }
  ];

  return {
    rota,
    orders,
    invoices,
    invoiceTotal: Math.round(invoices.reduce((sum, i) => sum + i.cost, 0) * 100) / 100,
    absentToday
  };
}
