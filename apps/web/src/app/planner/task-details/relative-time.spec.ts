import { relativeTime } from './relative-time';

describe('relativeTime', () => {
  const now = Date.parse('2026-03-20T12:00:00Z');
  const ago = (seconds: number) => new Date(now - seconds * 1000).toISOString();

  it('says «just now» under a minute, then minutes, hours, yesterday and days ago', () => {
    expect(relativeTime(ago(30), 'uk', 'щойно', now)).toBe('щойно');
    expect(relativeTime(ago(5 * 60), 'uk', 'щойно', now)).toBe(new Intl.RelativeTimeFormat('uk', { numeric: 'auto' }).format(-5, 'minute'));
    expect(relativeTime(ago(3 * 3600), 'uk', 'щойно', now)).toBe(new Intl.RelativeTimeFormat('uk', { numeric: 'auto' }).format(-3, 'hour'));
    expect(relativeTime(ago(86400 + 60), 'uk', 'щойно', now)).toBe('учора');
    expect(relativeTime(ago(2 * 86400), 'uk', 'щойно', now)).toBe(new Intl.RelativeTimeFormat('uk', { numeric: 'auto' }).format(-2, 'day'));
  });
});
