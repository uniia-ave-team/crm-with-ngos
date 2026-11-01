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
 * Профіль поточного користувача (`GET /api/v1/users/me`).
 * Поля, які ще не потрібні, не описано.
 */
export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  patronymic?: string | null;
  phoneNumber?: string | null;
  country?: string | null;
  emergencyContact?: string | null;
  preferredLanguage?: string | null;
  pronouns?: string | null;
  avatarUrl?: string | null;
  roles: UserRole[];
  customFields: unknown[];
}
