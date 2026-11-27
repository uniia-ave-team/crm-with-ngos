import { RoleUserDto, UserProfileDto } from '../../api/generated/models';
import { Untrusted } from '../http/api-contract';
import { PRONOUN_CATEGORY } from './pronouns';

/** Роль користувача в його профілі (`roles` відповіді `/users/me`): тип із контракту. */
export type UserRole = RoleUserDto;

/** Ролі з відповіді чи зі сховища: лише елементи з рядковими `id` й `name`, не масив — порожньо. */
export function sanitizeRoles(value: unknown): readonly UserRole[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.flatMap((item: unknown) => {
    const { id, name } = (item ?? {}) as Record<string, unknown>;
    return typeof id === 'string' && typeof name === 'string' ? [{ id, name }] : [];
  });
}

/**
 * Профіль поточного користувача (`GET /api/v1/users/me`): поля контракту (`UserProfileDto`), які
 * застосунок читає. Імена й типи беруться з контракту, тож якщо сервер перейменує чи прибере
 * поле, збірка впаде. Обовʼязкові лише `id`, пошта, імена й ролі; решта може бути відсутня
 * (сервер іншої версії) і тому необовʼязкова. Додаткових полів (`customFields`) тут нема: їх
 * показує лише таблиця колег.
 */
export type UserProfile = Pick<UserProfileDto, 'id' | 'email' | 'firstName' | 'lastName' | 'roles'> &
  Partial<
    Pick<
      UserProfileDto,
      | 'patronymic'
      | 'phoneNumber'
      | 'country'
      | 'emergencyContact'
      | 'preferredLanguage'
      | 'pronouns'
      | 'pronounCategory'
      | 'avatarUrl'
    >
  >;

/**
 * Профіль із відповіді `GET /api/v1/users/me`; `null` — відповідь не за контрактом: `id` не
 * рядок чи порожній, або пошта чи імена не рядки. Ролі — лише придатні (`sanitizeRoles`).
 * Необовʼязкові поля, що прийшли не того типу, відкидаються, ніби їх нема; категорія займенників
 * — лише одна з відомих (`PRONOUN_CATEGORY`) або `null`.
 */
export function toUserProfile(body: Untrusted<UserProfileDto>): UserProfile | null {
  const { id, email, firstName, lastName } = body ?? {};
  if (typeof id !== 'string' || !id || typeof email !== 'string' || typeof firstName !== 'string' || typeof lastName !== 'string') {
    return null;
  }
  const profile: UserProfile = { id, email, firstName, lastName, roles: [...sanitizeRoles(body?.roles)] };
  for (const field of OPTIONAL_TEXT_FIELDS) {
    const value = body?.[field];
    if (typeof value === 'string' || value === null) {
      profile[field] = value;
    }
  }
  const category = body?.pronounCategory;
  if (category === null || (Object.values(PRONOUN_CATEGORY) as unknown[]).includes(category)) {
    profile.pronounCategory = category as UserProfile['pronounCategory'];
  }
  return profile;
}

/** Необовʼязкові поля профілю: кожне — рядок або `null`. */
const OPTIONAL_TEXT_FIELDS = [
  'patronymic',
  'phoneNumber',
  'country',
  'emergencyContact',
  'preferredLanguage',
  'pronouns',
  'avatarUrl',
] as const satisfies readonly (keyof UserProfile)[];
