/** Payload JWT як обʼєкт; `null`, якщо токен нечитний (не JWT, зіпсований base64/JSON). */
export function readJwtPayload(token: string | null | undefined): Record<string, unknown> | null {
  const payload = token?.split('.')[1];
  if (!payload) {
    return null;
  }
  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const value: unknown = JSON.parse(json);
    return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** Поле `exp` (секунди) JWT; `null`, якщо його нема. */
export function readJwtExpiry(token: string | null | undefined): number | null {
  const exp = readJwtPayload(token)?.['exp'];
  return typeof exp === 'number' ? exp : null;
}

/** Поле `sub` JWT (id користувача); `null`, якщо його нема. */
export function readJwtSubject(token: string | null | undefined): string | null {
  const sub = readJwtPayload(token)?.['sub'];
  return typeof sub === 'string' && sub ? sub : null;
}

/**
 * Поле `demo_exp` JWT — Unix-час (секунди), коли буде видалено демо-дані відвідувача. Його
 * мають токени, видані в демо-режимі; сервер може віддати число або рядок із числом.
 * `null` — поля нема чи воно не схоже на час.
 */
export function readJwtDemoExpiry(token: string | null | undefined): number | null {
  const raw = readJwtPayload(token)?.['demo_exp'];
  const seconds = typeof raw === 'number' ? raw : typeof raw === 'string' && raw.trim() ? Number(raw) : NaN;
  return Number.isFinite(seconds) && seconds > 0 ? seconds : null;
}
