import * as React from 'react';

import { makeStyles, tokens } from '@fluentui/react-components';

import type {
  IChannelSplit,
  IDayHistory,
  IFeedItem,
  IForecastRisk,
  IPreOrderEntry,
  IStockLevel
} from '../../models/trattoria';
import { SERIF, SeverityChip } from './atoms';

const useStyles = makeStyles({
  h: {
    margin: '0 0 8px',
    fontFamily: SERIF,
    fontSize: '15px',
    fontWeight: 600,
    color: tokens.colorNeutralForeground1
  },
  line: { display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap', fontSize: '12px' },
  station: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    padding: '6px 10px',
    borderRadius: '8px',
    backgroundColor: tokens.colorNeutralBackground2,
    minWidth: '58px'
  },
  stationCount: { fontFamily: SERIF, fontSize: '18px', fontWeight: 700, color: 'var(--tc-tomato)' },
  stationLabel: { fontSize: '10px', textTransform: 'uppercase', letterSpacing: '0.05em', color: tokens.colorNeutralForeground3 },
  arrow: { color: tokens.colorNeutralForeground4 },
  stockRow: { display: 'grid', gridTemplateColumns: '92px 1fr 52px', alignItems: 'center', gap: '8px', fontSize: '12px', marginBottom: '5px' },
  gauge: { display: 'block', height: '7px', borderRadius: '4px', backgroundColor: tokens.colorNeutralBackground4, overflow: 'hidden' },
  gaugeFill: { display: 'block', height: '100%', borderRadius: '4px', transitionProperty: 'width', transitionDuration: '0.4s' },
  grams: { textAlign: 'right', color: tokens.colorNeutralForeground3, fontVariantNumeric: 'tabular-nums' },
  riskItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: '3px',
    padding: '9px 12px',
    borderRadius: '8px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderLeft: '3px solid var(--tc-line)',
    marginBottom: '7px'
  },
  riskHigh: { borderLeftColor: 'var(--tc-danger)' },
  riskMedium: { borderLeftColor: 'var(--tc-warn)' },
  riskTitle: { display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600, fontSize: '13px' },
  riskDetail: { fontSize: '12px', color: tokens.colorNeutralForeground2 },
  riskSuggestion: { fontSize: '12px', color: 'var(--tc-basil)', fontStyle: 'italic' },
  bars: { display: 'flex', alignItems: 'flex-end', gap: '6px', marginTop: '6px' },
  barCol: { display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '3px', flexGrow: 1, minWidth: 0 },
  bar: { display: 'block', width: '100%', borderRadius: '4px 4px 0 0', backgroundColor: 'var(--tc-gold)', opacity: 0.45 },
  barToday: { backgroundColor: 'var(--tc-tomato)', opacity: 1 },
  barLabel: { fontSize: '9px', color: tokens.colorNeutralForeground4, whiteSpace: 'nowrap' },
  feedItem: { display: 'flex', gap: '8px', fontSize: '12px', marginBottom: '6px', lineHeight: '1.35' },
  feedAt: { color: tokens.colorNeutralForeground4, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' },
  preItem: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'baseline',
    gap: '10px',
    padding: '7px 2px',
    borderBottom: '1px dashed var(--tc-line)',
    fontSize: '12.5px'
  },
  preWhen: { color: 'var(--tc-gold)', fontWeight: 600, whiteSpace: 'nowrap' },
  channels: { display: 'flex', gap: '6px', flexWrap: 'wrap', fontSize: '11.5px' },
  channel: {
    padding: '3px 9px',
    borderRadius: '999px',
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground2
  }
});

export const KitchenLine: React.FunctionComponent<{
  line: { ordered: number; preparing: number; baking: number; ready: number };
}> = (props) => {
  const s = useStyles();
  const stations: Array<[string, number]> = [
    ['ordered', props.line.ordered],
    ['preparing', props.line.preparing],
    ['baking', props.line.baking],
    ['ready', props.line.ready]
  ];
  return (
    <div className={s.line}>
      {stations.map(([label, count], i) => (
        <React.Fragment key={label}>
          {i > 0 && <span className={s.arrow}>→</span>}
          <span className={s.station}>
            <span className={s.stationCount}>{count}</span>
            <span className={s.stationLabel}>{label}</span>
          </span>
        </React.Fragment>
      ))}
    </div>
  );
};

