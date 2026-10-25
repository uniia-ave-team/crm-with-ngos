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
  internalPosition?: string | null;
  phoneNumber?: string | null;
  country?: string | null;
  emergencyContact?: string | null;
  preferredLanguage?: string | null;
  pronouns?: string | null;
  avatarUrl?: string | null;
  roles: unknown[];
  customFields: unknown[];
}
