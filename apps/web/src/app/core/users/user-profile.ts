import { UserProfileDto } from '../../api/generated/models';

/** Роль користувача в його профілі (`roles` відповіді `/users/me`). */
export interface UserRole {
  id: string;
  name: string;
}

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
 * поле, збірка впаде. Обовʼязкові лише `id`, пошта, імена, ролі й додаткові поля; решта може
 * бути відсутня (сервер іншої версії) і тому необовʼязкова.
 */
export type UserProfile = Pick<UserProfileDto, 'id' | 'email' | 'firstName' | 'lastName' | 'roles' | 'customFields'> &
  Partial<
    Pick<
      UserProfileDto,
      'patronymic' | 'phoneNumber' | 'country' | 'emergencyContact' | 'preferredLanguage' | 'pronouns' | 'avatarUrl'
    >
  >;
