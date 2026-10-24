/** Найбільша довжина номера, яку приймає сервер (`MaxPhoneNumberLength`). */
export const PHONE_MAX_LENGTH = 30;
/** Кількість цифр у міжнародному номері (E.164): від 7 до 15. */
const PHONE_MIN_DIGITS = 7;
const PHONE_MAX_DIGITS = 15;

/**
 * Чи схоже значення на номер телефону: необовʼязковий `+` на початку, далі цифри,
 * які можна розділяти пробілами, дужками, дефісами й крапками; усього від 7 до
 * 15 цифр і не довше за ліміт сервера. Порожнє значення тут не перевіряється —
 * це рішення поля (воно необовʼязкове).
 */
export function isPhoneNumber(value: string): boolean {
  const phone = value.trim();
  if (!phone || phone.length > PHONE_MAX_LENGTH) {
    return false;
  }
  if (!/^\+?[\d\s().-]+$/.test(phone)) {
    return false;
  }
  const digits = phone.replace(/\D/g, '').length;
  return digits >= PHONE_MIN_DIGITS && digits <= PHONE_MAX_DIGITS;
}
