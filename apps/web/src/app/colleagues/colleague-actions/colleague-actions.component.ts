import { Component, input, output } from '@angular/core';

/** Дія з колегою в меню біля трьох крапок. */
export type ColleagueAction = 'permissions' | 'edit' | 'resetPassword' | 'disable' | 'delete';

/** Пункт меню: яка дія, ключ підпису в мовному файлі й іконка. */
export interface ColleagueActionItem {
  readonly id: ColleagueAction;
  readonly labelKey: string;
  readonly icon: string;
}

/**
 * Список дій з колегою — вміст спливаючого вікна (`app-popover`), що відкривається
 * кнопкою з трьома крапками в кінці рядка таблиці. Сам нічого не вирішує: які
 * пункти показати (за правами) задає той, хто його використовує, а вибір віддає
 * подією `choose`.
 */
@Component({
  selector: 'app-colleague-actions',
  templateUrl: './colleague-actions.component.html',
  styleUrls: ['./colleague-actions.component.scss'],
  standalone: false,
})
export class ColleagueActionsComponent {
  readonly items = input.required<readonly ColleagueActionItem[]>();
  /** Вибрали дію. */
  readonly choose = output<ColleagueAction>();
}
