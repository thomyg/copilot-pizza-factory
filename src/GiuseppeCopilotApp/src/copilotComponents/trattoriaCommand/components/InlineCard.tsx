import * as React from 'react';

import { Button, makeStyles, tokens } from '@fluentui/react-components';

import type { ITrattoriaSnapshot, ViewKey } from '../models/trattoria';
import { GiuseppeNote, KpiTile, SERIF, ServiceChip, Stars } from './widgets/atoms';
import {
  ChannelChips,
  HistoryBars,
  KitchenLine,
  PreOrderList,
  RiskList,
  StockBars
} from './widgets/panels';

const useStyles = makeStyles({
  card: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
    padding: '14px 16px',
    borderLeft: '4px solid var(--tc-tomato)',
    borderRadius: '10px',
    backgroundColor: tokens.colorNeutralBackground1
  },
  header: { display: 'flex', alignItems: 'baseline', gap: '10px', flexWrap: 'wrap' },
  title: {
    margin: 0,
    fontFamily: SERIF,
    fontSize: '18px',
    fontWeight: 700,
    color: tokens.colorNeutralForeground1
  },
  subtitle: { fontSize: '12px', color: tokens.colorNeutralForeground3 },
  kpis: { display: 'flex', gap: '8px', flexWrap: 'wrap' },
  footer: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px', flexWrap: 'wrap' },
  expand: { color: 'var(--tc-tomato)' }
});

const TITLES: Record<ViewKey, string> = {
  tonight: 'Tonight at the trattoria',
  report: "Today's business report",
  forecast: 'The crystal ball',
  preorders: 'The reservation book'
};

export interface IInlineCardProps {
  view: ViewKey;
  giuseppeSays?: string;
  snapshot: ITrattoriaSnapshot;
  canExpand: boolean;
  onExpand: () => void;
}

/** The compact card rendered inline in the Copilot conversation. */
const InlineCard: React.FunctionComponent<IInlineCardProps> = (props) => {
  const s = useStyles();
  const { view, snapshot } = props;
  const { tonight, report } = snapshot;

  return (
    <div className={s.card}>
      <div className={s.header}>
        <h2 className={s.title}>🍕 {TITLES[view]}</h2>
        <span className={s.subtitle}>Trattoria Giuseppe · {report.dateLabel}</span>
        <ServiceChip open={tonight.serviceOpen} />
      </div>

      {view === 'tonight' && (
        <>
          <div className={s.kpis}>
            <KpiTile value={`${tonight.tablesSeated}/${tonight.tablesTotal}`} label="tables" />
            <KpiTile
              value={`${tonight.line.ordered + tonight.line.preparing + tonight.line.baking + tonight.line.ready}`}
              label="pies in flight"
            />
            <KpiTile value={`${tonight.guestsServed}`} label="guests served" />
          </div>
          <KitchenLine line={tonight.line} />
          <Stars value={tonight.averageStars} />
          <StockBars stock={tonight.stock.filter((l) => l.state !== 'ok')} limit={2} />
        </>
      )}

      {view === 'report' && (
        <>
          <div className={s.kpis}>
            <KpiTile value={`€${report.revenueToday.toFixed(0)}`} label="revenue today" />
            <KpiTile value={`${report.ordersToday}`} label="orders" />
            <KpiTile value={`€${report.paceProjection.toFixed(0)}`} label="pace projection" />
            <KpiTile value={report.topPizza} label="top pizza" />
          </div>
          <HistoryBars history={report.history} />
          <ChannelChips channels={report.channels} />
        </>
      )}

      {view === 'forecast' && <RiskList risks={snapshot.risks} limit={2} />}

      {view === 'preorders' && <PreOrderList preOrders={snapshot.preOrders} limit={3} />}

      <div className={s.footer}>
        <GiuseppeNote text={props.giuseppeSays} />
        {props.canExpand && (
          <Button appearance="transparent" className={s.expand} onClick={props.onExpand}>
            Apri il war room →
          </Button>
        )}
      </div>
    </div>
  );
};

export default InlineCard;
