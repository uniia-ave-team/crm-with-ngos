/**
 * Мінімальний контраст тла аватара і з білим (літери), і з чорним (#000000, тло
 * інтерфейсу в темній темі): 4.5:1 — рівень AA для звичайного тексту.
 */
const MIN_CONTRAST = 4.5;

type Rgb = readonly [number, number, number];

/**
 * Колір тла аватара за імʼям, прізвищем і назвою організації (враховуються
 * лише літери, інші символи — пробіли, дефіси — ні):
 * 1. імʼя й прізвище: беремо коди перших двох літер і сумуємо — окремо для
 *    імені, окремо для прізвища;
 * 1а. до суми імені додаємо код 5-ї літери назви організації, до суми
 *    прізвища — код її передостанньої літери (якщо літер замало або назви
 *    нема — додаємо 0);
 * 2. від кожної суми віднімаємо код останньої літери імені й додаємо код
 *    останньої літери прізвища (від'ємну суму беремо за модулем);
 * 3. обидві суми в HEX склеюємо (імʼя, потім прізвище); якщо знаків більше за
 *    шість — обрізаємо, якщо менше — дописуємо в кінець HEX суми імені знову,
 *    доки не назбирається шість;
 * 4. перший зсув: якщо контраст із білим нижчий за 4.5:1, затемнюємо колір
 *    (множимо канали на спільний коефіцієнт — відтінок зберігається) рівно
 *    настільки, щоб контраст став 4.5:1;
 * 5. другий зсув: якщо контраст із чорним нижчий за 4.5:1, висвітлюємо колір
 *    (підмішуємо білий) рівно настільки, щоб контраст став 4.5:1. Після другого
 *    зсуву колір приймається таким, яким він є.
 * Повертає `#rrggbb` або `null`, якщо в імені й прізвищі зовсім нема літер.
 */
export function avatarColor(firstName: string, lastName: string, ngoName = ''): string | null {
  const first = letterCodes(firstName);
  const last = letterCodes(lastName);
  const ngo = letterCodes(ngoName);
  if (first.length === 0 && last.length === 0) {
    return null;
  }
  const adjustment = (last.at(-1) ?? 0) - (first.at(-1) ?? 0);
  const firstSum = Math.abs(sum(first.slice(0, 2)) + (ngo[4] ?? 0) + adjustment);
  const lastSum = Math.abs(sum(last.slice(0, 2)) + (ngo.at(-2) ?? 0) + adjustment);

  const firstHex = firstSum.toString(16);
  let hex = firstHex + lastSum.toString(16);
  while (hex.length < 6) {
    hex += firstHex;
  }
  let color = parseHex(hex.slice(0, 6));
  if (contrastWithWhite(color) < MIN_CONTRAST) {
    color = darkenForWhite(color);
  }
  if (contrastWithBlack(color) < MIN_CONTRAST) {
    color = lightenForBlack(color);
  }
  return toHex(color);
}

/** Коди (Unicode) усіх літер рядка за порядком. */
function letterCodes(text: string): number[] {
  return Array.from(text)
    .filter((character) => /\p{L}/u.test(character))
    .map((letter) => letter.codePointAt(0)!);
}

function sum(values: number[]): number {
  return values.reduce((total, value) => total + value, 0);
}

/** Відносна яскравість sRGB-кольору (WCAG 2.x), 0 — чорний, 1 — білий. */
export function relativeLuminance([r, g, b]: Rgb): number {
  const linear = (channel: number) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
}

/** Контраст кольору з білим (WCAG 2.x): від 1 (білий) до 21 (чорний). */
export function contrastWithWhite(color: Rgb): number {
  return 1.05 / (relativeLuminance(color) + 0.05);
}

/** Контраст кольору з чорним (WCAG 2.x): від 1 (чорний) до 21 (білий). */
export function contrastWithBlack(color: Rgb): number {
  return (relativeLuminance(color) + 0.05) / 0.05;
}

function parseHex(hex: string): Rgb {
  return [parseInt(hex.slice(0, 2), 16), parseInt(hex.slice(2, 4), 16), parseInt(hex.slice(4, 6), 16)];
}

/** Найбільший коефіцієнт затемнення (бінарний пошук), за якого контраст із білим ≥ 4.5:1. */
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

/** Найменша частка білого (бінарний пошук), за якої контраст із чорним ≥ 4.5:1. */
function lightenForBlack(color: Rgb): Rgb {
  let low = 0; // вихідний колір — не підходить
  let high = 1; // білий — контраст 21, точно підходить
  for (let i = 0; i < 24; i++) {
    const middle = (low + high) / 2;
    if (contrastWithBlack(mixWithWhite(color, middle)) >= MIN_CONTRAST) {
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

// Округлення вгору: колір лише світлішає, тож контраст із чорним не впаде нижче порога.
function mixWithWhite([r, g, b]: Rgb, share: number): Rgb {
  return [Math.ceil(r + (255 - r) * share), Math.ceil(g + (255 - g) * share), Math.ceil(b + (255 - b) * share)];
}

function toHex([r, g, b]: Rgb): string {
  return '#' + [r, g, b].map((channel) => channel.toString(16).padStart(2, '0')).join('');
}
