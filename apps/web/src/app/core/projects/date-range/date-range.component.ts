import { Component, computed, input, model } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

/** Сьогоднішній день (за часом пристрою) у форматі поля дати, `YYYY-MM-DD`. */
export function today(): string {
  const now = new Date();
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** Чи підходять дати: початок є, а кінця нема чи він не раніше за початок (`YYYY-MM-DD` порівнюються як рядки). */
export function isDateRangeValid(start: string, end: string): boolean {
  return Boolean(start) && (!end || end >= start);
}

/**
 * «Дата початку» (обовʼязкова) і «Дата кінця» (необовʼязкова, не раніше за початок; скидається
 * хрестиком у кінці поля) — поля дати браузера поруч двома стовпчиками, а у вузькому вікні одне під
 * одним (форми проєкту й завдання). Значення — `YYYY-MM-DD` (`start`, `end`; без кінця — `''`).
 * Помилки полів — коли форма вже пробувала відправитися (`showErrors`); чи можна відправляти,
 * форма перевіряє сама (`isDateRangeValid`). `id` полів — `{idPrefix}-start` і `{idPrefix}-end`.
 */
@Component({
  selector: 'app-date-range',
  templateUrl: './date-range.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './date-range.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class DateRangeComponent {
  /** Початок `id` полів (у формі вони мають бути унікальні). */
  readonly idPrefix = input.required<string>();
  readonly start = model.required<string>();
  readonly end = model('');
  /** Показувати помилки (форма вже пробувала відправитися). */
  readonly showErrors = input(false);

  protected readonly startError = computed(() => (this.showErrors() && !this.start() ? 'common.required' : null));
  protected readonly endError = computed(() =>
    this.showErrors() && this.start() && !isDateRangeValid(this.start(), this.end()) ? 'planner.dialogs.add.endBeforeStart' : null,
  );

  protected onStart(event: Event): void {
    this.start.set((event.target as HTMLInputElement).value);
  }

  protected onEnd(event: Event): void {
    this.end.set((event.target as HTMLInputElement).value);
  }

  /** Хрестик «Скинути дату кінця»: без кінця; курсор повертається в поле дати. */
  protected clearEnd(input: HTMLInputElement): void {
    this.end.set('');
    input.focus();
  }
}
