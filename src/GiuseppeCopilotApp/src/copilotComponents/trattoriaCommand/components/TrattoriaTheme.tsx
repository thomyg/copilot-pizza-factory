import * as React from 'react';

import {
  FluentProvider,
  makeStyles,
  tokens,
  webDarkTheme,
  webLightTheme
} from '@fluentui/react-components';
import type { SPCopilotTheme } from '@microsoft/sp-copilot-component';

const useStyles = makeStyles({
  provider: {
    width: '100%',
    boxSizing: 'border-box',
    minWidth: 0,
    // Paint a theme-aware surface: the Copilot host renders over its own (often
    // white) surface, so a transparent provider would bleed the wrong color in
    // dark mode.
    backgroundColor: tokens.colorNeutralBackground1
  }
});

/** FORNO ROSSO accent palette, tuned per host theme, exposed as CSS custom props. */
const ROSSO_LIGHT: Record<string, string> = {
  '--tc-tomato': '#c93a21',
  '--tc-tomato-deep': '#a02c17',
  '--tc-gold': '#b07a24',
  '--tc-basil': '#4a7c3a',
  '--tc-parchment': '#f7f0e4',
  '--tc-ink': '#2b1d16',
  '--tc-danger': '#b3261e',
  '--tc-warn': '#9a6a00',
  '--tc-ok': '#3c7a3c',
  '--tc-line': 'rgba(43, 29, 22, 0.14)'
};

const ROSSO_DARK: Record<string, string> = {
  '--tc-tomato': '#e05238',
  '--tc-tomato-deep': '#c93a21',
  '--tc-gold': '#d4a04a',
  '--tc-basil': '#7fae6e',
  '--tc-parchment': '#2a2320',
  '--tc-ink': '#f2e9dc',
  '--tc-danger': '#ef8377',
  '--tc-warn': '#e0b45c',
  '--tc-ok': '#8fc98f',
  '--tc-line': 'rgba(242, 233, 220, 0.16)'
};

export interface ITrattoriaThemeProps {
  /** Color theme advertised by the Copilot host. */
  theme?: SPCopilotTheme;
}

/**
 * Fluent UI v9 theming plus the FORNO ROSSO accent tokens.
 *
 * The Copilot host renders the component inside an iframe whose document is not
 * always ready when the tree first mounts, so Fluent's theme style insertion can
 * be lost on first load. The provider is remounted exactly once after the
 * initial commit (via a changing key) so styles apply without user interaction —
 * the same pattern Microsoft's flagship sample uses.
 */
const TrattoriaTheme: React.FunctionComponent<ITrattoriaThemeProps> = (props) => {
  const styles = useStyles();
  const { theme, children } = props;

  const [mountGeneration, setMountGeneration] = React.useState(0);
  React.useEffect(() => {
    setMountGeneration(1);
  }, []);

  const isDark: boolean = theme === 'dark';
  const rosso: Record<string, string> = isDark ? ROSSO_DARK : ROSSO_LIGHT;

  return (
    <FluentProvider
      key={mountGeneration}
      theme={isDark ? webDarkTheme : webLightTheme}
      className={styles.provider}
      style={rosso as React.CSSProperties}
    >
      {children}
    </FluentProvider>
  );
};

export default TrattoriaTheme;
