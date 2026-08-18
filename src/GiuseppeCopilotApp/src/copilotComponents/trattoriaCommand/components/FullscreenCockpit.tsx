import * as React from 'react';

import { makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components';

import type { ITrattoriaSnapshot, ViewKey } from '../models/trattoria';
import { GiuseppeNote, KpiTile, SERIF, ServiceChip, Stars } from './widgets/atoms';
import {
  ChannelChips,
  FeedTicker,
  HistoryBars,
  KitchenLine,
  PanelHeading,
  PreOrderList,
  RiskList,
  StockBars
} from './widgets/panels';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: '16px',
    padding: '20px 22px',
    boxSizing: 'border-box',
    minHeight: '100%'
  },
  hero: { display: 'flex', alignItems: 'baseline', gap: '14px', flexWrap: 'wrap' },
  heroTitle: {
    margin: 0,
    fontFamily: SERIF,
    fontSize: '26px',
    fontWeight: 700,
    color: tokens.colorNeutralForeground1
  },
  heroRule: {
    height: '3px',
    width: '52px',
    borderRadius: '2px',
    backgroundColor: 'var(--tc-gold)',
    alignSelf: 'center'
  },
  heroSub: { fontSize: '13px', color: tokens.colorNeutralForeground3 },
  kpiBand: { display: 'flex', gap: '10px', flexWrap: 'wrap' },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
    gap: '14px',
    alignItems: 'start'
  },
  panel: {
    padding: '14px 16px',
    borderRadius: '10px',
    backgroundColor: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow2,
    ...shorthands.border('1px', 'solid', 'var(--tc-line)')
  },
  spotlight: {
    ...shorthands.borderColor('var(--tc-tomato)'),
    boxShadow: '0 0 0 1px var(--tc-tomato)'
  }
});

export interface IFullscreenCockpitProps {
  view: ViewKey;
  giuseppeSays?: string;
  snapshot: ITrattoriaSnapshot;
}

/** The war room: everything at once, with the asked-for view under the spotlight. */
const FullscreenCockpit: React.FunctionComponent<IFullscreenCockpitProps> = (props) => {
  const s = useStyles();
  const { view, snapshot } = props;
  const { tonight, report } = snapshot;

  const spot = (key: ViewKey): string =>
    mergeClasses(s.panel, view === key ? s.spotlight : undefined);

  const panels: Array<{ key: ViewKey | 'service' | 'stock' | 'feed'; node: React.ReactElement }> = [
    {
      key: 'tonight',
      node: (
        <div key="tonight" className={spot('tonight')}>
          <PanelHeading text="🔥 The line, right now" />
          <KitchenLine line={tonight.line} />
          <div style={{ marginTop: 10 }}>
            <Stars value={tonight.averageStars} />
          </div>
          <div style={{ marginTop: 10 }}>
            <ChannelChips channels={tonight.channels} />
          </div>
        </div>
      )
    },
    {
      key: 'forecast',
      node: (
        <div key="forecast" className={spot('forecast')}>
          <PanelHeading text="🔮 The crystal ball — what bites us next" />
          <RiskList risks={snapshot.risks} />
        </div>
      )
    },
    {
      key: 'report',
      node: (
        <div key="report" className={spot('report')}>
          <PanelHeading text="📈 Seven days of honest numbers" />
          <HistoryBars history={report.history} />
          <div style={{ marginTop: 8, fontSize: 12 }}>
            Top pizza today: <strong>{report.topPizza}</strong> · pace projection{' '}
            <strong>€{report.paceProjection.toFixed(0)}</strong>
          </div>
        </div>
      )
    },
    {
      key: 'preorders',
      node: (
        <div key="preorders" className={spot('preorders')}>
          <PanelHeading text="📅 The reservation book" />
          <PreOrderList preOrders={snapshot.preOrders} />
        </div>
      )
    },
    {
      key: 'stock',
      node: (
        <div key="stock" className={s.panel}>
          <PanelHeading text="🧺 The pantry" />
          <StockBars stock={tonight.stock} />
        </div>
      )
    },
    {
      key: 'feed',
      node: (
        <div key="feed" className={s.panel}>
          <PanelHeading text="🗞️ From the floor" />
          <FeedTicker feed={tonight.feed} />
        </div>
      )
    }
  ];

  // The spotlighted view leads the grid.
  panels.sort((a, b) => (a.key === view ? -1 : b.key === view ? 1 : 0));

  return (
    <div className={s.root}>
      <div className={s.hero}>
        <h1 className={s.heroTitle}>Trattoria Command</h1>
        <span className={s.heroRule} />
        <span className={s.heroSub}>
          {report.dateLabel} · {tonight.tablesSeated}/{tonight.tablesTotal} tables ·{' '}
          {tonight.guestsServed} guests served
        </span>
        <ServiceChip open={tonight.serviceOpen} />
      </div>

      <div className={s.kpiBand}>
        <KpiTile value={`€${report.revenueToday.toFixed(0)}`} label="revenue today" />
        <KpiTile value={`${report.ordersToday}`} label="orders" />
        <KpiTile value={`${report.pizzasToday}`} label="pizzas" />
        <KpiTile value={`€${report.paceProjection.toFixed(0)}`} label="pace projection" />
        <KpiTile
          value={`${tonight.line.ordered + tonight.line.preparing + tonight.line.baking + tonight.line.ready}`}
          label="pies in flight"
        />
      </div>

      <GiuseppeNote text={props.giuseppeSays} />

      <div className={s.grid}>{panels.map((p) => p.node)}</div>
    </div>
  );
};

export default FullscreenCockpit;
