import * as React from 'react';

import { Button, Input, Select, makeStyles, tokens } from '@fluentui/react-components';
import type { SPCopilotTheme } from '@microsoft/sp-copilot-component';

import type { IPreOrderEntry, ITrattoriaSnapshot } from '../../models/trattoria';
import { MENU } from '../../models/trattoria';
import type { ITrattoriaDataService } from '../../services/ITrattoriaDataService';
import { SERIF } from '../widgets/atoms';
import { PreOrderList } from '../widgets/panels';
import TrattoriaTheme from '../TrattoriaTheme';

const MAX_AMOUNT = 24;
const STORAGE_KEY = 'trattoria-preorders';

const useStyles = makeStyles({
  board: { padding: '16px 18px', maxWidth: '560px' },
  title: { margin: '0 0 4px', fontFamily: SERIF, fontSize: '20px', fontWeight: 700, color: tokens.colorNeutralForeground1 },
  blurb: { margin: '0 0 12px', fontSize: '12.5px', fontStyle: 'italic', color: tokens.colorNeutralForeground3 },
  form: { display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'center', marginBottom: '6px' },
  amount: { width: '72px' },
  name: { flexGrow: 1, minWidth: '140px' },
  result: {
    margin: '8px 0 0',
    padding: '7px 10px',
    borderLeft: '3px solid var(--tc-basil)',
    borderRadius: '0 8px 8px 0',
    backgroundColor: tokens.colorNeutralBackground2,
    fontSize: '12.5px'
  },
  resultError: { borderLeftColor: 'var(--tc-danger)' },
  book: {
    backgroundColor: 'var(--tc-tomato)',
    ':hover': { backgroundColor: 'var(--tc-tomato-deep)' },
    ':active': { backgroundColor: 'var(--tc-tomato-deep)' }
  }
});

interface IStoredPreOrder {
  pizza: string;
  amount: number;
  whenIso: string;
  name: string;
}

function loadStored(): IStoredPreOrder[] {
  try {
    return JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '[]') as IStoredPreOrder[];
  } catch {
    return [];
  }
}

function nextSaturdayAtSix(from: Date): string {
  let days: number = (6 - from.getDay() + 7) % 7;
  if (days === 0 && from.getHours() >= 18) {
    days = 7;
  }
  const d = new Date(from.getFullYear(), from.getMonth(), from.getDate() + days, 18, 0, 0);
  const pad = (n: number): string => (n < 10 ? `0${n}` : `${n}`);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export interface IPreOrderBoardProps {
  dataService: ITrattoriaDataService;
  theme?: SPCopilotTheme;
}

/** The reservation book on a SharePoint page: rehearsal entries plus locally-added ones. */
const PreOrderBoard: React.FunctionComponent<IPreOrderBoardProps> = (props) => {
  const s = useStyles();
  const [snapshot, setSnapshot] = React.useState<ITrattoriaSnapshot | undefined>(undefined);
  const [stored, setStored] = React.useState<IStoredPreOrder[]>(loadStored);
  const [pizza, setPizza] = React.useState<string>(MENU[0].name);
  const [amount, setAmount] = React.useState<number>(10);
  const [when, setWhen] = React.useState<string>(nextSaturdayAtSix(new Date()));
  const [forName, setForName] = React.useState<string>('');
  const [message, setMessage] = React.useState<string | undefined>(undefined);
  const [error, setError] = React.useState<boolean>(false);

  React.useEffect(() => {
    props.dataService.getSnapshot(new Date()).then(setSnapshot).catch(() => undefined);
  }, [props.dataService]);

  const book = (): void => {
    const parsed = new Date(when);
    if (amount < 1 || amount > MAX_AMOUNT) {
      setError(true);
      setMessage(`Amount must be between 1 and ${MAX_AMOUNT} — for more, call Giuseppe and bring a good story.`);
      return;
    }
    if (!(parsed.getTime() > Date.now())) {
      setError(true);
      setMessage('That moment has already happened. Pre-orders need a future date.');
      return;
    }
    if (!forName.trim()) {
      setError(true);
      setMessage("A name for the order, per favore — 'mystery guest' confuses the courier.");
      return;
    }

    const next: IStoredPreOrder[] = [...stored, { pizza, amount, whenIso: parsed.toISOString(), name: forName.trim() }];
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    setStored(next);
    setForName('');
    setError(false);
    setMessage(`✅ Booked — ${amount}× ${pizza} for ${forName.trim()} will fire on ${parsed.toLocaleString()}.`);
  };

  const entries: IPreOrderEntry[] = [
    ...(snapshot?.preOrders ?? []),
    ...stored.map((p) => {
      const d = new Date(p.whenIso);
      return {
        pizza: p.pizza,
        amount: p.amount,
        name: p.name,
        hoursOut: Math.round(((d.getTime() - Date.now()) / 3600000) * 10) / 10,
        whenLabel:
          d.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' }) +
          ` ${d.getHours()}:${d.getMinutes() < 10 ? '0' : ''}${d.getMinutes()}`
      };
    })
  ].sort((a, b) => a.hoursOut - b.hoursOut);

  return (
    <TrattoriaTheme theme={props.theme}>
      <div className={s.board}>
        <h2 className={s.title}>📅 Reserve ahead</h2>
        <p className={s.blurb}>Party, retro, bingo night — book your pizzas and we fire the ovens right on time.</p>
        <div className={s.form}>
          <Select value={pizza} onChange={(_, d) => setPizza(d.value)} aria-label="Pizza">
            {MENU.map((p) => (
              <option key={p.name} value={p.name}>
                {p.name}
              </option>
            ))}
          </Select>
          <Input
            className={s.amount}
            type="number"
            value={`${amount}`}
            onChange={(_, d) => setAmount(parseInt(d.value, 10) || 0)}
            aria-label="Amount"
          />
          <Input type="datetime-local" value={when} onChange={(_, d) => setWhen(d.value)} aria-label="When" />
          <Input
            className={s.name}
            placeholder="who is it for?"
            value={forName}
            onChange={(_, d) => setForName(d.value)}
          />
          <Button appearance="primary" className={s.book} onClick={book}>
            Book it 📅
          </Button>
        </div>
        {message && <p className={error ? `${s.result} ${s.resultError}` : s.result}>{message}</p>}
        <PreOrderList preOrders={entries} />
      </div>
    </TrattoriaTheme>
  );
};

export default PreOrderBoard;
