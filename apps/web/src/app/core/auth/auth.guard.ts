import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountRestoreService } from '../accounts/account-restore.service';
import { AccountsService } from '../accounts/accounts.service';
import { CurrentUserService } from '../users/current-user.service';
import { LOGIN_URL, RELOGIN_URL } from './auth.service';

/**
 * Пускає на сторінку лише того, хто увійшов, і визначає поточний акаунт:
 * - поточний акаунт уже підтверджено в цій сесії сторінки (профіль завантажено) —
 *   пускаємо одразу, а профіль перепитуємо у фоні (`GET /users/me`): так при
 *   кожній зміні сторінки в інтерфейс потрапляють зміни в БД. Невдача цього
 *   запиту сторінку не блокує (недійсні токени обробляє `authInterceptor`);
 * - інакше шукаємо чинний акаунт серед збережених так само, як сторінка входу
 *   (`AccountRestoreService`: активний першим, застарілі й недоступні пропускаються, а
 *   недоступні перевіряються знову лише коли доступних нема); перший чинний стає
 *   активним, і його дані (профіль, ГО) відображаються далі;
 * - доступного акаунта нема — переадресація на /relogin, якщо збережені акаунти є
 *   (їх можна відновити), і на /login, якщо акаунтів нема взагалі.
 * Іде перед `connectionGuard`: пошук сам перевіряє підключення до сервера
 * кожного акаунта, а знайдений чинний акаунт означає, що воно є.
 */
export const authGuard: CanActivateFn = async () => {
  const accounts = inject(AccountsService);
  const currentUser = inject(CurrentUserService);
  const restore = inject(AccountRestoreService);
  const router = inject(Router);

  accounts.reload();
  if (accounts.active() && currentUser.profileLoaded()) {
    void currentUser.reload();
    return true;
  }

  if ((await restore.restore()) === 'restored') {
    return true;
  }
  return router.createUrlTree([accounts.accounts().length > 0 ? RELOGIN_URL : LOGIN_URL]);
};
