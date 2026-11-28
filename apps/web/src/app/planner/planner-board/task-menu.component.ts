import { Component, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { ActionListComponent, ActionListItem } from '../../shared/action-list/action-list.component';
import { DialogHeaderComponent } from '../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { PopoverComponent } from '../../shared/popover/popover.component';

/** Дія із завданням: детальніше, редагувати чи видалити. */
export type TaskAction = 'details' | 'edit' | 'delete';

const ITEMS: readonly ActionListItem<TaskAction>[] = [
  { id: 'details', labelKey: 'planner.taskActions.details', icon: 'information-circle-outline' },
  { id: 'edit', labelKey: 'planner.taskActions.edit', icon: 'create-outline' },
  { id: 'delete', labelKey: 'planner.taskActions.delete', icon: 'trash-outline' },
];

/**
 * Меню трьох крапок картки завдання (як меню колонки): плашка (`app-popover`) зі списком дій
 * (`app-action-list`) «Детальніше», «Редагувати» (лише з `canEdit`) й «Видалити» (лише з
 * `canDelete`); відкривається `open(anchor, task)` під кнопкою. «Детальніше» й «Редагувати» віддають
 * завдання (`details`, `edit`), «Видалити» перепитує з назвою завдання в лапках і видаляє його
 * (`TasksService.remove`); невдача — причина у вікні.
 */
@Component({
  selector: 'app-task-menu',
  templateUrl: './task-menu.component.html',
  imports: [ActionListComponent, ConfirmDialogComponent, DialogHeaderComponent, ModalComponent, PopoverComponent, TranslatePipe],
})
export class TaskMenuComponent {
  /** Проєкт завдань. */
  readonly projectId = input.required<string>();
  /** Чи можна змінювати завдання (`TaskAccessService.canEdit`): інакше «Редагувати» нема. */
  readonly canEdit = input(false);
  /** Чи можна видаляти завдання (`TaskAccessService.canDelete`): інакше «Видалити» нема. */
  readonly canDelete = input(false);
  readonly details = output<ProjectTask>();
  readonly edit = output<ProjectTask>();
  /** Меню закрилося (будь-яким способом). */
  readonly closed = output<void>();

  private readonly popover = viewChild.required(PopoverComponent);
  private readonly modal = viewChild.required(ModalComponent);

  protected readonly items = computed(() =>
    ITEMS.filter((item) => (item.id !== 'edit' || this.canEdit()) && (item.id !== 'delete' || this.canDelete())),
  );
  private readonly tasks = inject(TasksService);
  /** Видалення йде. */
  protected readonly busy = signal(false);
  /** Помилка видалення (ключ мовного файлу). */
  protected readonly error = signal<string | null>(null);
  /** Завдання, для якого відкрите меню. */
  private readonly target = signal<ProjectTask | null>(null);
  /** Відкрите вікно: дія й завдання. */
  protected readonly dialog = signal<{ readonly action: TaskAction; readonly task: ProjectTask } | null>(null);

  /** Відкриває меню під кнопкою `anchor` для завдання `task`. */
  open(anchor: Event | HTMLElement, task: ProjectTask): void {
    this.target.set(task);
    this.popover().open(anchor);
  }

  protected async onChoose(action: TaskAction): Promise<void> {
    const task = this.target();
    // Вікно відкриваємо, коли меню вже закрилося: інакше фокус повернувся б на три крапки під вікном.
    await this.popover().close();
    if (!task) {
      return;
    }
    if (action === 'details') {
      this.details.emit(task);
    } else if (action === 'edit') {
      this.edit.emit(task);
    } else {
      this.busy.set(false);
      this.error.set(null);
      this.dialog.set({ action, task });
      this.modal().open();
    }
  }

  /** «Так» у перепитуванні: видаляє завдання; успіх закриває вікно. */
  protected async remove(task: ProjectTask): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const result = await this.tasks.remove(this.projectId(), task.id);
    this.busy.set(false);
    if (result === 'done') {
      void this.close();
    } else {
      this.error.set(`planner.tasks.failure.${result}`);
    }
  }

  protected close(): Promise<void> {
    return this.modal().close();
  }

  protected onClosed(): void {
    this.dialog.set(null);
  }
}
