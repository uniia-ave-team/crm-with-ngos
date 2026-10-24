import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountRestoreService } from '../accounts/account-restore.service';
import { AccountsService } from '../accounts/accounts.service';
import { CurrentUserService } from '../users/current-user.service';
import { REDIRECT_URL } from './auth.service';
import { hasAnyPermission } from './permissions';

/**
 * Пускає на сторінку лише того, у кого є **хоча б одне** з перелічених прав;
 * інакше переадресація на `REDIRECT_URL`. Ставиться після `authGuard`:
 *
 *   canActivate: [authGuard, connectionGuard, requireAnyPermission(PERMS)]
 *
 * Стражі маршруту стартують одночасно, тому цей сам дочекається входу
 * (пошуку акаунта, спільного з `authGuard`; відмову входу оформлює `authGuard`,
 * а цей лише не пускає). Далі вирішують свіжі права: після пошуку профіль уже
 * свіжий, а якщо він був у памʼяті — перепитується (`GET /users/me`, запит
 * спільний з фоновим, що його робить `authGuard`); страж чекає, поки права
 * (`/users/me/permissions`) потраплять у знімок акаунта. Якщо оновити не
 * вдалося (сервер не відповів), лишаються права, що вже збережені.
 */
export function requireAnyPermission(required: readonly string[]): CanActivateFn {
  return async () => {
    const accounts = inject(AccountsService);
    const currentUser = inject(CurrentUserService);
    const restore = inject(AccountRestoreService);
    const router = inject(Router);

    accounts.reload();
    if (accounts.active() && currentUser.user()) {
      await currentUser.reload();
    } else if ((await restore.restore()) !== 'restored') {
      return false;
    }
    await currentUser.permissionsSettled();

    return hasAnyPermission(currentUser.permissions(), required) ? true : router.createUrlTree([REDIRECT_URL]);
  };
}
