/**
 * Зʼєднує елементи в перелік: між елементами — `separator`, перед останнім — `last`
 * (наприклад, `', '` і `' або '` дають «A, B або C»). Один елемент лишається як є, порожній
 * список дає порожній рядок.
 */
export function joinList(items: readonly string[], separator: string, last: string): string {
  if (items.length <= 1) {
    return items[0] ?? '';
  }
  return `${items.slice(0, -1).join(separator)}${last}${items[items.length - 1]}`;
}
