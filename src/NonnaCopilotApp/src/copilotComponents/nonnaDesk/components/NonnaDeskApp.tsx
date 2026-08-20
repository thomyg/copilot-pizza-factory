import * as React from 'react';

import {
  FluentProvider,
  makeStyles,
  mergeClasses,
  tokens,
  webDarkTheme,
  webLightTheme
} from '@fluentui/react-components';
import type { SPCopilotDisplayMode, SPCopilotTheme } from '@microsoft/sp-copilot-component';

import type { DeskView, IBackOfficeSnapshot } from '../RehearsalBackOffice';

const SERIF = "'Fraunces', 'Iowan Old Style', 'Palatino', Georgia, serif";

/** FORNO ROSSO tokens, Nonna's cut: basil leads, gold keeps the books, tomato stays for alarms. */
const ROSSO_LIGHT: Record<string, string> = {
  '--nd-basil': '#4a7c3a', '--nd-gold': '#b07a24', '--nd-tomato': '#c93a21',
  '--nd-parchment': '#f7f0e4', '--nd-ink': '#2b1d16', '--nd-warn': '#9a6a00',
  '--nd-line': 'rgba(43, 29, 22, 0.14)'
};
const ROSSO_DARK: Record<string, string> = {
  '--nd-basil': '#7fae6e', '--nd-gold': '#d4a04a', '--nd-tomato': '#e05238',
  '--nd-parchment': '#2a2320', '--nd-ink': '#f2e9dc', '--nd-warn': '#e0b45c',
  '--nd-line': 'rgba(242, 233, 220, 0.16)'
};

const useStyles = makeStyles({
  provider: { width: '100%', boxSizing: 'border-box', minWidth: 0, backgroundColor: tokens.colorNeutralBackground1 },
  card: {
    display: 'flex', flexDirection: 'column', gap: '10px', padding: '14px 16px',
    borderLeft: '4px solid var(--nd-basil)', borderRadius: '10px', backgroundColor: tokens.colorNeutralBackground1
  },
  full: { padding: '20px 22px' },
  header: { display: 'flex', alignItems: 'baseline', gap: '10px', flexWrap: 'wrap' },
  title: { margin: 0, fontFamily: SERIF, fontSize: '18px', fontWeight: 700, color: tokens.colorNeutralForeground1 },
  titleBig: { fontSize: '24px' },
  sub: { fontSize: '12px', color: tokens.colorNeutralForeground3 },
  h: { margin: '8px 0 6px', fontFamily: SERIF, fontSize: '14.5px', fontWeight: 600, color: tokens.colorNeutralForeground1 },
  po: {
    display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap', padding: '9px 12px',
    borderRadius: '8px', borderLeft: '3px solid var(--nd-warn)',
    backgroundColor: tokens.colorNeutralBackground2, marginBottom: '6px', fontSize: '12.5px'
  },
  cost: { fontFamily: SERIF, fontWeight: 700, color: 'var(--nd-gold)' },
  note: { fontSize: '11.5px', color: tokens.colorNeutralForeground3, fontStyle: 'italic', width: '100%' },
  rotaRow: { display: 'flex', gap: '6px', flexWrap: 'wrap', marginBottom: '5px', fontSize: '12px', alignItems: 'baseline' },
  rotaWhen: { minWidth: '110px', color: tokens.colorNeutralForeground3 },
  seat: { padding: '2px 9px', borderRadius: '999px', backgroundColor: tokens.colorNeutralBackground3 },
  seatOpen: { backgroundColor: 'var(--nd-tomato)', color: '#fff', fontWeight: 600 },
  invoice: {
    display: 'flex', justifyContent: 'space-between', gap: '8px', fontSize: '12px',
    padding: '4px 2px', borderBottom: '1px dashed var(--nd-line)'
  },
  total: { textAlign: 'right', marginTop: '6px', fontFamily: SERIF, fontWeight: 700, color: 'var(--nd-gold)' },
  says: {
    display: 'flex', gap: '8px', alignItems: 'baseline', padding: '8px 12px', borderRadius: '8px',
    backgroundColor: 'var(--nd-parchment)', color: 'var(--nd-ink)',
    fontFamily: SERIF, fontStyle: 'italic', fontSize: '13px'
  },
  saysWho: { fontStyle: 'normal', fontWeight: 700, whiteSpace: 'nowrap', color: 'var(--nd-basil)' },
  grid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '14px', alignItems: 'start' },
  panel: {
    padding: '12px 14px', borderRadius: '10px', backgroundColor: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow2, borderTop: '1px solid var(--nd-line)'
  },
  spotlight: { boxShadow: '0 0 0 1.5px var(--nd-basil)' }
});

