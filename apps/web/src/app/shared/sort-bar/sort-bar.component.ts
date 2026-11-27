import { Component, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

/** Напрям сортування: від меншого до більшого (А–Я) чи навпаки. */
export type SortBarDirection = 'asc' | 'desc';

/** Поле, за яким можна сортувати: `id` і ключ підпису в мовному файлі. */
export interface SortBarOption {
  readonly id: string;
  readonly labelKey: string;
}

/** Вибране сортування: поле й напрям. */
export interface SortBarValue {
  readonly id: string;
  readonly direction: SortBarDirection;
}

/**
 * Рядок сортування для вузьких екранів, де нема заголовків таблиці: «Сортувати за», список полів
 * (`options`) і кнопка напряму (клік перемикає А–Я ↔ Я–А; іконки — як у заголовках таблиці).
 * Поточне сортування — `value`; зміна поля чи напряму — подія `sortChange` з новим значенням,
 * сам рядок нічого не запитує.
 *
 *   <app-sort-bar [options]="…" [value]="…" (sortChange)="sort($event)" />
 */
@Component({
  selector: 'app-sort-bar',
  templateUrl: './sort-bar.component.html',
  styleUrls: ['./sort-bar.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class SortBarComponent {
  readonly options = input.required<readonly SortBarOption[]>();
  readonly value = input.required<SortBarValue>();
  readonly sortChange = output<SortBarValue>();

  protected onField(id: string): void {
    this.sortChange.emit({ id, direction: this.value().direction });
  }

  protected toggleDirection(): void {
    const { id, direction } = this.value();
    this.sortChange.emit({ id, direction: direction === 'asc' ? 'desc' : 'asc' });
  }
}
