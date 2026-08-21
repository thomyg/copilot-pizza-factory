import * as React from 'react';

import { makeStyles, mergeClasses, shorthands } from '@fluentui/react-components';

import { DISPLAY, FORNO, GRAIN, useFornoFonts } from '../../../brand/forno';
import { FACTORY_API_BASE, type FactoryHttp } from '../../../factoryApi';

export interface IHeroLink {
  label: string;
  hint: string;
  url: string;
}

export interface ITrattoriaHeroProps {
  eyebrow: string;
  headline: string;
  lede: string;
  links: ReadonlyArray<IHeroLink>;
  /** Omit to render the hero without live numbers (e.g. in a workbench). */
  http?: FactoryHttp;
  apiBase?: string;
}

/** Only the handful of numbers the hero shows — deliberately not the whole snapshot. */
interface IHeroStats {
  /** The window, not the dining room: is the house trading at all right now. */
  windowOpen: boolean;
  minutesLeft: number;
  everRan: boolean;
  serviceOpen: boolean;
  tablesSeated: number;
  tablesTotal: number;
  ordersToday: number;
  revenueToday: number;
  averageStars: number | undefined;
}

const useStyles = makeStyles({
  stage: {
    position: 'relative',
    overflow: 'hidden',
    boxSizing: 'border-box',
    borderRadius: '16px',
    padding: '54px 44px 44px',
    color: FORNO.flour100,
    backgroundColor: FORNO.char900,
    backgroundImage: [
      `radial-gradient(120% 140% at 12% -20%, ${FORNO.tomatoDeep}cc 0%, transparent 58%)`,
      `radial-gradient(90% 120% at 88% 0%, ${FORNO.tomato}66 0%, transparent 55%)`,
      `linear-gradient(168deg, ${FORNO.char800} 0%, ${FORNO.char950} 100%)`
    ].join(', '),
    '@media (max-width: 720px)': { padding: '34px 22px 30px' }
  },
  grain: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    backgroundImage: GRAIN,
    opacity: 0.5,
    pointerEvents: 'none'
  },
  /** The ember line along the top edge — the oven mouth. */
  ember: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    height: '3px',
    backgroundImage: `linear-gradient(90deg, transparent, ${FORNO.tomatoGlow}, ${FORNO.gold}, ${FORNO.tomato}, transparent)`
  },
  inner: { position: 'relative', maxWidth: '980px' },

  eyebrow: {
    margin: 0,
    fontSize: '12px',
    fontWeight: 700,
    letterSpacing: '0.18em',
    textTransform: 'uppercase',
    color: FORNO.gold
  },
  headline: {
    fontFamily: DISPLAY,
    fontWeight: 600,
    fontSize: 'clamp(34px, 5.2vw, 64px)',
    lineHeight: 1.04,
    letterSpacing: '-0.015em',
    margin: '14px 0 0',
    color: FORNO.flour50,
    textWrap: 'balance'
  },
  lede: {
    margin: '18px 0 0',
    maxWidth: '62ch',
    fontSize: 'clamp(15px, 1.5vw, 18px)',
    lineHeight: 1.62,
    color: FORNO.flour300
  },

  statRow: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '10px',
    margin: '30px 0 0',
    alignItems: 'stretch'
  },
  stat: {
    minWidth: '116px',
    padding: '11px 16px 12px',
    borderRadius: '11px',
    backgroundColor: 'rgba(253, 250, 241, 0.06)',
    ...shorthands.borderLeft('2px', 'solid', FORNO.gold)
  },
  statValue: {
    fontFamily: DISPLAY,
    fontWeight: 600,
    fontSize: '25px',
    lineHeight: 1.1,
    color: FORNO.flour50
  },
  statLabel: {
    marginTop: '3px',
    fontSize: '10.5px',
    fontWeight: 700,
    letterSpacing: '0.13em',
    textTransform: 'uppercase',
    color: FORNO.flourMuted
  },
  pill: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '7px',
    alignSelf: 'center',
    padding: '7px 14px',
    borderRadius: '999px',
    fontSize: '11px',
    fontWeight: 700,
    letterSpacing: '0.11em',
    textTransform: 'uppercase'
  },
  pillOpen: { backgroundColor: `${FORNO.basil}2e`, color: FORNO.basil },
  pillShut: { backgroundColor: 'rgba(253, 250, 241, 0.09)', color: FORNO.flourMuted },
  dot: { width: '7px', height: '7px', borderRadius: '999px', backgroundColor: 'currentcolor' },

  action: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '9px',
    marginTop: '26px',
    padding: '13px 22px',
    borderRadius: '11px',
    cursor: 'pointer',
    fontWeight: 700,
    fontSize: '14.5px',
    color: FORNO.flour50,
    backgroundColor: FORNO.tomato,
    ...shorthands.border('1px', 'solid', FORNO.tomatoBright),
    transitionProperty: 'background-color, transform',
    transitionDuration: '140ms',
    ':hover': { backgroundColor: FORNO.tomatoBright, transform: 'translateY(-1px)' },
    ':disabled': { opacity: 0.55, cursor: 'default', transform: 'none' },
    ':focus-visible': { outline: `2px solid ${FORNO.gold}`, outlineOffset: '2px' }
  },
  actionHint: {
    marginTop: '10px',
    fontSize: '12.5px',
    color: FORNO.flourMuted,
    maxWidth: '52ch',
    lineHeight: 1.5
  },

  links: { display: 'flex', flexWrap: 'wrap', gap: '10px', margin: '30px 0 0' },
  link: {
    display: 'block',
    textDecoration: 'none',
    padding: '12px 17px',
    borderRadius: '11px',
    ...shorthands.border('1px', 'solid', 'rgba(240, 221, 169, 0.24)'),
    color: FORNO.flour100,
    transitionProperty: 'background-color, border-color, transform',
    transitionDuration: '140ms',
    ':hover': {
      backgroundColor: `${FORNO.tomato}2a`,
      ...shorthands.borderColor(FORNO.tomatoBright),
      transform: 'translateY(-1px)'
    },
    ':focus-visible': { outline: `2px solid ${FORNO.gold}`, outlineOffset: '2px' }
  },
  linkLabel: { fontWeight: 700, fontSize: '14px' },
  linkHint: { marginTop: '2px', fontSize: '11.5px', color: FORNO.flourMuted },

  note: {
    margin: '24px 0 0',
    fontFamily: DISPLAY,
    fontStyle: 'italic',
    fontSize: '14px',
    color: FORNO.goldPale,
    opacity: 0.85
  }
});

