import { RequestFailure } from '../core/http/request-failure';
import { Role } from '../core/roles/roles.service';

/** Що показує список ролей. */
export type RolesStatus = 'loading' | 'ready' | 'failed';

/**
 * Стан списку ролей: що з запитом і які ролі вже є (при оновленні лишається попередній
 * список). Його показують вікно «Ролі» (`app-roles-panel`) і вибір ролей у формі колеги
 * (`app-colleague-form`); завантажує сторінка колег.
 */
export interface RolesView {
  readonly status: RolesStatus;
  readonly roles: readonly Role[];
  /** Причина невдачі (лише для `failed`). */
  readonly failure: RequestFailure | null;
}
