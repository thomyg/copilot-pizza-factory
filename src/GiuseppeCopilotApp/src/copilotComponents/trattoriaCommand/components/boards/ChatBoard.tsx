import * as React from 'react';

import { Button, Input, Spinner, makeStyles, tokens } from '@fluentui/react-components';
import type { SPCopilotTheme } from '@microsoft/sp-copilot-component';

import { SERIF } from '../widgets/atoms';
import TrattoriaTheme from '../TrattoriaTheme';

const useStyles = makeStyles({
  board: { display: 'flex', flexDirection: 'column', padding: '14px 16px', maxWidth: '560px' },
  header: { display: 'flex', alignItems: 'baseline', gap: '8px', marginBottom: '10px' },
  title: { margin: 0, fontFamily: SERIF, fontSize: '18px', fontWeight: 700, color: tokens.colorNeutralForeground1 },
  sub: { fontSize: '11.5px', color: tokens.colorNeutralForeground3 },
  log: { display: 'flex', flexDirection: 'column', gap: '8px', minHeight: '120px', maxHeight: '360px', overflowY: 'auto', marginBottom: '10px' },
  bubble: { maxWidth: '85%', padding: '8px 12px', borderRadius: '12px', fontSize: '13px', lineHeight: '1.4', whiteSpace: 'pre-wrap' },
  user: { alignSelf: 'flex-end', backgroundColor: 'var(--tc-tomato)', color: '#fff', borderBottomRightRadius: '3px' },
  giuseppe: {
    alignSelf: 'flex-start',
    backgroundColor: 'var(--tc-parchment)',
    color: 'var(--tc-ink)',
    borderBottomLeftRadius: '3px'
  },
  blocked: { alignSelf: 'flex-start', backgroundColor: tokens.colorNeutralBackground4, fontStyle: 'italic' },
  hint: { fontSize: '12px', fontStyle: 'italic', color: tokens.colorNeutralForeground3, margin: '0 0 8px' },
  form: { display: 'flex', gap: '8px' },
  input: { flexGrow: 1 },
  send: {
    backgroundColor: 'var(--tc-tomato)',
    ':hover': { backgroundColor: 'var(--tc-tomato-deep)' },
    ':active': { backgroundColor: 'var(--tc-tomato-deep)' }
  }
});

interface IChatLine {
  who: 'user' | 'giuseppe' | 'blocked';
  text: string;
}

export interface IChatBoardProps {
  /** POST endpoint of the factory's Giuseppe chat API, e.g. https://host/api/giuseppe/chat */
  apiUrl: string;
  theme?: SPCopilotTheme;
}

/**
 * The pro-code route: this chat talks to the REAL GiuseppeAgent — the same
 * Microsoft.Extensions.AI tool-calling agent that runs the house — over a guarded,
 * rate-limited JSON API. Not a mock, not a rehearsal: his answers come from the
 * live dining room, the reservation book, and the crystal ball.
 */
const ChatBoard: React.FunctionComponent<IChatBoardProps> = (props) => {
  const s = useStyles();
  const [lines, setLines] = React.useState<IChatLine[]>([]);
  const [draft, setDraft] = React.useState<string>('');
  const [busy, setBusy] = React.useState<boolean>(false);
  const logRef = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    logRef.current?.scrollTo({ top: logRef.current.scrollHeight });
  }, [lines]);

  const send = async (): Promise<void> => {
    const message: string = draft.trim();
    if (!message || busy) {
      return;
    }
    setDraft('');
    setBusy(true);
    setLines((prev) => [...prev, { who: 'user', text: message }]);

    try {
      const response = await fetch(props.apiUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message })
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      const data: { allowed: boolean; reply: string } = await response.json();
      setLines((prev) => [...prev, { who: data.allowed ? 'giuseppe' : 'blocked', text: data.reply }]);
    } catch {
      setLines((prev) => [
        ...prev,
        { who: 'blocked', text: 'Mamma mia — the line to the kitchen is down. Is the factory running (and its address in the web part settings)?' }
      ]);
    } finally {
      setBusy(false);
    }
  };

  return (
    <TrattoriaTheme theme={props.theme}>
      <div className={s.board}>
        <div className={s.header}>
          <h2 className={s.title}>💬 Ask Giuseppe</h2>
          <span className={s.sub}>the real one — live from the factory floor</span>
        </div>
        <div className={s.log} ref={logRef}>
          {lines.length === 0 && (
            <p className={s.hint}>
              Try &quot;How is tonight looking?&quot;, &quot;Give me the business report&quot;, or &quot;What will bite us soon?&quot;
            </p>
          )}
          {lines.map((line, i) => (
            <div
              key={i}
              className={`${s.bubble} ${line.who === 'user' ? s.user : line.who === 'giuseppe' ? s.giuseppe : s.blocked}`}
            >
              {line.text}
            </div>
          ))}
          {busy && <Spinner size="tiny" label="Giuseppe is thinking…" />}
        </div>
        <div className={s.form}>
          <Input
            className={s.input}
            value={draft}
            placeholder="Ask the pizzaiolo…"
            onChange={(_, d) => setDraft(d.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                void send();
              }
            }}
          />
          <Button appearance="primary" className={s.send} disabled={busy} onClick={() => void send()}>
            Send
          </Button>
        </div>
      </div>
    </TrattoriaTheme>
  );
};

export default ChatBoard;