export const StockBars: React.FunctionComponent<{ stock: IStockLevel[]; limit?: number }> = (props) => {
  const s = useStyles();
  const rows: IStockLevel[] = props.limit ? props.stock.slice(0, props.limit) : props.stock;
  return (
    <div>
      {rows.map((level) => {
        const pct: number = Math.min(100, Math.round((level.grams * 100) / Math.max(1, level.openingGrams)));
        const color: string =
          level.state === 'crisis' ? 'var(--tc-danger)' : level.state === 'low' ? 'var(--tc-warn)' : 'var(--tc-ok)';
        return (
          <div key={level.ingredient} className={s.stockRow}>
            <span>{level.ingredient}</span>
            <span className={s.gauge}>
              <span className={s.gaugeFill} style={{ width: `${pct}%`, backgroundColor: color }} />
            </span>
            <span className={s.grams}>{level.grams} g</span>
          </div>
        );
      })}
    </div>
  );
};

export const RiskList: React.FunctionComponent<{ risks: IForecastRisk[]; limit?: number }> = (props) => {
  const s = useStyles();
  const rows: IForecastRisk[] = props.limit ? props.risks.slice(0, props.limit) : props.risks;
  return (
    <div>
      {rows.map((risk) => (
        <div
          key={risk.title}
          className={[
            s.riskItem,
            risk.severity === 'high' ? s.riskHigh : risk.severity === 'medium' ? s.riskMedium : ''
          ].join(' ')}
        >
          <span className={s.riskTitle}>
            <SeverityChip severity={risk.severity} /> {risk.title}
          </span>
          <span className={s.riskDetail}>{risk.detail}</span>
          <span className={s.riskSuggestion}>→ {risk.suggestion}</span>
        </div>
      ))}
    </div>
  );
};

export const HistoryBars: React.FunctionComponent<{ history: IDayHistory[] }> = (props) => {
  const s = useStyles();
  const max: number = Math.max(...props.history.map((d) => d.revenue), 1);
  return (
    <div className={s.bars}>
      {props.history.map((day) => (
        <div key={day.label} className={s.barCol} title={`${day.label}: ${day.orders} orders, €${day.revenue.toFixed(0)}`}>
          <span
            className={[s.bar, day.isToday ? s.barToday : ''].join(' ')}
            style={{ height: Math.max(5, Math.round((day.revenue / max) * 56)) }}
          />
          <span className={s.barLabel}>{day.label.split(' ')[0]}</span>
        </div>
      ))}
    </div>
  );
};

export const FeedTicker: React.FunctionComponent<{ feed: IFeedItem[] }> = (props) => {
  const s = useStyles();
  return (
    <div>
      {props.feed.map((item, i) => (
        <div key={i} className={s.feedItem}>
          <span className={s.feedAt}>{item.at}</span>
          <span>{item.text}</span>
        </div>
      ))}
    </div>
  );
};

export const PreOrderList: React.FunctionComponent<{ preOrders: IPreOrderEntry[]; limit?: number }> = (props) => {
  const s = useStyles();
  const rows: IPreOrderEntry[] = props.limit ? props.preOrders.slice(0, props.limit) : props.preOrders;
  return (
    <div>
      {rows.map((p, i) => (
        <div key={i} className={s.preItem}>
          <span>
            <strong>
              {p.amount}× {p.pizza}
            </strong>{' '}
            — {p.name}
          </span>
          <span className={s.preWhen}>{p.whenLabel}</span>
        </div>
      ))}
    </div>
  );
};

export const ChannelChips: React.FunctionComponent<{ channels: IChannelSplit }> = (props) => {
  const s = useStyles();
  const entries: Array<[string, number]> = [
    ['🌐 web', props.channels.web],
    ['💬 chat', props.channels.chat],
    ['🤖 copilot', props.channels.copilot],
    ['📞 phone', props.channels.phone],
    ['🚪 walk-in', props.channels.walkIn]
  ];
  return (
    <div className={s.channels}>
      {entries.map(([label, count]) => (
        <span key={label} className={s.channel}>
          {label} {count}
        </span>
      ))}
    </div>
  );
};

export const PanelHeading: React.FunctionComponent<{ text: string }> = (props) => {
  const s = useStyles();
  return <h3 className={s.h}>{props.text}</h3>;
};
