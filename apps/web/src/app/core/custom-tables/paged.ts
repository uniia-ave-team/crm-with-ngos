/** Рядок без пробілів на краях; не рядок — порожньо. */
export const text = (value: unknown): string => (typeof value === 'string' ? value.trim() : '');

/**
 * Елементи сторінки списку (`PagedResult` сервера) й чи є наступна сторінка. Непридатні елементи
 * відкидає `toItem` (повертає порожній масив). Без `hasNextPage` наступна сторінка є, якщо
 * `pageNumber` менший за `totalPages`.
 */
export function readPagedItems<T, R>(body: unknown, page: number, toItem: (item: T) => R[]): { items: R[]; hasNextPage: boolean } {
  const record = (body ?? {}) as Record<string, unknown>;
  const items = Array.isArray(record['items']) ? (record['items'] as T[]).flatMap(toItem) : [];
  const totalPages = Number(record['totalPages']);
  const hasNextPage = typeof record['hasNextPage'] === 'boolean' ? record['hasNextPage'] : Number.isFinite(totalPages) && page < totalPages;
  return { items, hasNextPage };
}
