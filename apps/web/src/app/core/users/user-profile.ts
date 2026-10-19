/**
 * Профіль користувача (`GET /api/v1/users/me` і `GET /api/v1/users/{id}`).
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
