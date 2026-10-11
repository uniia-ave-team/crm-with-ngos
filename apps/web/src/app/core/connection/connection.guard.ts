import { inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { CanActivateFn, Router } from '@angular/router';
import { filter, map, take } from 'rxjs';

import { ConnectionService } from './connection.service';

/**
 * Значення query-параметра `error`, з яким guard переадресовує на /login.
 * Сторінка входу за ним пояснює, чому користувача викинуло сюди.
 */
export const NO_CONNECTION_ERROR = 'no-connection';

/**
 * Пускає на сторінку лише за наявності підключення (API за поточною адресою
 * існує й працює). Поки перевірка ще триває — чекає її результату. Якщо
 * підключення немає — переадресовує на /login з помилкою.
 *
 * Перевірка йде за наданою адресою з localStorage (якщо вона є), а якщо ні —
 * за доменом сторінки: це вирішує сам ConnectionService.
 */
export const connectionGuard: CanActivateFn = () => {
  const connection = inject(ConnectionService);
  const router = inject(Router);

  return toObservable(connection.apiStatus).pipe(
    filter((status) => status !== 'unknown' && status !== 'checking'),
    take(1),
    map((status) =>
      status === 'healthy' ? true : router.createUrlTree(['/login'], { queryParams: { error: NO_CONNECTION_ERROR } }),
    ),
  );
};
