import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { RolePillComponent } from '../../shared/role-pill/role-pill.component';
import { RolesView } from '../roles-view';

/**
 * Вибір ролей колеги плашками (`app-role-pill`: вибрана — заливка й `checkmark-circle`,
 * невибрана — рамка й порожнє коло `ellipse-outline`) з підписом «Ролі». Ролі приходять входом
 * `roles` (стан запиту, як у вікні «Ролі»): поки йде запит, порожньо чи невдача — повідомлення.
 * Що вибрано, тримає сторінка чи форма (`selected`); натискання на плашку — подія `roleToggle` з
 * `id` ролі. `required` — зірочка біля підпису; `error` — ключ помилки під плашками.
 *
 * Показують форма нового колеги й «Редагувати» колегу (з правом `AssignRoleToUser`).
 */
@Component({
  selector: 'app-role-choices',
  templateUrl: './role-choices.component.html',
  styleUrls: ['../../settings/settings-section.scss', './role-choices.component.scss'],
  imports: [RolePillComponent, TranslatePipe],
})
export class RoleChoicesComponent {
  readonly roles = input.required<RolesView>();
  /** Вибрані ролі (`id`). */
  readonly selected = input.required<ReadonlySet<string>>();
  readonly required = input(false);
  /** Ключ у мовному файлі помилки під плашками; `null` — без помилки. */
  readonly error = input<string | null>(null);
  /** Натиснули на плашку ролі (`id`). */
  readonly roleToggle = output<string>();
}