const TITLES: Record<DeskView, string> = {
  rota: 'The rota',
  approvals: 'Waiting for a signature',
  invoices: 'The books'
};

export interface INonnaDeskAppProps {
  view: DeskView;
  nonnaSays?: string;
  snapshot: IBackOfficeSnapshot;
  theme?: SPCopilotTheme;
  displayMode?: SPCopilotDisplayMode;
}

const NonnaDeskApp: React.FunctionComponent<INonnaDeskAppProps> = (props) => {
  const s = useStyles();
  const isDark: boolean = props.theme === 'dark';
  const { snapshot, view } = props;
  const fullscreen: boolean = props.displayMode === 'fullscreen';

  const rotaGroups: Array<{ when: string; seats: typeof snapshot.rota }> = [];
  for (const entry of snapshot.rota) {
    const when = `${entry.dayLabel} ${entry.slot}`;
    const existing = rotaGroups.filter((g) => g.when === when)[0];
    if (existing) {
      existing.seats.push(entry);
    } else {
      rotaGroups.push({ when, seats: [entry] });
    }
  }

  const pending = snapshot.orders.filter((o) => o.state === 'PendingApproval');

  const rotaPanel = (
    <div className={mergeClasses(s.panel, fullscreen && view === 'rota' ? s.spotlight : undefined)}>
      <h3 className={s.h}>📋 The rota — today &amp; tomorrow</h3>
      {rotaGroups.slice(0, fullscreen ? undefined : 2).map((group) => (
        <div key={group.when} className={s.rotaRow}>
          <span className={s.rotaWhen}>{group.when}</span>
          {group.seats.map((seat, i) => (
            <span key={i} className={mergeClasses(s.seat, seat.assignedTo ? undefined : s.seatOpen)}>
              {seat.role}: {seat.assignedTo ?? 'OPEN'}
            </span>
          ))}
        </div>
      ))}
      <p className={s.note}>{snapshot.absentToday} is out today — the open seat needs cover.</p>
    </div>
  );

  const approvalsPanel = (
    <div className={mergeClasses(s.panel, fullscreen && view === 'approvals' ? s.spotlight : undefined)}>
      <h3 className={s.h}>✍️ Waiting for a signature</h3>
      {pending.length === 0 && <p className={s.note}>Nothing pending. A rare moment.</p>}
      {pending.map((o) => (
        <div key={o.id} className={s.po}>
          <strong>{o.id}</strong>
          <span>{o.grams} g {o.ingredient} · {o.supplier}</span>
          <span className={s.cost}>€{o.cost.toFixed(2)}</span>
          <span className={s.note}>{o.note}</span>
        </div>
      ))}
    </div>
  );

  const invoicesPanel = (
    <div className={mergeClasses(s.panel, fullscreen && view === 'invoices' ? s.spotlight : undefined)}>
      <h3 className={s.h}>💶 The books</h3>
      {snapshot.invoices.map((i) => (
        <div key={i.id} className={s.invoice}>
          <span>{i.id} · {i.grams} g {i.ingredient} · {i.supplier}</span>
          <span className={s.cost}>€{i.cost.toFixed(2)}</span>
        </div>
      ))}
      <p className={s.total}>Total on file: €{snapshot.invoiceTotal.toFixed(2)}</p>
    </div>
  );

  const spotlit: React.ReactElement =
    view === 'rota' ? rotaPanel : view === 'approvals' ? approvalsPanel : invoicesPanel;

  return (
    <FluentProvider
      theme={isDark ? webDarkTheme : webLightTheme}
      className={s.provider}
      style={(isDark ? ROSSO_DARK : ROSSO_LIGHT) as React.CSSProperties}
    >
      <div className={mergeClasses(s.card, fullscreen ? s.full : undefined)}>
        <div className={s.header}>
          <h2 className={mergeClasses(s.title, fullscreen ? s.titleBig : undefined)}>
            🧾 {fullscreen ? "Nonna's Desk" : TITLES[view]}
          </h2>
          <span className={s.sub}>TrattoriaSoft ERP 3000 · she sees everything</span>
        </div>

        {props.nonnaSays && (
          <div className={s.says}>
            <span className={s.saysWho}>Nonna:</span>
            <span>“{props.nonnaSays}”</span>
          </div>
        )}

        {fullscreen ? (
          <div className={s.grid}>
            {spotlit}
            {view !== 'approvals' && approvalsPanel}
            {view !== 'rota' && rotaPanel}
            {view !== 'invoices' && invoicesPanel}
          </div>
        ) : (
          spotlit
        )}
      </div>
    </FluentProvider>
  );
};

export default NonnaDeskApp;
