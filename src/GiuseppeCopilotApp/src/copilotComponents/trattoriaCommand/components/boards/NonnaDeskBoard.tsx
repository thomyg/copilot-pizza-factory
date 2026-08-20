import * as React from 'react';

import { Button, Spinner, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import type { SPCopilotTheme } from '@microsoft/sp-copilot-component';

import { SERIF } from '../widgets/atoms';
import type { FactoryHttp } from '../../../../factoryApi';
import TrattoriaTheme from '../TrattoriaTheme';

interface IPurchaseOrderDto {
  id: string;
  ingredient: string;
  grams: number;
  cost: number;
  supplier: string;
  state: string;
  note?: string;
}

interface IRotaEntryDto {
  date: string;
  slot: string;
  role: string;
  assignedTo: string | null;
}

interface IInvoiceDto {
  id: string;
  supplier: string;
  ingredient: string;
  grams: number;
  cost: number;
}

const useStyles = makeStyles({
  board: { padding: '14px 16px', maxWidth: '640px' },
  header: { display: 'flex', alignItems: 'baseline', gap: '8px', marginBottom: '10px' },
  title: { margin: 0, fontFamily: SERIF, fontSize: '18px', fontWeight: 700, color: tokens.colorNeutralForeground1 },
  sub: { fontSize: '11.5px', color: tokens.colorNeutralForeground3 },
  h: { margin: '12px 0 6px', fontFamily: SERIF, fontSize: '14.5px', fontWeight: 600 },
  po: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    flexWrap: 'wrap',
    padding: '9px 12px',
    borderRadius: '8px',
    borderLeft: '3px solid var(--tc-warn)',
    backgroundColor: tokens.colorNeutralBackground2,
    marginBottom: '6px',
    fontSize: '12.5px'
  },
  poCost: { fontFamily: SERIF, fontWeight: 700, color: 'var(--tc-gold)' },
  grow: { flexGrow: 1 },
  approve: {
    backgroundColor: 'var(--tc-ok)',
    ':hover': { backgroundColor: 'var(--tc-basil)' }
  },
  quiet: { fontSize: '12px', fontStyle: 'italic', color: tokens.colorNeutralForeground3 },
  rotaRow: { display: 'flex', gap: '6px', flexWrap: 'wrap', marginBottom: '5px', fontSize: '12px', alignItems: 'baseline' },
  rotaWhen: { minWidth: '120px', color: tokens.colorNeutralForeground3 },
  seat: {
    padding: '2px 9px',
    borderRadius: '999px',
    backgroundColor: tokens.colorNeutralBackground3
  },
  seatOpen: { backgroundColor: 'var(--tc-danger)', color: '#fff', fontWeight: 600 },
  invoice: { display: 'flex', justifyContent: 'space-between', gap: '8px', fontSize: '12px', padding: '4px 2px', borderBottom: '1px dashed var(--tc-line)' }
});

function groupRota(rota: IRotaEntryDto[]): Array<{ when: string; seats: IRotaEntryDto[] }> {
  const groups: Array<{ when: string; seats: IRotaEntryDto[] }> = [];
  for (const entry of rota) {
    const when = `${entry.date} ${entry.slot}`;
    const existing = groups.filter((g) => g.when === when)[0];
    if (existing) {
      existing.seats.push(entry);
    } else {
      groups.push({ when, seats: [entry] });
    }
  }
  return groups;
}

export interface INonnaDeskBoardProps {
  /** Base of the Nonna API, e.g. https://host/api/nonna */
  apiBase: string;
  /** The factory's front door, with the signed-in user's token attached. */
  http: FactoryHttp;
  theme?: SPCopilotTheme;
}

/**
 * Nonna's desk, live: pending purchase orders with real approve/reject buttons, today's
 * rota with open seats highlighted, and the latest invoices — all straight from
 * TrattoriaSoft via the guarded /api/nonna endpoints. The approve button moves real money
 * (well, demo money) and real grams: two seconds later the factory's silo fills.
 */
