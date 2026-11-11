import { Component, input, output } from '@angular/core';

import { AccountTile, accountTooltip } from '../../../shared/account-avatar/account-tile';

export type { AccountTile };

/**
 * Зміст вікна акаунта (відкривається клацанням по аватару в меню): акаунти
 * користувача в різних середовищах (аватар + логотип ГО), кнопка додавання
 * ще одного, «Налаштування профілю», лінія й «Вийти». Поки лише вигляд:
 * події віддаємо назовні, самі нічого не робимо.
 */
@Component({
  selector: 'app-account-menu',
  templateUrl: './account-menu.component.html',
  styleUrls: ['./account-menu.component.scss'],
  standalone: false,
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
