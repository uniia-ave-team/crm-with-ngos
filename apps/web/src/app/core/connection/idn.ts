/**
 * Домени з нелатинськими літерами (наприклад, у зоні `.укр`). У посиланні браузер записує
 * такий домен латиницею — у Punycode: `приклад.укр` стає `xn--80aikifvh.xn--j1amh`. Запити
 * йдуть саме за таким записом, а щоб показати домен людині чи взяти з нього літери, його треба
 * розкодувати назад. Готової функції для цього в браузері нема, тому розкодування тут
 * (алгоритм RFC 3492).
 */

const BASE = 36;
const T_MIN = 1;
const T_MAX = 26;
const SKEW = 38;
const DAMP = 700;
const INITIAL_BIAS = 72;
const INITIAL_CODE_POINT = 128;
const PREFIX = 'xn--';

/**
 * Домен так, як його пишуть люди: частини в Punycode (`xn--…`) розкодовано, решта лишається як
 * є. Частина, яку розкодувати не вдалося (зіпсований запис), теж лишається як є.
 */
export function hostnameToUnicode(hostname: string): string {
  return hostname
    .split('.')
    .map((label) => {
      if (!label.toLowerCase().startsWith(PREFIX)) {
        return label;
      }
      return decodePunycode(label.slice(PREFIX.length)) ?? label;
    })
    .join('.');
}

/**
 * Адреса сервера так, як її показують людині: `https://xn--80aikifvh.xn--j1amh:5065` →
 * `https://приклад.укр:5065`. Адреси зберігаються й надсилаються в записі браузера, а в полях
 * вводу й повідомленнях мають читатися. Якщо таку адресу ввести назад, вийде той самий сервер.
 */
export function serverAddressToUnicode(address: string): string {
  return address.replace(/xn--[a-z0-9-]+/gi, (label) => hostnameToUnicode(label));
}

/** Розкодовує одну частину домену з Punycode (без `xn--`); `null` — це не Punycode. */
function decodePunycode(input: string): string | null {
  // До останнього дефіса — звичайні латинські символи частини, після нього — закодовані.
  const delimiter = input.lastIndexOf('-');
  const output: number[] = Array.from(input.slice(0, Math.max(delimiter, 0)), (char) => char.charCodeAt(0));
  let codePoint = INITIAL_CODE_POINT;
  let bias = INITIAL_BIAS;
  let position = 0;

  for (let index = delimiter + 1; index < input.length; ) {
    const previousPosition = position;
    for (let weight = 1, k = BASE; ; k += BASE) {
      const digit = index < input.length ? digitOf(input.charCodeAt(index++)) : null;
      if (digit === null) {
        return null;
      }
      position += digit * weight;
      const threshold = k <= bias ? T_MIN : k >= bias + T_MAX ? T_MAX : k - bias;
      if (digit < threshold) {
        break;
      }
      weight *= BASE - threshold;
    }
    const length = output.length + 1;
    bias = adaptBias(position - previousPosition, length, previousPosition === 0);
    codePoint += Math.floor(position / length);
    position %= length;
    if (codePoint > 0x10ffff) {
      return null;
    }
    output.splice(position++, 0, codePoint);
  }
  return String.fromCodePoint(...output);
}

/** Значення «цифри» Punycode: `a`–`z` — 0–25, `0`–`9` — 26–35; інший символ — `null`. */
function digitOf(charCode: number): number | null {
  if (charCode >= 48 && charCode <= 57) {
    return charCode - 22;
  }
  if (charCode >= 65 && charCode <= 90) {
    return charCode - 65;
  }
  if (charCode >= 97 && charCode <= 122) {
    return charCode - 97;
  }
  return null;
}

function adaptBias(delta: number, length: number, first: boolean): number {
  let rest = first ? Math.floor(delta / DAMP) : delta >> 1;
  rest += Math.floor(rest / length);
  let k = 0;
  for (; rest > ((BASE - T_MIN) * T_MAX) >> 1; k += BASE) {
    rest = Math.floor(rest / (BASE - T_MIN));
  }
  return k + Math.floor(((BASE - T_MIN + 1) * rest) / (rest + SKEW));
}
