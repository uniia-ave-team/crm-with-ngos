import { RequestFailure } from '../http/request-failure';
import { StoredAccount } from './accounts.service';

/** Ключ перекладу помилки входу (`login.form.error.<ключ>`) за відповіддю сервера. */
export function loginErrorKey(result: RequestFailure): 'invalid' | 'unauthorized' | 'unreachable' | 'error' {
  switch (result) {
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
