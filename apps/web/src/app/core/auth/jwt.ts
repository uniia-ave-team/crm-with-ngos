/**
 * Читання access-токена на боці клієнта.
 *
 * JWT (JSON Web Token) — рядок із трьох частин через крапку: `заголовок.вміст.підпис`.
 * Заголовок і вміст — це JSON, закодований у base64url (base64, де «+» і «/» замінено на «-» і
 * «_», а доповнення «=» прибрано). У вмісті (payload) сервер записує, кому видано токен і до
 * якого часу він чинний; підпис засвідчує, що вміст ніхто не змінив.
 *
 * Клієнт підпис не перевіряє і не може: ключ є лише в сервера. Тому прочитане тут — підказка
 * для інтерфейсу, а не доказ: чи пускати користувача, вирішує сервер у відповідь на запит.
 * Читаємо три поля:
 * - `exp` — щоб оновити токен до того, як сервер його відхилить (`readJwtExpiry`);
 * - `sub` — щоб знати, чий це акаунт (`readJwtSubject`);
 * - `role_id` — з якими ролями видано токен (`readJwtRoleIds`).
 *
 * Жодна функція не кидає помилок: зіпсований чи чужий формат токена дає `null`, і той, хто
 * викликав, поводиться так, ніби цього поля нема.
 */

/**
 * Вміст (друга частина) JWT як обʼєкт. `null` — токена нема або він нечитний: це не JWT (нема
 * другої частини), зіпсований base64 чи JSON, або вміст не є обʼєктом.
 *
 * Основа для функцій нижче; окремо в застосунку не викликається.
 *
 * `atob` розкодовує байти як Latin-1, тож нелатинський текст у вмісті вийшов би спотвореним.
 * Нам це не заважає: `exp` — число, `sub` — ідентифікатор із латинських символів.
 */
export function readJwtPayload(token: string | null | undefined): Record<string, unknown> | null {
  const payload = token?.split('.')[1];
  if (!payload) {
    return null;
  }
  try {
    // base64url → звичайний base64, який розуміє `atob` (відсутнє доповнення «=» він допускає).
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const value: unknown = JSON.parse(json);
    return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/**
 * Поле `exp` JWT: момент, коли токен перестає діяти, у **секундах** від 1970 року (не в
 * мілісекундах, як `Date.now()`). `null` — поля нема, воно не число або токен нечитний.
 *
 * Використовує `AuthService`:
 * - `accessTokenExpired()` — чи токен уже прострочений або ось-ось буде. За цим токени
 *   оновлюються наперед: `authInterceptor` — перед запитом, `forgetActive()` — перед виходом,
 *   `AccountRestoreService` — коли перевіряє акаунт під час пошуку чинного;
 * - `accessTokenMsLeft()` — скільки лишилось до кінця терміну. За цим `SessionService`
 *   планує фонове оновлення токенів.
 */
export function readJwtExpiry(token: string | null | undefined): number | null {
  const exp = readJwtPayload(token)?.['exp'];
  return typeof exp === 'number' ? exp : null;
}

/**
 * Поле `sub` JWT (subject): `id` користувача, якому видано токен. `null` — поля нема, воно
 * порожнє чи не рядок або токен нечитний.
 *
 * Використовує `AuthService.adoptTokens()` після входу й після реєстрації за запрошенням: за
 * цим `id` разом з адресою сервера акаунт впізнається серед збережених (той самий користувач
 * на тому самому сервері не додається вдруге). Без `sub` акаунт впізнають за поштою.
 */
export function readJwtSubject(token: string | null | undefined): string | null {
  const sub = readJwtPayload(token)?.['sub'];
  return typeof sub === 'string' && sub ? sub : null;
}

/**
 * Поле `role_id` JWT: `id` ролей, з якими видано токен (сервер пише по claim на роль: одна
 * роль — рядок, кілька — масив; ролей нема — поля нема, тоді порожній список). `null` — токен
 * нечитний.
 *
 * Сервер бере права з цих ролей, а не з поточних ролей користувача, доки токен не оновлено.
 * Тому `CurrentUserService` порівнює їх із ролями з профілю (`/users/me`) і, коли ролі змінили,
 * оновлює токени, а тоді й права.
 */
export function readJwtRoleIds(token: string | null | undefined): string[] | null {
  const payload = readJwtPayload(token);
  if (!payload) {
    return null;
  }
  const value = payload['role_id'];
  const ids = Array.isArray(value) ? value : value === undefined ? [] : [value];
  return ids.filter((id): id is string => typeof id === 'string' && id !== '');
}