/** One number, in the house's own hand. */
const Stat: React.FunctionComponent<{ value: string; label: string }> = ({ value, label }) => {
  const s = useStyles();
  return (
    <div className={s.stat}>
      <div className={s.statValue}>{value}</div>
      <div className={s.statLabel}>{label}</div>
    </div>
  );
};

/**
 * The front door of the demo: FORNO ROSSO over char, with tonight's real numbers
 * burning in the corner. The stats are the point — a hero that quotes the running
 * factory proves the claim in its own headline before anyone scrolls.
 *
 * If the factory cannot be reached the hero still stands; it just drops the
 * numbers rather than showing zeros, because a hero lying about revenue is worse
 * than a hero staying quiet.
 */
const TrattoriaHero: React.FunctionComponent<ITrattoriaHeroProps> = (props) => {
  useFornoFonts();
  const s = useStyles();
  const [stats, setStats] = React.useState<IHeroStats | undefined>(undefined);

  const base: string = props.apiBase ?? FACTORY_API_BASE;
  const http: FactoryHttp | undefined = props.http;

  React.useEffect(() => {
    if (!http) {
      return undefined;
    }

    let cancelled: boolean = false;

    const pull = async (): Promise<void> => {
      try {
        const snap = await http.getJson<{
          service?: { open: boolean; minutesLeft: number; everRan: boolean };
          tonight: {
            serviceOpen: boolean;
            tablesSeated: number;
            tablesTotal: number;
            averageStars: number | null;
          };
          report: { ordersToday: number; revenueToday: number };
        }>(`${base}/api/trattoria/snapshot`);

        if (!cancelled) {
          setStats({
            windowOpen: snap.service?.open ?? snap.tonight.serviceOpen,
            minutesLeft: snap.service?.minutesLeft ?? 0,
            everRan: snap.service?.everRan ?? true,
            serviceOpen: snap.tonight.serviceOpen,
            tablesSeated: snap.tonight.tablesSeated,
            tablesTotal: snap.tonight.tablesTotal,
            ordersToday: snap.report.ordersToday,
            revenueToday: snap.report.revenueToday,
            averageStars: snap.tonight.averageStars ?? undefined
          });
        }
      } catch {
        /* Stay quiet: no numbers beats invented numbers. */
      }
    };

    void pull();
    const timer: number = window.setInterval(() => void pull(), 15000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [http, base]);

  const [opening, setOpening] = React.useState<boolean>(false);

  const openService = (): void => {
    if (!http || opening) {
      return;
    }
    setOpening(true);
    http
      .postJson<unknown>(`${base}/api/trattoria/service/open`, {})
      .catch(() => {
        /* The next poll shows whether it took; a dead button beats a lying one. */
      })
      .then(() => setOpening(false))
      .catch(() => setOpening(false));
  };

  const between: boolean = stats !== undefined && !stats.windowOpen;

  return (
    <div className={s.stage}>
      <div className={s.ember} />
      <div className={s.grain} />
      <div className={s.inner}>
        <p className={s.eyebrow}>{props.eyebrow}</p>
        <h1 className={s.headline}>{props.headline}</h1>
        <p className={s.lede}>{props.lede}</p>

        {stats && (
          <div className={s.statRow}>
            <span className={mergeClasses(s.pill, stats.windowOpen ? s.pillOpen : s.pillShut)}>
              <span className={s.dot} />
              {stats.windowOpen
                ? `Service open · ${Math.max(0, Math.round(stats.minutesLeft))} min left`
                : 'Between services'}
            </span>
            <Stat value={`${stats.tablesSeated}/${stats.tablesTotal}`} label="Tables seated" />
            <Stat value={String(stats.ordersToday)} label="Orders today" />
            <Stat value={`€${Math.round(stats.revenueToday)}`} label="Revenue today" />
            {stats.averageStars !== undefined && (
              <Stat value={stats.averageStars.toFixed(1)} label="Guest rating" />
            )}
          </div>
        )}

        {between && (
          <div>
            <button className={s.action} onClick={openService} disabled={opening} type="button">
              {opening ? 'Opening…' : '▶  Open the service'}
            </button>
            <div className={s.actionHint}>
              The house is closed between demos, so nothing runs and nothing accrues. Opening it
              starts the real factory for fifteen minutes — after that it shuts itself, and the
              service is written into the books.
            </div>
          </div>
        )}

        {props.links.length > 0 && (
          <div className={s.links}>
            {props.links.map((l: IHeroLink) => (
              <a key={l.url} className={s.link} href={l.url} target="_blank" rel="noreferrer">
                <div className={s.linkLabel}>{l.label}</div>
                <div className={s.linkHint}>{l.hint}</div>
              </a>
            ))}
          </div>
        )}

        <p className={s.note}>
          {!stats
            ? '— the kitchen is warming up; numbers appear the moment it answers.'
            : stats.windowOpen
              ? '— and every number above moved while you were reading this.'
              : stats.everRan
                ? '— the numbers above are the last service, closed and booked.'
                : '— the pantry is stocked and the book is full; nobody has opened the doors yet.'}
        </p>
      </div>
    </div>
  );
};

export default TrattoriaHero;
