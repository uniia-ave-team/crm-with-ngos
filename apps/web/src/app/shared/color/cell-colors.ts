import { contrast, contrastWithBlack, contrastWithWhite, darkenUntil, lightenUntil, parseHex, toHex } from './contrast';

/** Найменший контраст тексту з тлом клітинки (WCAG AAA для звичайного тексту). */
export const CELL_TEXT_CONTRAST = 7;

/**
 * Колір тексту для тла клітинки: той самий відтінок, що й тло, лише темніший (на світлому тлі) чи
 * світліший (на темному) — рівно настільки, щоб контраст із тлом був не менше 7:1. Тож текст на
 * рожевому — темно-рожевий, на блакитному — темно-синій. Де 7:1 недосяжний навіть із чорним чи білим
 * (насичені середні кольори, як чистий червоний), — той із них, що дає більший контраст. Колір тексту
 * не зберігається: він рахується з тла щоразу.
 */
export function cellTextColor(background: string): string {
  const color = parseHex(background);
  const enough = (candidate: readonly [number, number, number]) => contrast(candidate, color) >= CELL_TEXT_CONTRAST;
  const onLight = contrastWithBlack(color) >= contrastWithWhite(color);
  return toHex(onLight ? darkenUntil(color, enough) : lightenUntil(color, enough));
}

/** Перевірка HEX-кольору з поля: `#rrggbb` чи `rrggbb` → `#rrggbb` (малими), інше — `null`. */
export function normalizeHex(value: string): string | null {
  const match = /^#?([0-9a-f]{6})$/i.exec(value.trim());
  return match ? `#${match[1].toLowerCase()}` : null;
}
