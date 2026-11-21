/**
 * Спільна математика кольорів аватара (`avatar-color.ts`) і плашки ролі (`role-color.ts`):
 * яскравість і контраст за WCAG 2.x та зсув кольору рівно настільки, щоб контраст став
 * потрібним.
 */

/** Колір як три канали sRGB, кожен від 0 до 255. */
export type Rgb = readonly [number, number, number];

/** Відносна яскравість sRGB-кольору (WCAG 2.x), 0 — чорний, 1 — білий. */
export function relativeLuminance([r, g, b]: Rgb): number {
  const linear = (channel: number) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
}

/** Контраст двох кольорів (WCAG 2.x): від 1 (однакові) до 21 (чорний і білий). */
export function contrast(first: Rgb, second: Rgb): number {
  const [lighter, darker] = [relativeLuminance(first), relativeLuminance(second)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
}

/** Контраст кольору з білим (WCAG 2.x): від 1 (білий) до 21 (чорний). */
export function contrastWithWhite(color: Rgb): number {
  return 1.05 / (relativeLuminance(color) + 0.05);
}

/** Контраст кольору з чорним (WCAG 2.x): від 1 (чорний) до 21 (білий). */
export function contrastWithBlack(color: Rgb): number {
  return (relativeLuminance(color) + 0.05) / 0.05;
}

/** Скільки кроків бінарного пошуку: 24 дають точність, далеку за межами одиниці каналу. */
const SEARCH_STEPS = 24;

/**
 * Затемнює колір (множить канали на спільний коефіцієнт — відтінок зберігається) рівно
 * настільки, щоб `enough` став істинним: шукає найбільший такий коефіцієнт. Чорний (коефіцієнт
 * 0) має підходити завжди.
 */
export function darkenUntil(color: Rgb, enough: (candidate: Rgb) => boolean): Rgb {
  let low = 0; // чорний — підходить
  let high = 1; // вихідний колір — не підходить
  for (let i = 0; i < SEARCH_STEPS; i++) {
    const middle = (low + high) / 2;
    if (enough(scaleDown(color, middle))) {
      low = middle;
    } else {
      high = middle;
    }
  }
  return scaleDown(color, low);
}

/**
 * Висвітлює колір (підмішує білий) рівно настільки, щоб `enough` став істинним: шукає
 * найменшу таку частку білого. Білий (частка 1) має підходити завжди.
 */
export function lightenUntil(color: Rgb, enough: (candidate: Rgb) => boolean): Rgb {
  let low = 0; // вихідний колір — не підходить
  let high = 1; // білий — підходить
  for (let i = 0; i < SEARCH_STEPS; i++) {
    const middle = (low + high) / 2;
    if (enough(mixWithWhite(color, middle))) {
      high = middle;
    } else {
      low = middle;
    }
  }
  return mixWithWhite(color, high);
}

// Округлення вниз: колір лише темнішає, тож контраст зі світлим не впаде нижче порога.
function scaleDown([r, g, b]: Rgb, factor: number): Rgb {
  return [Math.floor(r * factor), Math.floor(g * factor), Math.floor(b * factor)];
}

// Округлення вгору: колір лише світлішає, тож контраст із темним не впаде нижче порога.
function mixWithWhite([r, g, b]: Rgb, share: number): Rgb {
  return [Math.ceil(r + (255 - r) * share), Math.ceil(g + (255 - g) * share), Math.ceil(b + (255 - b) * share)];
}

/** `#rrggbb` чи `rrggbb` → канали. */
export function parseHex(hex: string): Rgb {
  const digits = hex.startsWith('#') ? hex.slice(1) : hex;
  return [parseInt(digits.slice(0, 2), 16), parseInt(digits.slice(2, 4), 16), parseInt(digits.slice(4, 6), 16)];
}

/** Канали → `#rrggbb`. */
export function toHex([r, g, b]: Rgb): string {
  return '#' + [r, g, b].map((channel) => channel.toString(16).padStart(2, '0')).join('');
}

/** Коди (Unicode) усіх літер рядка за порядком; решта символів — пробіли, дефіси, цифри — ні. */
export function letterCodes(text: string): number[] {
  return Array.from(text)
    .filter((character) => /\p{L}/u.test(character))
    .map((letter) => letter.codePointAt(0)!);
}
