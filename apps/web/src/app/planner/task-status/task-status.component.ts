import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { ProjectTask, TaskStatus, TasksService } from '../../core/tasks/tasks.service';

/** Наступний стан при підвищенні: «До виконання» → «В процесі» → «Готово»; з інших — нема. */
const NEXT_STATUS: Partial<Record<TaskStatus, TaskStatus>> = { 0: 1, 1: 3 };

/**
 * Тег стану завдання (картка на дошці, таблиця «Моїх завдань»). З правом змінювати (`canPromote`)
 * і якщо є наступний стан («До виконання» → «В процесі» → «Готово») тег — кнопка: наведення (чи
 * фокус) показує наступний стан синім зі стрілкою вгору, клік підвищує його — `PUT` завдання
 * (`TasksService.update`, решта даних та сама). Поки сервер відповідає, тег — уже новий стан у його
 * кольорах, з хмарою оновлення замість стрілки; потім список перечитується й тег показує новий стан.
 * Після кліку підказка наступного стану не зʼявляється, поки курсор не вийде з тегу. Невдача чи
 * наведення без кліку — тег знову поточний. Клік не відкриває саме завдання.
 */
@Component({
  selector: 'app-task-status',
  templateUrl: './task-status.component.html',
  styleUrls: ['../task-labels.scss', './task-status.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class TaskStatusComponent {
  readonly task = input.required<ProjectTask>();
  readonly projectId = input.required<string>();
  /** Чи можна змінювати завдання (`UpdateTask`). */
  readonly canPromote = input(false);

  private readonly tasks = inject(TasksService);

  /** Наступний стан (`null` — підвищувати нікуди чи не можна). */
  protected readonly next = computed(() => (this.canPromote() ? (NEXT_STATUS[this.task().status] ?? null) : null));
  protected readonly hovered = signal(false);
  /** Після кліку підказка наступного стану не показується, поки курсор не вийде з тегу. */
  private readonly clicked = signal(false);
  /** Стан, до якого підвищуємо (запит іде чи список ще не перечитано); `null` — нема. */
  private readonly promotedTo = signal<TaskStatus | null>(null);
  /** Запит іде: тег підвищений, з хмарою оновлення (поки завдання не прийде з новим станом). */
  protected readonly pending = computed(() => {
    const target = this.promotedTo();
    return target !== null && this.task().status !== target;
  });
  /** Що показує тег: підвищений (наведення чи запит) або поточний. */
  protected readonly shown = computed(() => {
    const next = this.next();
    if (this.pending()) {
      return this.promotedTo()!;
    }
    return this.raised() && next !== null ? next : this.task().status;
  });
  /** Підказка наступного стану (синім зі стрілкою): наведення без кліку. */
  protected readonly raised = computed(() => !this.pending() && !this.clicked() && this.hovered() && this.next() !== null);

  protected onEnter(): void {
    this.hovered.set(true);
  }

  protected onLeave(): void {
    this.hovered.set(false);
    this.clicked.set(false);
  }

  constructor() {
    // Завдання прийшло з новим станом — підвищення завершене.
    effect(() => {
      if (this.promotedTo() === this.task().status) {
        this.promotedTo.set(null);
      }
    });
  }

  protected async promote(event: Event): Promise<void> {
    event.stopPropagation();
    const next = this.next();
    if (next === null || this.pending()) {
      return;
    }
    const task = this.task();
    this.clicked.set(true);
    this.promotedTo.set(next);
    const result = await this.tasks.update(
      this.projectId(),
      task,
      {
        title: task.title,
        description: task.description,
        status: next,
        priority: task.priority,
        assigneeIds: task.assignees.map((assignee) => assignee.id),
        tags: task.tags.map((tag) => tag.title),
        startDate: task.startDate,
        endDate: task.endDate,
      },
      task.tags.map((tag) => tag.id),
    );
    if (result !== 'done') {
      this.promotedTo.set(null);
    }
  }
}
