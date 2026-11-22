import { contrastWithBlack, contrastWithWhite, darkenUntil, letterCodes, lightenUntil, parseHex, toHex } from '../color/contrast';

/**
 * Мінімальний контраст тла аватара і з білим (літери), і з чорним (#000000, тло
 * інтерфейсу в темній темі): 4.5:1 — рівень AA для звичайного тексту.
 */
const MIN_CONTRAST = 4.5;

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
    color = darkenUntil(color, (candidate) => contrastWithWhite(candidate) >= MIN_CONTRAST);
  }
  if (contrastWithBlack(color) < MIN_CONTRAST) {
    color = lightenUntil(color, (candidate) => contrastWithBlack(candidate) >= MIN_CONTRAST);
  }
  return toHex(color);
}

function sum(values: number[]): number {
  return values.reduce((total, value) => total + value, 0);
}
