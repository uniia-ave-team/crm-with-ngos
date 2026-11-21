import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';

/**
 * Чим закінчився невдалий запит до API:
 * - `invalid` — 400, сервер не прийняв дані;
 * - `unauthorized` / `forbidden` — 401 / 403;
 * - `unreachable` — відповіді немає (мережа, CORS — статус 0; таймаут запиту);
 * - `error` — будь-яка інша помилка: інший статус сервера або помилка, що не є відповіддю
 *   (наприклад, збій у коді, який обробляє відповідь). Сервер тут не винен, тож це не
 *   «немає звʼязку».
 */
export type RequestFailure = 'invalid' | 'unauthorized' | 'forbidden' | 'unreachable' | 'error';

export function toRequestFailure(error: unknown): RequestFailure {
  if (error instanceof TimeoutError) {
    return 'unreachable';
  }
  if (!(error instanceof HttpErrorResponse)) {
    return 'error';
  }
  switch (error.status) {
    case 0:
      return 'unreachable';
    case 400:
      return 'invalid';
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    default:
      return 'error';
  }
}

/**
 * Коди помилок демо-режиму: поле `errorCode` у тілі відповіді сервера (problem JSON, RFC 7807).
 * - `DEMO_MODE_RESTRICTED` (403) — дію в демо вимкнено;
 * - `DEMO_QUOTA_EXCEEDED` (403) — вичерпано ліміт демо (наприклад, завантажень);
 * - `DEMO_SESSION_EXPIRED` (401) — копії демо-даних відвідувача вже нема (на будь-який запит
 *   з токеном і на обмін токенів);
 * - `DEMO_CAPACITY_REACHED` (503 на вхід) — забагато відвідувачів демо одночасно.
 */
export const DEMO_MODE_RESTRICTED = 'DEMO_MODE_RESTRICTED';
export const DEMO_QUOTA_EXCEEDED = 'DEMO_QUOTA_EXCEEDED';
export const DEMO_SESSION_EXPIRED = 'DEMO_SESSION_EXPIRED';
export const DEMO_CAPACITY_REACHED = 'DEMO_CAPACITY_REACHED';

/**
 * `errorCode` із тіла невдалої відповіді (обʼєкт або JSON-рядок) чи `null`, якщо його там нема.
 * Тіло-`Blob` (запити зображень) тут не читається — для нього є `readBlobErrorCode`.
 */
export function readErrorCode(error: unknown): string | null {
  return error instanceof HttpErrorResponse ? errorCodeOf(error.error) : null;
}

/** Те саме для тіла-`Blob`: його вміст читається асинхронно. Нечитне тіло — `null`. */
export async function readBlobErrorCode(body: Blob): Promise<string | null> {
  try {
    return errorCodeOf(await blobText(body));
  } catch {
    return null;
  }
}

/** Текст `Blob`; де нема `Blob.text()` (старі браузери) — через `FileReader`. */
function blobText(body: Blob): Promise<string> {
  if (typeof body.text === 'function') {
    return body.text();
  }
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error ?? new Error('Blob is unreadable'));
    reader.readAsText(body);
  });
}

/** `errorCode` тіла відповіді: обʼєкта або тексту з JSON; рядок — інакше `null`. */
function errorCodeOf(body: unknown): string | null {
  let parsed = body;
  if (typeof body === 'string') {
    try {
      parsed = JSON.parse(body);
    } catch {
      return null;
    }
  }
  const code = parsed !== null && typeof parsed === 'object' ? (parsed as { errorCode?: unknown }).errorCode : null;
  return typeof code === 'string' ? code : null;
}
