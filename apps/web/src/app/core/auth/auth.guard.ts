import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountRestoreService } from '../accounts/account-restore.service';
import { AccountsService } from '../accounts/accounts.service';
import { NO_CONNECTION_ERROR } from '../connection/connection.guard';
import { CurrentUserService } from '../users/current-user.service';
import { LOGIN_URL } from './auth.service';

/**
 * Пускає на сторінку лише того, хто увійшов, і визначає поточний акаунт:
 * - поточний акаунт уже підтверджено в цій сесії сторінки (профіль завантажено) —
 *   пускаємо одразу, а профіль перепитуємо у фоні (`GET /users/me`): так при
 *   кожній зміні сторінки в інтерфейс потрапляють зміни в БД. Невдача цього
 *   запиту сторінку не блокує (недійсні токени обробляє `authInterceptor`);
 * - інакше шукаємо чинний акаунт серед збережених так само, як сторінка входу
 *   (`AccountRestoreService`: активний першим, застарілі позначаються й
 *   пропускаються); перший чинний стає активним, і його дані (профіль, ГО)
 *   відображаються далі;
 * - чинного акаунта нема — переадресація на /login; якщо причиною був хоч один
 *   акаунт, чий сервер не відповів (він теж стає застарілим), — на /login з
 *   помилкою про відсутність підключення.
 * Іде перед `connectionGuard`: пошук сам перевіряє підключення до сервера
 * кожного акаунта, а знайдений чинний акаунт означає, що воно є.
 */
export const authGuard: CanActivateFn = async () => {
  const accounts = inject(AccountsService);
  const currentUser = inject(CurrentUserService);
  const restore = inject(AccountRestoreService);
  const router = inject(Router);

  accounts.reload();
  if (accounts.active() && currentUser.user()) {
    void currentUser.reload();
    return true;
  }

  switch (await restore.restore()) {
    case 'restored':
      return true;
    case 'unavailable':
      return router.createUrlTree([LOGIN_URL], { queryParams: { error: NO_CONNECTION_ERROR } });
    default:
      return router.createUrlTree([LOGIN_URL]);
  }
};
