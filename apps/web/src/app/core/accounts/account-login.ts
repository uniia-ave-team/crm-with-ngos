import type { LoginResult } from '../auth/auth.service';
import { StoredAccount } from './accounts.service';

/**
 * Ключ перекладу помилки входу (`login.form.error.<ключ>`) за результатом входу. `busy` —
 * демо перевантажене (місць нема або забагато входів з однієї адреси): `demoBusy`.
 */
export function loginErrorKey(
  result: Exclude<LoginResult, 'ok'>,
): 'invalid' | 'unauthorized' | 'unreachable' | 'storageFull' | 'demoBusy' | 'error' {
  switch (result) {
    case 'storageFull':
      return 'storageFull';
    case 'busy':
      return 'demoBusy';
    case 'invalid':
      return 'invalid';
    case 'unauthorized':
      return 'unauthorized';
    case 'unreachable':
      return 'unreachable';
    default:
      return 'error';
  }
}

/**
 * У акаунт треба входити знову (форма входу), а не перемикатися на нього: сервер уже відхилив
 * його токени (`outdated`). Спроба перемкнутися однаково впала б на тих самих токенах, лише після
 * кількох запитів. Позначка «сервер не відповів» (`unavailable`) на це не впливає.
 */
export function needsSignIn(account: StoredAccount): boolean {
  return account.outdated;
}

/** Пошти збігаються без урахування регістру й пробілів по краях. */
export function sameEmail(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}

/**
 * Ключ акаунта, який вхід з поштою `email` перезапише: `accountId` вказує на збережений акаунт,
 * і в нього така сама пошта, як у тієї, з якою входять. Інакше — `undefined` (вхід додасть
 * акаунт або оновить той, що вже є для цього користувача на сервері).
 */
export function accountToReplace(
  accounts: readonly StoredAccount[],
  accountId: string | undefined | null,
  email: string,
): string | undefined {
  const saved = accountId ? accounts.find((account) => account.id === accountId) : undefined;
  return saved && sameEmail(saved.email, email) ? saved.id : undefined;
}
