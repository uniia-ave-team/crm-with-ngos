import { Component, input, output } from '@angular/core';

import { RequestFailure } from '../../core/http/request-failure';
import { Role } from '../../core/roles/roles.service';

/** Що показує список ролей. */
export type RolesStatus = 'loading' | 'ready' | 'failed';

/** Стан списку ролей: що з запитом і які ролі вже є (при оновленні лишається попередній список). */
export interface RolesView {
  readonly status: RolesStatus;
  readonly roles: readonly Role[];
  /** Причина невдачі (лише для `failed`). */
  readonly failure: RequestFailure | null;
}

/**
 * Зміст бічного вікна «Ролі»: заголовок і ролі плашками (`app-role-pill`, колір за
 * назвою), по краях рядка кнопки «Редагувати» й «Видалити». Сам нічого не завантажує: стан приходить входом `view`.
 */
@Component({
  selector: 'app-roles-panel',
  templateUrl: './roles-panel.component.html',
  styleUrls: ['./roles-panel.component.scss'],
  standalone: false,
})
export class RolesPanelComponent {
  readonly view = input.required<RolesView>();
  /** Показувати кнопки «Редагувати» й «Видалити» (за замовчуванням ні: їх відкриває право на керування ролями). */
  readonly canManage = input(false);
  /** Натиснули «Редагувати» (олівець) біля ролі. */
  readonly roleEdit = output<Role>();
  /** Натиснули «Видалити» (смітник) біля ролі. */
  readonly roleDelete = output<Role>();
}