const NonnaDeskBoard: React.FunctionComponent<INonnaDeskBoardProps> = (props) => {
  const s = useStyles();
  const [orders, setOrders] = React.useState<IPurchaseOrderDto[] | undefined>(undefined);
  const [rota, setRota] = React.useState<IRotaEntryDto[]>([]);
  const [invoices, setInvoices] = React.useState<IInvoiceDto[]>([]);
  const [error, setError] = React.useState<string | undefined>(undefined);

  const refresh = React.useCallback(async (): Promise<void> => {
    try {
      const [po, ro, inv] = await Promise.all([
        props.http.getJson<IPurchaseOrderDto[]>(`${props.apiBase}/purchase-orders`),
        props.http.getJson<IRotaEntryDto[]>(`${props.apiBase}/rota?days=2`),
        props.http.getJson<IInvoiceDto[]>(`${props.apiBase}/invoices`)
      ]);
      setOrders(po);
      setRota(ro);
      setInvoices(inv);
      setError(undefined);
    } catch {
      setError('TrattoriaSoft is not answering — is the factory running (and the API base configured)?');
    }
  }, [props.apiBase, props.http]);

  React.useEffect(() => {
    void refresh();
    const timer = window.setInterval(() => void refresh(), 4000);
    return () => window.clearInterval(timer);
  }, [refresh]);

  const act = async (id: string, action: 'approve' | 'reject'): Promise<void> => {
    await props.http
      .postJson<unknown>(
        `${props.apiBase}/purchase-orders/${id}/${action}`,
        action === 'reject' ? { reason: 'Nonna said no' } : {}
      )
      .catch(() => undefined);
    await refresh();
  };

  const pending: IPurchaseOrderDto[] = (orders ?? []).filter((o) => o.state === 'PendingApproval');

  return (
    <TrattoriaTheme theme={props.theme}>
      <div className={s.board}>
        <div className={s.header}>
          <h2 className={s.title}>🧾 Nonna&apos;s Desk</h2>
          <span className={s.sub}>TrattoriaSoft ERP 3000 · live · she sees everything</span>
        </div>

        {error && <p className={s.quiet}>{error}</p>}
        {!error && orders === undefined && <Spinner size="tiny" label="Opening the ledger…" />}

        {orders !== undefined && (
          <>
            <h3 className={s.h}>✍️ Waiting for a signature</h3>
            {pending.length === 0 && <p className={s.quiet}>Nothing pending. A rare moment — enjoy it.</p>}
            {pending.map((o) => (
              <div key={o.id} className={s.po}>
                <strong>{o.id}</strong>
                <span>
                  {o.grams} g {o.ingredient} · {o.supplier}
                </span>
                <span className={s.poCost}>€{o.cost.toFixed(2)}</span>
                <span className={s.grow} />
                <Button size="small" appearance="primary" className={s.approve} onClick={() => void act(o.id, 'approve')}>
                  Approve
                </Button>
                <Button size="small" onClick={() => void act(o.id, 'reject')}>
                  Reject
                </Button>
              </div>
            ))}

            <h3 className={s.h}>📋 The rota — today &amp; tomorrow</h3>
            {groupRota(rota).map((group) => (
              <div key={group.when} className={s.rotaRow}>
                <span className={s.rotaWhen}>{group.when}</span>
                {group.seats.map((seat, i) => (
                  <span key={i} className={mergeClasses(s.seat, seat.assignedTo ? undefined : s.seatOpen)}>
                    {seat.role}: {seat.assignedTo ?? 'OPEN'}
                  </span>
                ))}
              </div>
            ))}

            <h3 className={s.h}>💶 Latest invoices</h3>
            {invoices.length === 0 && <p className={s.quiet}>No invoices yet — a quiet ledger is a suspicious ledger.</p>}
            {invoices.slice(0, 5).map((i) => (
              <div key={i.id} className={s.invoice}>
                <span>
                  {i.id} · {i.grams} g {i.ingredient} · {i.supplier}
                </span>
                <span className={s.poCost}>€{i.cost.toFixed(2)}</span>
              </div>
            ))}
          </>
        )}
      </div>
    </TrattoriaTheme>
  );
};

export default NonnaDeskBoard;
