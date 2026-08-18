import * as React from 'react';

import { makeStyles, tokens } from '@fluentui/react-components';
import type { SPCopilotTheme } from '@microsoft/sp-copilot-component';

import type { ITrattoriaSnapshot } from '../../models/trattoria';
import { MENU } from '../../models/trattoria';
import type { ITrattoriaDataService } from '../../services/ITrattoriaDataService';
import { pizzaAvailability } from '../../services/RehearsalTrattoriaService';
import { SERIF } from '../widgets/atoms';
import TrattoriaTheme from '../TrattoriaTheme';

const TOPPINGS: Record<string, string> = {
  Margherita: 'Tomato sauce · Mozzarella',
  Diavolo: 'Tomato sauce · Mozzarella · Salami',
  Hawaii: 'Tomato sauce · Mozzarella · Ham · Pineapple',
  Prosciutto: 'Tomato sauce · Mozzarella · Ham',
  Funghi: 'Tomato sauce · Mozzarella · Mushroom',
  'Al Tonno': 'Tomato sauce · Mozzarella · Tuna'
};

const useStyles = makeStyles({
  board: { padding: '18px 20px', maxWidth: '520px' },
  eyebrow: {
    margin: 0,
    fontSize: '11px',
    letterSpacing: '0.28em',
    textTransform: 'uppercase',
    color: 'var(--tc-gold)',
    textAlign: 'center'
  },
  title: {
    margin: '4px 0 14px',
    fontFamily: SERIF,
    fontSize: '24px',
    fontWeight: 700,
    textAlign: 'center',
    color: tokens.colorNeutralForeground1
  },
  item: { marginBottom: '13px' },
  line: { display: 'flex', alignItems: 'baseline', gap: '8px' },
  name: { fontFamily: SERIF, fontSize: '16px', fontWeight: 600, whiteSpace: 'nowrap' },
  dots: {
    flexGrow: 1,
    borderBottom: '2px dotted var(--tc-gold)',
    opacity: 0.55,
    transform: 'translateY(-4px)'
  },
  price: { fontFamily: SERIF, fontSize: '15px', fontWeight: 600, color: 'var(--tc-gold)', whiteSpace: 'nowrap' },
  sub: { fontSize: '12px', color: tokens.colorNeutralForeground3, marginTop: '2px' },
  fire: { fontSize: '10.5px', letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--tc-tomato)' },
  badge: {
    display: 'inline-block',
    marginLeft: '8px',
    padding: '1px 8px',
    borderRadius: '999px',
    fontSize: '10px',
    fontWeight: 700,
    letterSpacing: '0.05em',
    textTransform: 'uppercase'
  },
  badgeLow: { backgroundColor: 'var(--tc-warn)', color: '#fff' },
  badgeOut: { backgroundColor: 'var(--tc-danger)', color: '#fff' },
  out: { opacity: 0.5 },
  footer: {
    marginTop: '10px',
    textAlign: 'center',
    fontSize: '11.5px',
    fontStyle: 'italic',
    color: tokens.colorNeutralForeground3
  }
});

export interface IMenuBoardProps {
  dataService: ITrattoriaDataService;
  theme?: SPCopilotTheme;
}

/** The canteen play: the house menu with live pantry-derived availability badges. */
const MenuBoard: React.FunctionComponent<IMenuBoardProps> = (props) => {
  const s = useStyles();
  const [snapshot, setSnapshot] = React.useState<ITrattoriaSnapshot | undefined>(undefined);

  React.useEffect(() => {
    props.dataService.getSnapshot(new Date()).then(setSnapshot).catch(() => undefined);
  }, [props.dataService]);

  const availability: Record<string, 'ok' | 'low' | 'out'> = snapshot
    ? pizzaAvailability(snapshot.tonight.stock)
    : {};

  return (
    <TrattoriaTheme theme={props.theme}>
      <div className={s.board}>
        <p className={s.eyebrow}>Il menù</p>
        <h2 className={s.title}>Six pizzas. No compromises.</h2>
        {MENU.map((pizza) => {
          const state: 'ok' | 'low' | 'out' = availability[pizza.name] ?? 'ok';
          return (
            <div key={pizza.name} className={state === 'out' ? `${s.item} ${s.out}` : s.item}>
              <div className={s.line}>
                <span className={s.name}>{pizza.name}</span>
                {state === 'low' && <span className={`${s.badge} ${s.badgeLow}`}>running low</span>}
                {state === 'out' && <span className={`${s.badge} ${s.badgeOut}`}>86&apos;d</span>}
                <span className={s.dots} />
                <span className={s.price}>€{pizza.price.toFixed(2)}</span>
              </div>
              <div className={s.sub}>
                {TOPPINGS[pizza.name]} · <span className={s.fire}>🔥 90 sec in the fire</span>
              </div>
            </div>
          );
        })}
        <p className={s.footer}>
          Badges come straight from the pantry — when the pineapple runs out, the menu says so before the kitchen has to.
        </p>
      </div>
    </TrattoriaTheme>
  );
};

export default MenuBoard;
