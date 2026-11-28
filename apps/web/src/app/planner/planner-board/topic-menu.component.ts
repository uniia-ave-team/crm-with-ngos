import { Component, inject, input, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { ProjectTopic, ProjectsService } from '../../core/projects/projects.service';
import { TasksService } from '../../core/tasks/tasks.service';
import { TopicFormComponent } from '../../core/projects/topic-form/topic-form.component';
import { ActionListComponent, ActionListItem } from '../../shared/action-list/action-list.component';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { DialogHeaderComponent } from '../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { PopoverComponent } from '../../shared/popover/popover.component';

/** Дія з колонкою дошки: редагувати чи видалити. */
export type TopicAction = 'edit' | 'delete';

const ITEMS: readonly ActionListItem<TopicAction>[] = [
  { id: 'edit', labelKey: 'planner.topicActions.edit', icon: 'create-outline' },
  { id: 'delete', labelKey: 'planner.topicActions.delete', icon: 'trash-outline' },
];

/**
 * Меню трьох крапок колонки дошки (як меню проєкту): плашка (`app-popover`) зі списком дій
 * (`app-action-list`), відкривається `open(anchor, topic)` під кнопкою. Вибір відкриває вікно по
 * центру екрана з назвою дії й хрестиком:
 * - «Редагувати» — та сама форма, що й «Нова колонка» (`app-topic-form`), з назвою колонки й
 *   «Зберегти» (`ProjectsService.updateTopic`; назву не змінили — вікно просто закривається);
 * - «Видалити» — спершу перевірка, чи є в колонці завдання (`TasksService.page` з `TopicId`): є —
 *   повідомлення, що колонку з завданнями видалити не можна (сервер її не видаляє); нема —
 *   підтвердження з назвою колонки (`removeTopic`; якщо завдання тим часом зʼявилися, сервер
 *   відмовить — те саме повідомлення).
 * Успіх закриває вікно (дошка перепитує колонки), невдача — причина у вікні.
 */
@Component({
  selector: 'app-topic-menu',
  templateUrl: './topic-menu.component.html',
  styleUrls: ['./topic-menu.component.scss'],
  imports: [ActionListComponent, ConfirmDialogComponent, DialogHeaderComponent, ModalComponent, PopoverComponent, TopicFormComponent, TranslatePipe],
})
export class TopicMenuComponent {
  /** Проєкт, чия дошка. */
  readonly projectId = input.required<string>();

  private readonly projects = inject(ProjectsService);
  private readonly tasks = inject(TasksService);
  /** «Видалити»: перевіряємо завдання колонки (`checking`), у ній є завдання (`blocked`) чи можна (`ready`). */
  protected readonly removal = signal<'checking' | 'blocked' | 'ready'>('checking');
  private readonly popover = viewChild.required(PopoverComponent);
  private readonly modal = viewChild.required(ModalComponent);

  protected readonly items = ITEMS;
  /** Колонка, для якої відкрите меню. */
  private readonly target = signal<ProjectTopic | null>(null);
  /** Відкрите вікно: дія й колонка. */
  protected readonly dialog = signal<{ readonly action: TopicAction; readonly topic: ProjectTopic } | null>(null);
  /** Запит із вікна йде. */
  protected readonly busy = signal(false);
  /** Помилка запиту з вікна (ключ мовного файлу). */
  protected readonly error = signal<string | null>(null);

  /** Відкриває меню під кнопкою `anchor` для колонки `topic`. */
  open(anchor: Event | HTMLElement, topic: ProjectTopic): void {
    this.target.set(topic);
    this.popover().open(anchor);
  }

  protected async onChoose(action: TopicAction): Promise<void> {
    const topic = this.target();
    // Вікно відкриваємо, коли меню вже закрилося: інакше фокус повернувся б на три крапки під вікном.
    await this.popover().close();
    if (topic) {
      this.busy.set(false);
      this.error.set(null);
      this.dialog.set({ action, topic });
      this.modal().open();
      if (action === 'delete') {
        void this.checkTasks(topic);
      }
    }
  }

  /** Чи є в колонці завдання: є — видалити не можна; не вдалося перевірити — хай вирішує сервер. */
  private async checkTasks(topic: ProjectTopic): Promise<void> {
    this.removal.set('checking');
    const page = await this.tasks.page(this.projectId(), 1, 1, { topicId: topic.id });
    if (this.dialog()?.topic === topic) {
      this.removal.set(typeof page !== 'string' && page.items.length ? 'blocked' : 'ready');
    }
  }

  protected async save(topic: ProjectTopic, title: string): Promise<void> {
    if (title === topic.title) {
      void this.close();
      return;
    }
    await this.run(() => this.projects.updateTopic(this.projectId(), topic.id, title));
  }

  protected async remove(topic: ProjectTopic): Promise<void> {
    await this.run(() => this.projects.removeTopic(this.projectId(), topic.id));
    if (this.error() === 'planner.dialogs.failure.hasTasks') {
      this.error.set(null);
      this.removal.set('blocked');
    }
  }

  /** Виконує запит вікна: успіх — закриває вікно, невдача — показує помилку в ньому. */
  private async run(request: () => Promise<string>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    const result = await request();
    this.busy.set(false);
    if (result === 'done') {
      void this.close();
    } else {
      this.error.set(`planner.dialogs.failure.${result}`);
    }
  }

  protected close(): Promise<void> {
    return this.modal().close();
  }

  protected onClosed(): void {
    this.dialog.set(null);
  }
}
