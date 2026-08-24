import * as React from 'react';

import { Button, Spinner, makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components';

import { DISPLAY, useFornoFonts } from '../../../brand/forno';
import { FACTORY_API_BASE, type FactoryHttp } from '../../../factoryApi';
import { type Vocabulary, titleCase, vocabularyFor } from '../../../vocabulary';

/** What the house asks a human to decide, and what it already worked out for them. */
interface ITimeOffRequest {
  id: string;
  name: string;
  date: string;
  slot?: string;
  reason: string;
  state: string;
  cover: string[];
  leavesAGap: boolean;
  summary: string;
  note?: string;
}

interface IRequisition {
  id: string;
  ingredient: string;
  grams: number;
  cost: number;
  supplier: string;
  state: string;
  decision: string;
  why: string;
}

interface IBudget {
  period: string;
  budgetEur: number;
  committedEur: number;
  remainingEur: number;
  usedPercent: number;
  isTight: boolean;
}

export interface IBackOfficeProps {
  http: FactoryHttp;
  apiBase?: string;
  vocabulary?: Vocabulary;
  /** Seconds between refreshes; 0 reads once. */
  refreshSeconds?: number;
}

const useStyles = makeStyles({
  board: { width: '100%', boxSizing: 'border-box', minWidth: 0 },
  head: { display: 'flex', alignItems: 'baseline', gap: '14px', flexWrap: 'wrap', marginBottom: '4px' },
  title: { fontFamily: DISPLAY, fontWeight: 600, fontSize: '23px', margin: 0, color: tokens.colorNeutralForeground1 },
  rule: { flexGrow: 1, height: '1px', backgroundColor: tokens.colorNeutralStroke2 },

  budget: {
    marginTop: '14px',
    marginBottom: '18px',
    padding: '13px 16px',
    borderRadius: '10px',
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2)
  },
  budgetTop: { display: 'flex', justifyContent: 'space-between', gap: '12px', fontSize: '13px', flexWrap: 'wrap' },
  budgetPeriod: { fontWeight: 700, color: tokens.colorNeutralForeground1 },
  budgetFigures: { color: tokens.colorNeutralForeground3 },
  track: {
    marginTop: '9px',
    height: '7px',
    borderRadius: '999px',
    backgroundColor: tokens.colorNeutralStroke2,
    overflow: 'hidden'
  },
  fill: { height: '100%', borderRadius: '999px', backgroundColor: 'var(--tc-basil, #4a7c3a)' },
  fillTight: { backgroundColor: 'var(--tc-tomato, #c93a21)' },

  columns: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '16px' },
  column: { minWidth: 0 },
  columnTitle: {
    fontSize: '12px',
    fontWeight: 700,
    letterSpacing: '0.09em',
    textTransform: 'uppercase',
    color: tokens.colorNeutralForeground3,
    marginBottom: '8px'
  },

  card: {
    padding: '12px 14px',
    borderRadius: '10px',
    marginBottom: '9px',
    backgroundColor: tokens.colorNeutralBackground1,
    ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2)
  },
  cardWaiting: { ...shorthands.borderLeft('3px', 'solid', 'var(--tc-gold, #b07a24)') },
  cardBlocked: { ...shorthands.borderLeft('3px', 'solid', 'var(--tc-tomato, #c93a21)') },
  cardGap: { ...shorthands.borderLeft('3px', 'solid', 'var(--tc-tomato, #c93a21)') },

  line: { fontSize: '13.5px', color: tokens.colorNeutralForeground1, lineHeight: 1.5 },
  why: { marginTop: '5px', fontSize: '12px', color: tokens.colorNeutralForeground3, lineHeight: 1.5 },
  actions: { display: 'flex', gap: '7px', marginTop: '10px', flexWrap: 'wrap' },
  quiet: { fontSize: '13px', color: tokens.colorNeutralForeground3, fontStyle: 'italic' }
});

/**
 * Nonna's desk, with the two decisions that make it a back office rather than a prop:
 * somebody wants a day off and somebody wants to spend money.
 *
 * Both are shown the way a manager needs them — the situation first, the arithmetic under it,
 * and only then the buttons. A requisition the budget refuses shows no approve button at all,
 * because offering one would suggest a signature could conjure funds.
 */
