import { Component, computed, inject, input, linkedSignal, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { TASK_COMMENT_MAX_LENGTH, TaskComment } from '../../core/tasks/tasks.service';
import { AvatarComponent } from '../../shared/avatar/avatar.component';

/**
 * Коментар у бічному вікні завдання: аватар, автор (`author`, менший шрифт) і час (`time`), текст
 * тонким шрифтом. Свій коментар (`own`) при наведенні має три крапки (`menu` — меню дій). У
 * редагуванні (`editing`) текст — поле з ним, а замість трьох крапок — завжди видима «Надіслати»
 * (Enter чи кнопка — `save` з новим текстом); `busy` — запит іде.
 */
@Component({
  selector: 'app-task-comment',
  templateUrl: './task-comment.component.html',
  styleUrls: ['./task-comment.component.scss'],
  imports: [AvatarComponent, IonIcon, TranslatePipe],
})
export class TaskCommentComponent {
  readonly comment = input.required<TaskComment>();
  readonly author = input('');
  readonly time = input('');
  readonly own = input(false);
  readonly editing = input(false);
  readonly busy = input(false);
  readonly menu = output<Event>();
  readonly save = output<string>();

  private readonly accounts = inject(AccountsService);

  protected readonly maxLength = TASK_COMMENT_MAX_LENGTH;
  /** Текст у редагуванні (спершу — поточний). */
  protected readonly text = linkedSignal(() => (this.editing() ? this.comment().content : ''));
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');

  protected onText(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
  }

  protected submit(): void {
    if (!this.busy() && this.text().trim()) {
      this.save.emit(this.text());
    }
  }
}
