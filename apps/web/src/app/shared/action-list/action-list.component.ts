import { Component, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

/** Пункт меню дій: яка дія (`id`), ключ підпису в мовному файлі й іконка. */
export interface ActionListItem<T extends string = string> {
  readonly id: T;
  readonly labelKey: string;
  readonly icon: string;
  /** Дія запланована, але ще не готова: пункт у розмітці, проте прихований класом `planned`. */
  readonly planned?: boolean;
}

/**
 * Список дій — вміст спливаючого вікна (`app-popover`), що відкривається кнопкою з трьома
 * крапками (рядок таблиці колег, картка колеги, матеріал чи папка «Бази знань»). Сам нічого не
 * вирішує: які пункти показати задає той, хто його використовує, а вибір віддає подією `choose`
 * (з `id` пункту).
 */
@Component({
  selector: 'app-action-list',
  templateUrl: './action-list.component.html',
  styleUrls: ['./action-list.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class ActionListComponent<T extends string = string> {
  readonly items = input.required<readonly ActionListItem<T>[]>();
  /** Вибрали дію. */
  readonly choose = output<T>();
}