const BackOffice: React.FunctionComponent<IBackOfficeProps> = (props) => {
  useFornoFonts();
  const s = useStyles();
  const words = vocabularyFor(props.vocabulary);
  const base: string = props.apiBase ?? FACTORY_API_BASE;
  const every: number = props.refreshSeconds ?? 8;

  const [timeOff, setTimeOff] = React.useState<ITimeOffRequest[] | undefined>(undefined);
  const [orders, setOrders] = React.useState<IRequisition[]>([]);
  const [budget, setBudget] = React.useState<IBudget | undefined>(undefined);
  const [busy, setBusy] = React.useState<string | undefined>(undefined);
  const [error, setError] = React.useState<string | undefined>(undefined);

  const refresh = React.useCallback(async (): Promise<void> => {
    try {
      const [t, o, b] = await Promise.all([
        props.http.getJson<ITimeOffRequest[]>(`${base}/api/nonna/time-off`),
        props.http.getJson<IRequisition[]>(`${base}/api/nonna/purchase-orders`),
        props.http.getJson<IBudget>(`${base}/api/nonna/budget`)
      ]);
      setTimeOff(t);
      setOrders(o);
      setBudget(b);
      setError(undefined);
    } catch {
      setError('The back office is not answering.');
    }
  }, [base, props.http]);

  React.useEffect(() => {
    void refresh();
    if (every <= 0) {
      return undefined;
    }
    const timer: number = window.setInterval(() => void refresh(), every * 1000);
    return () => window.clearInterval(timer);
  }, [refresh, every]);

  const act = async (url: string, body: unknown, key: string): Promise<void> => {
    setBusy(key);
    await props.http.postJson<unknown>(url, body).catch(() => undefined);
    await refresh();
    setBusy(undefined);
  };

  const pending: ITimeOffRequest[] = (timeOff ?? []).filter((r) => r.state === 'Pending');
  const settled: ITimeOffRequest[] = (timeOff ?? []).filter((r) => r.state !== 'Pending').slice(0, 3);
  const waiting: IRequisition[] = orders.filter((o) => o.state === 'PendingApproval');
  const blocked: IRequisition[] = orders.filter((o) => o.state === 'BlockedByBudget');

  if (timeOff === undefined) {
    return <Spinner size="tiny" label="Opening the ledgers…" style={{ padding: 16 }} />;
  }

  return (
    <div className={s.board}>
      <div className={s.head}>
        <h2 className={s.title}>{titleCase(words.backOffice)}</h2>
        <div className={s.rule} />
      </div>

      {budget && budget.budgetEur > 0 && (
        <div className={s.budget}>
          <div className={s.budgetTop}>
            <span className={s.budgetPeriod}>
              {budget.period} · {words.requisitions}
            </span>
            <span className={s.budgetFigures}>
              €{budget.committedEur.toFixed(2)} committed · €{budget.remainingEur.toFixed(2)} left of €
              {budget.budgetEur.toFixed(2)}
            </span>
          </div>
          <div className={s.track}>
            <div
              className={mergeClasses(s.fill, budget.isTight && s.fillTight)}
              style={{ width: `${Math.min(100, budget.usedPercent)}%` }}
            />
          </div>
        </div>
      )}

      {error && <div className={s.quiet}>{error}</div>}

      <div className={s.columns}>
        <div className={s.column}>
          <div className={s.columnTitle}>
            {words.timeOff} — {pending.length} waiting
          </div>

          {pending.length === 0 && <div className={s.quiet}>Nothing waiting on a decision.</div>}

          {pending.map((r) => (
            <div key={r.id} className={mergeClasses(s.card, r.leavesAGap ? s.cardGap : s.cardWaiting)}>
              <div className={s.line}>{r.summary}</div>
              <div className={s.why}>
                {r.id}
                {r.leavesAGap
                  ? ' · approving leaves the shift open'
                  : ` · ${r.cover.length} qualified ${r.cover.length === 1 ? 'person' : 'people'} free`}
              </div>
              <div className={s.actions}>
                <Button
                  size="small"
                  appearance="primary"
                  disabled={busy === r.id}
                  onClick={() => void act(`${base}/api/nonna/time-off/${r.id}/approve`, {}, r.id)}
                >
                  Approve
                </Button>
                <Button
                  size="small"
                  disabled={busy === r.id}
                  onClick={() =>
                    void act(`${base}/api/nonna/time-off/${r.id}/decline`, { reason: 'not this week' }, r.id)
                  }
                >
                  Decline
                </Button>
              </div>
            </div>
          ))}

          {settled.map((r) => (
            <div key={r.id} className={s.card}>
              <div className={s.line}>
                {r.name} · {r.date}
                {r.slot ? ` ${r.slot.toLowerCase()}` : ''}
              </div>
              <div className={s.why}>{r.note ?? r.state}</div>
            </div>
          ))}
        </div>

        <div className={s.column}>
          <div className={s.columnTitle}>
            {words.requisitions} — {waiting.length} to sign
            {blocked.length > 0 ? `, ${blocked.length} blocked` : ''}
          </div>

          {waiting.length === 0 && blocked.length === 0 && (
            <div className={s.quiet}>Nothing needs a signature.</div>
          )}

          {waiting.map((o) => (
            <div key={o.id} className={mergeClasses(s.card, s.cardWaiting)}>
              <div className={s.line}>
                {o.grams} g {o.ingredient} · €{o.cost.toFixed(2)} · {o.supplier}
              </div>
              <div className={s.why}>{o.why}</div>
              <div className={s.actions}>
                <Button
                  size="small"
                  appearance="primary"
                  disabled={busy === o.id}
                  onClick={() => void act(`${base}/api/nonna/purchase-orders/${o.id}/approve`, {}, o.id)}
                >
                  Approve
                </Button>
                <Button
                  size="small"
                  disabled={busy === o.id}
                  onClick={() =>
                    void act(`${base}/api/nonna/purchase-orders/${o.id}/reject`, { reason: 'not this month' }, o.id)
                  }
                >
                  Reject
                </Button>
              </div>
            </div>
          ))}

          {/* No approve button on purpose: a signature cannot conjure funds. */}
          {blocked.map((o) => (
            <div key={o.id} className={mergeClasses(s.card, s.cardBlocked)}>
              <div className={s.line}>
                {o.grams} g {o.ingredient} · €{o.cost.toFixed(2)} — refused by policy
              </div>
              <div className={s.why}>{o.why}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default BackOffice;
