import { Component, TemplateRef, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { IonIcon, IonInfiniteScroll, IonInfiniteScrollContent } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/auth/permissions';
import { RequestFailure } from '../../core/http/request-failure';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import {
  DataTableCellContext,
  DataTableColumn,
  DataTableComponent,
  DataTableRow,
  DataTableRowMenuEvent,
  DataTableSort,
} from '../../shared/data-table/data-table.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { SortBarComponent, SortBarOption, SortBarValue } from '../../shared/sort-bar/sort-bar.component';
import { TaskCardComponent, formatDay } from '../planner-board/task-card/task-card.component';
import { TaskDialogComponent } from '../planner-board/task-dialog.component';
import { TaskMenuComponent } from '../planner-board/task-menu.component';
import { TaskAssigneesComponent } from '../task-assignees/task-assignees.component';
import { TaskStatusComponent } from '../task-status/task-status.component';

/** Скільки завдань за запит (решта — при прокрутці). */
export const MY_TASKS_PAGE_SIZE = 20;

/** Рядок таблиці: завдання. */
interface TaskRow extends DataTableRow {
  readonly task: ProjectTask;
}

/** Стан списку: завантажується, є (і чи є ще сторінки) чи не вдалося. */
type MyTasksState =
  | { readonly status: 'loading' | 'ready'; readonly tasks: readonly ProjectTask[]; readonly hasMore: boolean }
  | { readonly status: 'failed'; readonly tasks: readonly ProjectTask[]; readonly hasMore: boolean; readonly failure: RequestFailure | 'gone' };

/**
 * Вкладка «Мої завдання» планера: завдання проєкту, де користувач виконавець (`TasksService.page`
 * з `Filter=mine`; сервер спершу ставить ті, де більше нових коментарів), по `MY_TASKS_PAGE_SIZE`,
 * решта — при прокрутці. Таблиця (`app-data-table`, як у колег): назва (з іконкою коментарів —
 * червоний кружок, якщо є нові), стан (`app-task-status`: з `UpdateTask` — підвищення кліком), виконавці (`app-task-assignees`), часові рамки, пріоритет і
 * теги; клік по заголовку сортує на сервері (виконавці й теги — ні), пошук шапки (`search`) — теж
 * на сервері. На телефоні (до 768px) — ті самі картки, що й на дошці (`app-task-card`), а згори,
 * коли `sortOpen`, — рядок сортування (`app-sort-bar`), як у колег. Клік по рядку чи картці й
 * «Детальніше» — `openTask`; три крапки — меню завдання (`app-task-menu`, «Редагувати» з
 * `UpdateTask` — вікно завдання). Після змін завдань (`TasksService.version`) — список заново.
 */
@Component({
  selector: 'app-my-tasks',
  templateUrl: './my-tasks.component.html',
  styleUrls: ['../task-labels.scss', './my-tasks.component.scss'],
  imports: [
    DataTableComponent,
    EmptyStateComponent,
    IonIcon,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    SortBarComponent,
    TaskAssigneesComponent,
    TaskCardComponent,
    TaskDialogComponent,
    TaskMenuComponent,
    TaskStatusComponent,
    TranslatePipe,
  ],
})
export class MyTasksComponent {
  readonly projectId = input.required<string>();
  /** Текст пошуку з шапки (`SearchTerm`). */
  readonly search = input('');
  /** Рядок сортування на телефоні відкритий (кнопка з фільтром у шапці). */
  readonly sortOpen = input(false);
  /** Відкрити завдання в боковому вікні (клік по рядку чи картці, «Детальніше»). */
  readonly openTask = output<ProjectTask>();

  private readonly tasks = inject(TasksService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly menu = viewChild.required(TaskMenuComponent);

  private readonly titleCell = viewChild<TemplateRef<DataTableCellContext>>('titleCell');
  private readonly statusCell = viewChild<TemplateRef<DataTableCellContext>>('statusCell');
  private readonly assigneesCell = viewChild<TemplateRef<DataTableCellContext>>('assigneesCell');
  private readonly datesCell = viewChild<TemplateRef<DataTableCellContext>>('datesCell');
  private readonly priorityCell = viewChild<TemplateRef<DataTableCellContext>>('priorityCell');
  private readonly tagsCell = viewChild<TemplateRef<DataTableCellContext>>('tagsCell');

  private readonly taskAccess = inject(TaskAccessService);
  /** «Редагувати» в меню й підвищення стану — `UpdateTask` і доступ у проєкті (не переглядач). */
  protected readonly canEdit = computed(() => this.taskAccess.canEdit(this.projectId()));
  /** «Видалити» в меню — `DeleteTask` і доступ у проєкті. */
  protected readonly canDelete = computed(() => this.taskAccess.canDelete(this.projectId()));
  protected readonly state = signal<MyTasksState>({ status: 'loading', tasks: [], hasMore: false });
  /** Завдання, чиє меню відкрите (рядок лишається виділеним). */
  protected readonly menuTaskId = signal<string | null>(null);

  /**
   * Колонки (ширини в пікселях: на ширшому екрані розтягуються пропорційно; теги — утричі ширші);
   * `orderBy` — поле, за яким сортує сервер; відповідальних і теги сервер не сортує (`sortable: false`).
   */
  protected readonly columns = computed<readonly DataTableColumn[]>(() => [
    { id: 'title', labelKey: 'planner.myTasks.columns.title', width: '264px', template: this.titleCell(), orderBy: 'Title' },
    { id: 'status', labelKey: 'planner.myTasks.columns.status', width: '156px', template: this.statusCell(), orderBy: 'Status' },
    { id: 'assignees', labelKey: 'planner.myTasks.columns.assignees', width: '216px', template: this.assigneesCell(), sortable: false },
    { id: 'dates', labelKey: 'planner.myTasks.columns.dates', width: '168px', template: this.datesCell(), orderBy: 'StartDate' },
    { id: 'priority', labelKey: 'planner.myTasks.columns.priority', width: '180px', template: this.priorityCell(), orderBy: 'Priority' },
    { id: 'tags', labelKey: 'planner.myTasks.columns.tags', width: '648px', template: this.tagsCell(), sortable: false },
  ]);
  protected readonly rows = computed<readonly TaskRow[]>(() => this.state().tasks.map((task) => ({ id: task.id, task })));

  /** Сортування запиту: колонка й напрям; `null` — порядок сервера. */
  private readonly sort = signal<{ readonly id: string; readonly orderBy: string; readonly direction: 'asc' | 'desc' } | null>(null);
  /** Поля рядка сортування на телефоні — колонки, за якими сортує сервер. */
  protected readonly sortOptions = computed<readonly SortBarOption[]>(() =>
    this.columns()
      .filter((column) => !!column.orderBy)
      .map((column) => ({ id: column.id, labelKey: column.labelKey ?? '' })),
  );
  protected readonly sortValue = computed<SortBarValue>(() => {
    const sort = this.sort();
    return sort ? { id: sort.id, direction: sort.direction } : { id: 'title', direction: 'asc' };
  });

  private nextPage = 1;
  private loading = false;
  /** «Покоління» списку: нове сортування, пошук чи проєкт — відповіді на давніші запити не діють. */
  private generation = 0;

  constructor() {
    effect(() => {
      this.projectId();
      this.search();
      this.sort();
      this.tasks.version();
      untracked(() => this.reload());
    });
  }

  /** Дати завдання: «початок -» і кінець другим рядком; без кінця — «з початку». */
  protected dates(task: ProjectTask): { readonly start: string; readonly end: string | null } {
    return {
      start: formatDay(task.startDate),
      end: task.endDate ? formatDay(task.endDate) : null,
    };
  }

  protected taskOf(row: DataTableRow): ProjectTask {
    return (row as TaskRow).task;
  }

  protected onSort(sort: DataTableSort | null): void {
    this.sort.set(sort?.column.orderBy ? { id: sort.column.id, orderBy: sort.column.orderBy, direction: sort.direction } : null);
  }

  protected onSortBar(value: SortBarValue): void {
    const column = this.columns().find((candidate) => candidate.id === value.id);
    if (column?.orderBy) {
      this.sort.set({ id: column.id, orderBy: column.orderBy, direction: value.direction });
    }
  }

  protected openMenu(anchor: Event | HTMLElement, task: ProjectTask): void {
    this.menuTaskId.set(task.id);
    this.menu().open(anchor, task);
  }

  protected onRowMenu(event: DataTableRowMenuEvent): void {
    this.openMenu(event.anchor, this.taskOf(event.row));
  }

  /** Список з першої сторінки заново (новий проєкт, пошук, сортування чи зміна завдань). */
  protected reload(): void {
    this.generation++;
    this.loading = false;
    this.nextPage = 1;
    // Оновлення після змін не ховає показані, поки йде запит.
    this.state.update((state) => ({ status: 'loading', tasks: state.tasks, hasMore: false }));
    void this.loadMore(true);
  }

  /** Наступна сторінка (`fresh` — замість показаних). Невдача лишає, що є, і показує причину. */
  protected async loadMore(fresh = false): Promise<void> {
    if (this.loading) {
      return;
    }
    this.loading = true;
    const generation = this.generation;
    const sort = this.sort();
    const result = await this.tasks.page(this.projectId(), this.nextPage, MY_TASKS_PAGE_SIZE, {
      mine: true,
      orderBy: sort?.orderBy ?? null,
      sortOrder: sort?.direction,
      search: this.search(),
    });
    if (generation !== this.generation) {
      return;
    }
    this.loading = false;
    const before = this.state();
    if (typeof result === 'string') {
      this.state.set({ status: 'failed', tasks: fresh ? [] : before.tasks, hasMore: false, failure: result });
      return;
    }
    this.nextPage++;
    const kept = fresh ? [] : before.tasks;
    const known = new Set(kept.map((task) => task.id));
    this.state.set({ status: 'ready', tasks: [...kept, ...result.items.filter((task) => !known.has(task.id))], hasMore: result.hasNextPage });
  }

  /** Докрутили до кінця: наступна сторінка, потім індикатор знімається. */
  protected async onInfiniteScroll(event: Event): Promise<void> {
    if (this.state().hasMore) {
      await this.loadMore();
    }
    await (event.target as { complete?: () => Promise<void> | void } | null)?.complete?.();
  }
}
