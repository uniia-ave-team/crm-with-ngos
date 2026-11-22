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
