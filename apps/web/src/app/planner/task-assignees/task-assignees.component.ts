import { Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { TaskAssignee } from '../../core/tasks/tasks.service';
import { AvatarComponent } from '../../shared/avatar/avatar.component';


/**
 * Виконавці завдання (картка на дошці, таблиця «Моїх завдань», бічне вікно): аватарки внахлест, як
 * учасники в шапці (`max`, за замовчуванням дві, далі «...»; підказка — імена всіх). Виконавців нема — порожня аватарка з пунктирним
 * контуром («Без виконавців»).
 */
@Component({
  selector: 'app-task-assignees',
  templateUrl: './task-assignees.component.html',
  styleUrls: ['./task-assignees.component.scss'],
  imports: [AvatarComponent, TranslatePipe],
})
export class TaskAssigneesComponent {
  readonly assignees = input.required<readonly TaskAssignee[]>();
  /** Скільки аватарок видно; решта — аватаркою з «...». */
  readonly max = input(2);

  private readonly accounts = inject(AccountsService);

  protected readonly shown = computed(() => this.assignees().slice(0, this.max()));
  protected readonly hidden = computed(() => Math.max(this.assignees().length - this.max(), 0));
  /** Імена виконавців — у підказці й назві для читачів екрана. */
  protected readonly names = computed(() =>
    this.assignees()
      .map((assignee) => `${assignee.lastName} ${assignee.firstName}`.trim())
      .join(', '),
  );
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');
}
