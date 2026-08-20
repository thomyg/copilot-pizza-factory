import { makeStaticStyles } from '@fluentui/react-components';

import { FRAUNCES_WOFF2_BASE64 } from './fraunces';

/**
 * FORNO ROSSO — the design language of the Copilot Pizza Factory, ported to SPFx.
 *
 * The glow of the wood fire: molten tomato red is the hero, burning over warm
 * charred surfaces, with flour-dusted creams and crust gold for highlights.
 * These values are the same tokens the factory's own `wwwroot/app.css` defines;
 * keep them in sync when the brand moves.
 */
export const FORNO = {
  char950: '#140f0b',
  char900: '#1d1611',
  char800: '#281e16',
  char700: '#38291d',

  flour50: '#fdfaf1',
  flour100: '#f8f1e0',
  flour200: '#efe3c8',
  flour300: '#e2d1ac',
  flourMuted: '#a2917a',

  tomato: '#c93a21',
  tomatoBright: '#e8562f',
  tomatoDeep: '#8f2415',
  tomatoGlow: '#ff7a45',

  gold: '#d9a13f',
  goldPale: '#f0dda9',

  basil: '#7fae6e'
} as const;

/** The artisanal-food display serif. Karla is not bundled; UI text rides the host's sans. */
export const DISPLAY = "'Fraunces', 'Iowan Old Style', Palatino, Georgia, serif";

/**
 * Grain over char — the texture that keeps a dark surface from looking like a
 * flat rectangle. Inline SVG noise, so it costs no request.
 */
export const GRAIN =
  "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='160' height='160'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='2'/%3E%3CfeColorMatrix values='0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0.35 0'/%3E%3C/filter%3E%3Crect width='160' height='160' filter='url(%23n)'/%3E%3C/svg%3E\")";

/**
 * Registers the @font-face once per page. Call the hook from any component that
 * needs the display serif — griffel de-duplicates, so several web parts on one
 * page still inject a single rule.
 */
export const useFornoFonts = makeStaticStyles({
  '@font-face': {
    fontFamily: 'Fraunces',
    fontStyle: 'normal',
    fontWeight: '300 900',
    fontDisplay: 'swap',
    src: `url(data:font/woff2;base64,${FRAUNCES_WOFF2_BASE64}) format('woff2')`
  }
});
