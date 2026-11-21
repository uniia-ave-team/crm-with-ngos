/** Найбільша довжина пошти, яку приймає сервер (`MaxEmailLength`). */
export const EMAIL_MAX_LENGTH = 256;

/**
 * Чи схоже значення на пошту — так само, як вирішує сервер (`EmailAddress()` у
 * FluentValidation, режим `AspNetCoreCompatible`): рівно один `@`, і він не перший і не
 * останній символ. Сервер ще обмежує довжину (`EMAIL_MAX_LENGTH`).
 *
 * Понад сервер клієнт не пускає пробілів усередині: сервер їх прийняв би, але така пошта
 * майже напевно помилка введення, і лист на неї не дійде. Пробіли по краях не рахуються —
 * пошта йде в запит обрізаною.
 */
export function isEmail(value: string): boolean {
  const email = value.trim();
  if (!email || email.length > EMAIL_MAX_LENGTH || /\s/.test(email)) {
    return false;
  }
  const at = email.indexOf('@');
  return at > 0 && at < email.length - 1 && at === email.lastIndexOf('@');
}
