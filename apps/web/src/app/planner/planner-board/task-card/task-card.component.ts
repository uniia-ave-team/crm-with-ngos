import { Component, computed, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { ProjectTask } from '../../../core/tasks/tasks.service';
import { TaskAssigneesComponent } from '../../task-assignees/task-assignees.component';
import { TaskStatusComponent } from '../../task-status/task-status.component';

/** День `YYYY-MM-DD` як «20.03.2026» (ДД.ММ.РРРР). */
export function formatDay(day: string): string {
  const [year, month, date] = day.split('-');
  return `${date}.${month}.${year}`;
}

/**
 * Картка завдання на дошці: назва, дати («20.03.2026 - 25.03.2026»; без кінця — «з 20.03.2026»),
 * тег стану (`app-task-status`: з `canEdit` — підвищення кліком) й пріоритет (кольори — за станом і пріоритетом, свої для світлої й темної теми), унизу —
 * коментарі («N нових коментарів» з червоним кружком на іконці; нових нема — просто «Коментарі»)
 * і виконавці (`app-task-assignees`). Клік по картці (чи Enter / пробіл на ній) — `open`. Наведення
 * (чи фокус) показує в правому верхньому куті три крапки — `menu` (меню дій із завданням); назва під
 * ними обрізається, а висота картки не змінюється.
 */
@Component({
  selector: 'app-task-card',
  templateUrl: './task-card.component.html',
  styleUrls: ['../../task-labels.scss', './task-card.component.scss'],
  imports: [IonIcon, TaskAssigneesComponent, TaskStatusComponent, TranslatePipe],
})
export class TaskCardComponent {
  readonly task = input.required<ProjectTask>();
  /** Проєкт завдання (для підвищення стану). */
  readonly projectId = input.required<string>();
  /** Чи можна змінювати завдання (`UpdateTask`): тоді стан підвищується кліком по тегу. */
  readonly canEdit = input(false);
  /** Три крапки: кнопка (під нею — меню) і завдання. */
  readonly menu = output<{ readonly anchor: Event; readonly task: ProjectTask }>();
  /** Клік по картці (чи Enter / пробіл на ній): відкрити завдання. */
  readonly open = output<void>();


  /** Дати завдання; без кінця — від початку (ключ і дні для перекладу). */
  protected readonly dates = computed(() => {
    const task = this.task();
    const start = formatDay(task.startDate);
    return task.endDate
      ? { key: 'planner.task.dates', start, end: formatDay(task.endDate) }
      : { key: 'planner.task.from', start, end: '' };
  });
  /** Три крапки: меню, не відкриваючи саме завдання. */
  protected onMenu(event: Event): void {
    event.stopPropagation();
    this.menu.emit({ anchor: event, task: this.task() });
  }

  /** Enter чи пробіл на самій картці (не на трьох крапках): відкрити завдання. */
  protected onKey(event: Event): void {
    if (event.target === event.currentTarget) {
      event.preventDefault();
      this.open.emit();
    }
  }
}
