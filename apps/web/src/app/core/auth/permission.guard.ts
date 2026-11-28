import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AccountRestoreService } from '../accounts/account-restore.service';
import { AccountsService } from '../accounts/accounts.service';
import { CurrentUserService } from '../users/current-user.service';
import { REDIRECT_URL } from './auth.service';
import { hasAnyPermission } from './permissions';

/**
 * Свіжі права активного акаунта (для стражів і стартової сторінки, у контексті впровадження).
 * Стражі маршруту стартують одночасно, тому ця функція сама дочекається входу (пошуку акаунта,
 * спільного з `authGuard`); вхід не вдався — `null` (відмову оформлює `authGuard`). Далі профіль
 * перепитується, якщо він був у памʼяті (`GET /users/me`, запит спільний з фоновим, що його робить
 * `authGuard`), і функція чекає, поки права (`/users/me/permissions`) потраплять у знімок акаунта.
 * Якщо оновити не вдалося (сервер не відповів), лишаються права, що вже збережені.
 */
export async function freshPermissions(): Promise<readonly string[] | null> {
  const accounts = inject(AccountsService);
  const currentUser = inject(CurrentUserService);
  const restore = inject(AccountRestoreService);

  accounts.reload();
  if (accounts.active() && currentUser.profileLoaded()) {
    await currentUser.reload();
  } else if ((await restore.restore()) !== 'restored') {
    return null;
  }
  await currentUser.permissionsSettled();
  return currentUser.permissions();
}

/**
 * Пускає на сторінку лише того, у кого є **хоча б одне** з перелічених прав;
 * інакше переадресація на `REDIRECT_URL` (стартова сторінка: вона веде на першу доступну).
 * Ставиться після `authGuard`:
 *
 *   canActivate: [authGuard, connectionGuard, requireAnyPermission(PERMS)]
 *
 * Вирішують свіжі права (`freshPermissions`); вхід не вдався — не пускає (відмову оформлює
 * `authGuard`).
 */
export function requireAnyPermission(required: readonly string[]): CanActivateFn {
  return async () => {
    const router = inject(Router);
    const granted = await freshPermissions();
    if (granted === null) {
      return false;
    }
    return hasAnyPermission(granted, required) ? true : router.createUrlTree([REDIRECT_URL]);
  };
}
