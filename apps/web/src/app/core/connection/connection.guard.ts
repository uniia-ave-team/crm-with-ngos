import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { CanActivateFn, Router } from '@angular/router';
import { filter, firstValueFrom } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';
import { signedOutUrl } from '../auth/auth.service';
import { ApiStatus, ConnectionService } from './connection.service';

/** Перевірка завершилася: стан уже не «невідомо» й не «перевіряю». */
const settled = (status: ApiStatus): boolean => status !== 'unknown' && status !== 'checking';

/**
 * Пускає на сторінку лише за наявності підключення (API за поточною адресою існує й працює).
 *
 * Немає підключення — сервер активного акаунта не відповідає. Акаунт позначається недоступним
 * (`unavailable`) і перестає бути активним, а користувач потрапляє на сторінку повторного
 * входу (`/relogin`): там видно, що з цим акаунтом, і можна вибрати інший. Це те саме, що
 * стається, коли сервер не відповів на оновлення токенів (`AuthService.expireSession`). Без
 * збережених акаунтів — на `/login` (на практиці туди раніше веде `authGuard`, що йде першим).
 *
 * Сам страж сервер зазвичай не перевіряє: він читає результат останньої перевірки
 * (`ConnectionService.apiStatus`), а якщо вона ще триває — чекає на неї. Перевірку запускають
 * ті, хто міняє ціль: пошук чинного акаунта (`AccountRestoreService`, його викликає
 * `authGuard`, що йде першим), перемикання акаунта, сторінка входу. Тож сервер, який перестав
 * відповідати вже під час роботи, страж не помітить: це помічає `authGuard` за невдалим
 * фоновим `GET /users/me` (перевіряє сервер і, якщо той мовчить, знімає акаунт з активного).
 * Виняток — невдача, що лишилася від давнішої перевірки: перш ніж через неї знімати акаунт з
 * активного, страж перевіряє сервер ще раз (він міг уже відновитися).
 *
 * Яку адресу перевіряти, вирішує `ConnectionService`: сервер активного акаунта, а без нього —
 * адреса, надана на сторінці входу, чи домен сторінки.
 */
export const connectionGuard: CanActivateFn = async () => {
  const connection = inject(ConnectionService);
  const accounts = inject(AccountsService);
  const router = inject(Router);

  // Перевірка ще триває — її результат буде свіжим; уже завершена могла застаріти.
  const stale = settled(connection.apiStatus());
  let status = await firstValueFrom(toObservable(connection.apiStatus).pipe(filter(settled)));
  if (status !== 'healthy' && stale) {
    status = await connection.checkApi();
  }
  if (status === 'healthy') {
    return true;
  }

  const activeId = accounts.activeId();
  if (activeId) {
    accounts.markUnavailable(activeId);
  }
  return router.createUrlTree([signedOutUrl(accounts)]);
};
