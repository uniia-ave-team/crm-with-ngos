import { Component, input, output } from '@angular/core';

/**
 * Зміст вікна підтвердження (ставиться в `app-modal`): питання, необовʼязкова помилка й дві
 * кнопки — підтвердити (небезпечна дія, червона) і скасувати. Тексти приходять уже
 * перекладеними. Поки дія виконується (`busy`), кнопка підтвердження вимкнена.
 *
 *   <app-modal width="min(356px, calc(100vw - 32px))" [label]="…">
 *     <app-confirm-dialog [message]="…" confirmLabel="…" cancelLabel="…" (confirm)="…" (dismiss)="…" />
 *   </app-modal>
 */
@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrls: ['./confirm-dialog.component.scss'],
})
export class ConfirmDialogComponent {
  /** Питання. */
  readonly message = input.required<string>();
  /** Напис кнопки підтвердження. */
  readonly confirmLabel = input.required<string>();
  /** Напис кнопки скасування. */
  readonly cancelLabel = input.required<string>();
  /** Дія виконується: кнопка підтвердження вимкнена. */
  readonly busy = input(false);
  /** Помилка попередньої спроби (вже перекладена) або `null`. */
  readonly error = input<string | null>(null);

  readonly confirm = output<void>();
  readonly dismiss = output<void>();
}
