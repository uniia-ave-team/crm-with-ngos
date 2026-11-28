/** Межі одиниць «скільки тому»: менше за межу — ця одиниця (секунди в ній). */
const UNITS: readonly (readonly [Intl.RelativeTimeFormatUnit, number, number])[] = [
  ['minute', 60, 3600],
  ['hour', 3600, 86400],
  ['day', 86400, 7 * 86400],
  ['week', 7 * 86400, 30 * 86400],
  ['month', 30 * 86400, 365 * 86400],
  ['year', 365 * 86400, Infinity],
];

/**
 * Скільки часу минуло від `iso` до `now` мовою `language`: «щойно» (менше хвилини — `justNow`),
 * далі «5 хвилин тому», «вчора», «2 дні тому», «минулого тижня»… (`Intl.RelativeTimeFormat`).
 */
export function relativeTime(iso: string, language: string, justNow: string, now = Date.now()): string {
  const seconds = Math.max(0, (now - new Date(iso).getTime()) / 1000);
  if (seconds < 60) {
    return justNow;
  }
  const format = new Intl.RelativeTimeFormat(language, { numeric: 'auto' });
  const [unit, size] = UNITS.find(([, , limit]) => seconds < limit) ?? UNITS[UNITS.length - 1];
  return format.format(-Math.floor(seconds / size), unit);
}
