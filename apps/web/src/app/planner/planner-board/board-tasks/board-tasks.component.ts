import { Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { ProjectTask, TasksService } from '../../../core/tasks/tasks.service';
import { TaskCardComponent } from '../task-card/task-card.component';

/** Скільки завдань колонки за запит (більше сервер не віддає); сторінки йдуть одна за одною до кінця. */
export const BOARD_TASKS_PAGE_SIZE = 100;

/** Стан списку колонки. */
interface BoardTasksState {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly tasks: readonly ProjectTask[];
}

/**
 * Картки завдань однієї колонки дошки (`app-task-card`): усі завдання колонки — `TasksService.page`
 * з `TopicId`, по `BOARD_TASKS_PAGE_SIZE`, сторінка за сторінкою до кінця. Список прокручується
 * всередині колонки. Після змін завдань (`TasksService.version`) — заново (показані лишаються, поки
 * йдуть запити). Клік по картці — `openTask`, три крапки — `menu`.
 */
@Component({
  selector: 'app-board-tasks',
  templateUrl: './board-tasks.component.html',
  styleUrls: ['./board-tasks.component.scss'],
  imports: [TaskCardComponent, TranslatePipe],
})
export class BoardTasksComponent {
  readonly projectId = input.required<string>();
  readonly topicId = input.required<string>();
  /** Назва колонки (назва списку для читачів екрана). */
  readonly label = input('');
  /** Чи можна змінювати завдання (`UpdateTask`): підвищення стану на картці. */
  readonly canEdit = input(false);
  readonly openTask = output<ProjectTask>();
  readonly menu = output<{ readonly anchor: Event; readonly task: ProjectTask }>();

  private readonly tasks = inject(TasksService);

  protected readonly state = signal<BoardTasksState>({ status: 'loading', tasks: [] });
  /** «Покоління» списку: відповідь на запит до перезавантаження нічого не змінює. */
  private generation = 0;

  constructor() {
    effect(() => {
      this.projectId();
      this.topicId();
      this.tasks.version();
      untracked(() => void this.reload());
    });
  }

  /** Усі сторінки колонки заново; показані лишаються, поки не прийде все. */
  protected async reload(): Promise<void> {
    const generation = ++this.generation;
    this.state.update((state) => ({ ...state, status: 'loading' }));
    const tasks: ProjectTask[] = [];
    for (let page = 1; ; page++) {
      const result = await this.tasks.page(this.projectId(), page, BOARD_TASKS_PAGE_SIZE, { topicId: this.topicId() });
      if (generation !== this.generation) {
        return;
      }
      if (typeof result === 'string') {
        this.state.update((state) => ({ ...state, status: 'failed' }));
        return;
      }
      const known = new Set(tasks.map((task) => task.id));
      tasks.push(...result.items.filter((task) => !known.has(task.id)));
      if (!result.hasNextPage || !result.items.length) {
        this.state.set({ status: 'ready', tasks });
        return;
      }
    }
  }
}
