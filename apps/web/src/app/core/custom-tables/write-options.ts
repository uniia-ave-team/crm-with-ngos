/**
 * Параметри запиту, що змінює дані таблиць. Поки сторінка схована (`visibilityState === 'hidden'`:
 * вкладку перемкнули, згорнули чи закривають), запит іде з `keepalive` — браузер не обірве його, якщо
 * сторінку закриють, доки він ще йде (працює з `withFetch()` у `provideHttpClient`; таким запитам
 * разом браузер дає до 64 КБ тіла, тож поки сторінку видно, `keepalive` не вмикаємо).
 */
export function writeRequestOptions(): { keepalive: boolean } {
  return { keepalive: typeof document !== 'undefined' && document.visibilityState === 'hidden' };
}
