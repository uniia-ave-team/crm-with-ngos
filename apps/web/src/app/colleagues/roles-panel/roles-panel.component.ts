import { Component, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { Role } from '../../core/roles/roles.service';
import { RolePillComponent } from '../../shared/role-pill/role-pill.component';
import { RolesView } from '../roles-view';

/**
 * Зміст бічного вікна «Ролі»: заголовок і ролі плашками (`app-role-pill`, колір за
 * назвою), по краях рядка кнопки «Редагувати» й «Видалити». Сам нічого не завантажує: стан приходить входом `view`.
 */
@Component({
  selector: 'app-roles-panel',
  templateUrl: './roles-panel.component.html',
  styleUrls: ['./roles-panel.component.scss'],
  imports: [RolePillComponent, IonIcon, TranslatePipe],
})
export class RolesPanelComponent {
  readonly view = input.required<RolesView>();
  /** Ключ у мовному файлі повідомлення над списком (наприклад, після створення ролі); `null` — нема. */
  readonly notice = input<string | null>(null);
  /** Показувати кнопки «Редагувати» й «Видалити» (за замовчуванням ні: їх відкриває право на керування ролями). */
  readonly canManage = input(false);
  /** Натиснули «Редагувати» (олівець) біля ролі. */
  readonly roleEdit = output<Role>();
  /** Натиснули «Видалити» (смітник) біля ролі. */
  readonly roleDelete = output<Role>();
}
