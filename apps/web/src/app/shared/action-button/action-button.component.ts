import { Component, computed, inject, input } from '@angular/core';
import { IonIcon } from '@ionic/angular';

import { SearchHeaderComponent } from '../search-header/search-header.component';

/** Колір кнопки: синій, зелений чи нейтральний (тло й текст меню). */
export type ActionButtonTone = 'blue' | 'green' | 'neutral';

/**
 * Кнопка дії з іконкою й текстом: у шапці сторінки, у шапці бічного вікна.
 *
 *   <app-action-button icon="add-outline" tone="green" [label]="…" (click)="add()" />
 *
 * Назва кнопки є і в `aria-label`, бо в шапці з пошуком (`app-search-header`) текст може
 * зникнути: коли пошук відкрито, кнопка плавно стискається до іконки (ширина тексту
 * `grid-template-columns` 1fr → 0fr, тією ж тривалістю, що й рух шапки), а на телефоні
 * (до 600px) у такій шапці завжди лише іконка. Поза шапкою з пошуком текст видно завжди.
 *
 * Висоту задає змінна `--action-button-height` (за замовчуванням 36px).
 */
@Component({
  selector: 'app-action-button',
  templateUrl: './action-button.component.html',
  styleUrls: ['./action-button.component.scss'],
  host: {
    '[class.action-button--in-header]': 'inHeader',
    '[class.action-button--compact]': 'compact()',
  },
  imports: [IonIcon],
})
export class ActionButtonComponent {
  /** Назва іконки Ionicons. */
  readonly icon = input.required<string>();
  readonly label = input.required<string>();
  readonly tone = input<ActionButtonTone>('neutral');

  private readonly header = inject(SearchHeaderComponent, { optional: true });
  protected readonly inHeader = this.header !== null;
  /** Стиснута до іконки: шапка в режимі пошуку. */
  protected readonly compact = computed(() => this.header?.open() ?? false);
}
