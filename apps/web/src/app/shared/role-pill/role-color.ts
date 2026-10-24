import { contrastWithWhite, relativeLuminance } from '../avatar/avatar-color';

/**
 * Мінімальний контраст кольору плашки ролі: 7:1 — рівень AAA для звичайного
 * тексту (з білим для заливки, з тлом сторінки для рамки й тексту).
 */
const MIN_CONTRAST = 7;

/**
 * Тло сторінки в темній темі (`--ion-background-color` у `theme/nav-tokens.scss`):
 * від нього рахується контраст кольору рамки й тексту неактивної плашки.
 */
export const DARK_PAGE_BACKGROUND = '#1b1c1f';

/** Золотий кут: сусідні значення сум потрапляють у далекі відтінки. */
const GOLDEN_ANGLE = 137.508;
/**
 * Зсув відтінку за другою сумою: розводить ролі, у яких збіглася загальна сума.
 * Значення підібрано так, щоб при малих різницях сум (до ±10) відтінки не
 * збігались із золотим кутом (97.31° давало збіг на 1.4° для різниць +1 і +6).
 */
const SECOND_SUM_HUE_STEP = 71.46;
const SATURATION = 0.7;
const LIGHTNESS = 0.5;

type Rgb = readonly [number, number, number];

/** Кольори плашки ролі (`#rrggbb`). */
export interface RoleColor {
  /**
   * Основний: контраст із білим ≥ 7:1. Заливка активної плашки (текст білий) і,
   * у світлій темі, рамка й текст неактивної (на білому тлі сторінки).
   */
  readonly base: string;
  /**
   * Той самий відтінок, світліший: контраст із тлом темної теми
   * (`DARK_PAGE_BACKGROUND`) ≥ 7:1. Рамка й текст неактивної плашки в темній темі.
   */
  readonly light: string;
}

/** Суми літер назви ролі, з яких рахується відтінок. */
export interface RoleLetterSums {
  /** Перша літера + остання. */
  readonly first: number;
  /** Друга + передостання. */
  readonly second: number;
  /** Третя + передпередостання. */
  readonly third: number;
  /** Перша сума + третя − друга. */
  readonly total: number;
}

/**
 * Кольори плашки ролі за її назвою (враховуються лише літери, інші символи —
 * пробіли, дефіси, цифри — ні; регістр теж важить: код великої й малої літери
 * різний):
 * 1. сума першої літери з останньою, другої з передостанньою, третьої з
 *    передпередостаньою (якщо літер замало — відсутні рахуються за 0);
 * 2. загальна сума: перша + третя − друга;
 * 3. відтінок: загальна сума × золотий кут + друга сума × 71.46°, за модулем
 *    360°; насиченість 70%, яскравість 50% (насичений колір);
 * 4. перший зсув: колір затемнюється (множенням каналів на спільний коефіцієнт,
 *    відтінок зберігається) рівно настільки, щоб контраст із білим був 7:1
 *    (якщо він уже не нижчий — лишається як є) — це `base`;
 * 5. другий зсув: від `base` підмішується білий рівно стільки, щоб контраст із
 *    тлом темної теми став 7:1 — це `light`.
 * Контраст 7:1 одним кольором і з білим, і з чорним недосяжний (потрібна
 * яскравість ≤ 0.10 і ≥ 0.30 одночасно), тому кольорів два: для світлої теми й
 * для темної. Повертає `null`, якщо в назві зовсім нема літер.
 */
export function roleColor(roleName: string): RoleColor | null {
  const sums = roleLetterSums(roleName);
  if (!sums) {
    return null;
  }
  const hue = (((sums.total * GOLDEN_ANGLE + sums.second * SECOND_SUM_HUE_STEP) % 360) + 360) % 360;
  const vivid = hslToRgb(hue, SATURATION, LIGHTNESS);
  const base = contrastWithWhite(vivid) >= MIN_CONTRAST ? vivid : darkenForWhite(vivid);
  const light = lightenForDarkPage(base);
  return { base: toHex(base), light: toHex(light) };
}

/** Суми літер назви; `null`, якщо літер нема. */
export function roleLetterSums(roleName: string): RoleLetterSums | null {
  const letters = Array.from(roleName)
    .filter((character) => /\p{L}/u.test(character))
    .map((letter) => letter.codePointAt(0)!);
  if (letters.length === 0) {
    return null;
  }
  const at = (index: number) => letters[index] ?? 0;
  const last = letters.length - 1;
  const first = at(0) + at(last);
  const second = at(1) + at(last - 1);
  const third = at(2) + at(last - 2);
  return { first, second, third, total: first + third - second };
}

/** Найбільший коефіцієнт затемнення (бінарний пошук), за якого контраст із білим ≥ 7:1. */
function darkenForWhite(color: Rgb): Rgb {
  let low = 0; // чорний — контраст 21, точно підходить
  let high = 1; // вихідний колір — не підходить
  for (let i = 0; i < 24; i++) {
    const middle = (low + high) / 2;
    if (contrastWithWhite(scaleDown(color, middle)) >= MIN_CONTRAST) {
      low = middle;
    } else {
      high = middle;
    }
  }
  return scaleDown(color, low);
}

/** Найменша частка білого (бінарний пошук), за якої контраст із тлом темної теми ≥ 7:1. */
function lightenForDarkPage(color: Rgb): Rgb {
  const background = relativeLuminance(parseHex(DARK_PAGE_BACKGROUND));
  const contrast = (candidate: Rgb) => (relativeLuminance(candidate) + 0.05) / (background + 0.05);
  if (contrast(color) >= MIN_CONTRAST) {
    return color;
  }
  let low = 0; // вихідний колір — не підходить
  let high = 1; // білий — контраст понад 15, точно підходить
  for (let i = 0; i < 24; i++) {
    const middle = (low + high) / 2;
    if (contrast(mixWithWhite(color, middle)) >= MIN_CONTRAST) {
      high = middle;
    } else {
      low = middle;
    }
  }
  return mixWithWhite(color, high);
}

// Округлення вниз: колір лише темнішає, тож контраст із білим не впаде нижче порога.
function scaleDown([r, g, b]: Rgb, factor: number): Rgb {
  return [Math.floor(r * factor), Math.floor(g * factor), Math.floor(b * factor)];
}

// Округлення вгору: колір лише світлішає, тож контраст із темним тлом не впаде нижче порога.
function mixWithWhite([r, g, b]: Rgb, share: number): Rgb {
  return [Math.ceil(r + (255 - r) * share), Math.ceil(g + (255 - g) * share), Math.ceil(b + (255 - b) * share)];
}

function hslToRgb(hue: number, saturation: number, lightness: number): Rgb {
  const chroma = (1 - Math.abs(2 * lightness - 1)) * saturation;
  const section = hue / 60;
  const x = chroma * (1 - Math.abs((section % 2) - 1));
  const [r, g, b] =
    section < 1 ? [chroma, x, 0] : section < 2 ? [x, chroma, 0] : section < 3 ? [0, chroma, x] : section < 4 ? [0, x, chroma] : section < 5 ? [x, 0, chroma] : [chroma, 0, x];
  const offset = lightness - chroma / 2;
  return [Math.round((r + offset) * 255), Math.round((g + offset) * 255), Math.round((b + offset) * 255)];
}

function parseHex(hex: string): Rgb {
  return [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)];
}

function toHex([r, g, b]: Rgb): string {
  return '#' + [r, g, b].map((channel) => channel.toString(16).padStart(2, '0')).join('');
}
