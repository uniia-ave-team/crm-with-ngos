import { Component, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountTile, accountTooltip } from '../../accounts/account-avatar/account-tile';
import { AccountAvatarComponent } from '../../accounts/account-avatar/account-avatar.component';

/**
 * Зміст вікна акаунта (відкривається клацанням по аватару в меню): акаунти
 * користувача в різних середовищах (аватар + логотип ГО), кнопка додавання
 * ще одного, «Налаштування профілю», лінія й «Вийти». Компонент лише показує й
 * сповіщає подіями, що вибрали; що робити далі, вирішує меню (`primary-nav`).
 */
@Component({
  selector: 'app-account-menu',
  templateUrl: './account-menu.component.html',
  styleUrls: ['./account-menu.component.scss'],
  imports: [AccountAvatarComponent, IonIcon, TranslatePipe],
})
export class AccountMenuComponent {
  readonly accounts = input<readonly AccountTile[]>([]);

  readonly selectAccount = output<AccountTile>();
  readonly addAccount = output<void>();
  readonly settings = output<void>();
  readonly signOut = output<void>();

  /**
   * Підказка плитки (її показує сам браузер при наведенні): у першому рядку «ПІБ — Назва ГО»
   * (що з цього є), у другому — стан акаунта, якщо з ним негаразд (`accountTooltip`). Нічого
   * показати — `null`.
   */
  protected tooltip(account: AccountTile, unavailableText: string, outdatedText: string): string | null {
    return accountTooltip(account, unavailableText, outdatedText);
  }
}
