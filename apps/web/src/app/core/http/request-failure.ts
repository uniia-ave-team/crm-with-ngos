import { HttpErrorResponse } from '@angular/common/http';

/**
 * Чим закінчився невдалий запит до API:
 * - `invalid` — 400, сервер не прийняв дані;
 * - `unauthorized` / `forbidden` — 401 / 403;
 * - `unreachable` — відповіді немає (мережа, таймаут, CORS);
 * - `error` — будь-яка інша помилка сервера.
 */
export type RequestFailure = 'invalid' | 'unauthorized' | 'forbidden' | 'unreachable' | 'error';

export function toRequestFailure(error: unknown): RequestFailure {
  if (!(error instanceof HttpErrorResponse) || error.status === 0) {
    return 'unreachable';
  }
  switch (error.status) {
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
