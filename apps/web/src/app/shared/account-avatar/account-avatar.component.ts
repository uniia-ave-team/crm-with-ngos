import { Component, input } from '@angular/core';

import { AccountTile } from './account-tile';

/**
 * Аватар акаунта зі значками: стан (`outdated`, `unavailable`) ліворуч унизу й логотип ГО
 * праворуч унизу, обидва з контуром за альфа-каналом. Розмір — `--avatar-size` (44px за
 * замовчуванням). Використовується в плитках меню акаунтів і в списку на сторінці повторного входу.
 */
@Component({
  selector: 'app-account-avatar',
  templateUrl: './account-avatar.component.html',
  styleUrls: ['./account-avatar.component.scss'],
  standalone: false,
})
export class AccountAvatarComponent {
  readonly account = input.required<AccountTile>();
}
