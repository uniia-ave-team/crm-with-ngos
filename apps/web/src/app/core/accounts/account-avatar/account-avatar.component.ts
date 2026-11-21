import { Component, input } from '@angular/core';
import { IonIcon } from '@ionic/angular';

import { AccountTile } from './account-tile';
import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { AlphaOutlineDirective } from '../../../shared/alpha-outline/alpha-outline.directive';
import { ProtectedSrcDirective } from '../../../shared/protected-src/protected-src.directive';

/**
 * Аватар акаунта зі значками: стан (`outdated`, `unavailable`) ліворуч унизу й логотип ГО
 * праворуч унизу, обидва з контуром за альфа-каналом. Розмір — `--avatar-size` (44px за
 * замовчуванням). Використовується в плитках меню акаунтів і в списку на сторінці повторного входу.
 */
@Component({
  selector: 'app-account-avatar',
  templateUrl: './account-avatar.component.html',
  styleUrls: ['./account-avatar.component.scss'],
  imports: [AvatarComponent, IonIcon, AlphaOutlineDirective, ProtectedSrcDirective],
})
export class AccountAvatarComponent {
  readonly account = input.required<AccountTile>();
}
