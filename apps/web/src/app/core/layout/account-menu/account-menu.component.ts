import { Component, input, output } from '@angular/core';

/** Акаунт у списку: знімок із запису акаунта (імʼя, аватар, назва й логотип ГО). */
export interface AccountTile {
  readonly id: string | number;
  readonly firstName: string | null | undefined;
  readonly lastName: string | null | undefined;
  readonly avatarUrl: string | null | undefined;
  /** Назва організації акаунта (впливає на колір аватара). */
  readonly ngoName?: string | null;
  /** Логотип ГО (будь-яке зображення з прозорістю); без нього значка на плитці нема. */
  readonly logoUrl?: string | null;
  /** Поточний (активний) акаунт: плитка отримує клас `active` і білу рамку. */
  readonly active?: boolean;
  /** Сесію акаунта завершено (токени відхилено): сірий аватар і жовтий значок помилки ліворуч. */
  readonly outdated?: boolean;
  /**
   * Сервер акаунта не відповів. Незалежний від `outdated`: сам по собі дає жовтий значок
   * синхронізації, а разом із `outdated` — червоний значок помилки.
   */
  readonly unavailable?: boolean;
}

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
}
