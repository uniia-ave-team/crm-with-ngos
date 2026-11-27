import { HttpErrorResponse } from '@angular/common/http';

/**
 * Запис чи таблицю вже видалили (сервер відповів 404): повтор не допоможе, тож це окремий результат,
 * а не невдача, яку варто повторити.
 */
export type Gone = 'gone';

export function isGone(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 404;
}
