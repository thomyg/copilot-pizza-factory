import * as React from 'react';

import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';

import type { RiskSeverity } from '../../models/trattoria';

export const SERIF = "'Fraunces', 'Iowan Old Style', 'Palatino', Georgia, serif";

const useStyles = makeStyles({
  kpi: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    padding: '10px 14px',
    borderRadius: '10px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderLeft: '3px solid var(--tc-gold)',
    minWidth: '96px'
  },
  kpiValue: {
    fontFamily: SERIF,
    fontSize: '22px',
    lineHeight: '1.1',
    fontWeight: 600,
    color: tokens.colorNeutralForeground1
  },
  kpiLabel: {
    fontSize: '11px',
    letterSpacing: '0.06em',
    textTransform: 'uppercase',
    color: tokens.colorNeutralForeground3
  },
  stars: { color: 'var(--tc-gold)', letterSpacing: '2px', whiteSpace: 'nowrap' },
  starsMuted: { color: tokens.colorNeutralForeground4 },
  chip: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    padding: '2px 10px',
    borderRadius: '999px',
    fontSize: '11px',
    fontWeight: 600,
    letterSpacing: '0.04em',
    textTransform: 'uppercase'
  },
  chipOpen: { backgroundColor: 'var(--tc-basil)', color: '#fff' },
  chipClosed: { backgroundColor: tokens.colorNeutralBackground5, color: tokens.colorNeutralForeground3 },
  sevHigh: { backgroundColor: 'var(--tc-danger)', color: '#fff' },
  sevMedium: { backgroundColor: 'var(--tc-warn)', color: '#fff' },
  sevLow: { backgroundColor: tokens.colorNeutralBackground5, color: tokens.colorNeutralForeground2 },
  note: {
    display: 'flex',
    gap: '8px',
    alignItems: 'baseline',
    padding: '8px 12px',
    borderRadius: '8px',
    backgroundColor: 'var(--tc-parchment)',
    color: 'var(--tc-ink)',
    fontFamily: SERIF,
    fontStyle: 'italic',
    fontSize: '13px',
    lineHeight: '1.35'
  },
  noteWho: { fontStyle: 'normal', fontWeight: 700, whiteSpace: 'nowrap', color: 'var(--tc-tomato)' }
});

export const KpiTile: React.FunctionComponent<{ value: string; label: string }> = (props) => {
  const s = useStyles();
  return (
    <div className={s.kpi}>
      <span className={s.kpiValue}>{props.value}</span>
      <span className={s.kpiLabel}>{props.label}</span>
    </div>
  );
};

export const Stars: React.FunctionComponent<{ value: number | undefined }> = (props) => {
  const s = useStyles();
  if (props.value === undefined) {
    return <span className={mergeClasses(s.stars, s.starsMuted)}>— no reviews yet</span>;
  }
  const full: number = Math.round(props.value);
  return (
    <span className={s.stars} title={`${props.value.toFixed(1)} stars`}>
      {'★★★★★'.slice(0, full)}
      <span className={s.starsMuted}>{'★★★★★'.slice(full)}</span> {props.value.toFixed(1)}
    </span>
  );
};

export const ServiceChip: React.FunctionComponent<{ open: boolean }> = (props) => {
  const s = useStyles();
  return (
    <span className={mergeClasses(s.chip, props.open ? s.chipOpen : s.chipClosed)}>
      {props.open ? '● Service open' : '○ Service closed'}
    </span>
  );
};

export const SeverityChip: React.FunctionComponent<{ severity: RiskSeverity }> = (props) => {
  const s = useStyles();
  const cls: string =
    props.severity === 'high' ? s.sevHigh : props.severity === 'medium' ? s.sevMedium : s.sevLow;
  return <span className={mergeClasses(s.chip, cls)}>{props.severity}</span>;
};

export const GiuseppeNote: React.FunctionComponent<{ text?: string }> = (props) => {
  const s = useStyles();
  if (!props.text) {
    return null;
  }
  return (
    <div className={s.note}>
      <span className={s.noteWho}>Giuseppe:</span>
      <span>“{props.text}”</span>
    </div>
  );
};
