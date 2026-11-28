import { Component, ElementRef, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/auth/permissions';
import { ProjectTopic, ProjectsService } from '../../core/projects/projects.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { ProjectTask } from '../../core/tasks/tasks.service';
import { BoardTasksComponent } from './board-tasks/board-tasks.component';
import { TaskDialogComponent } from './task-dialog.component';
import { TaskMenuComponent } from './task-menu.component';
import { TopicMenuComponent } from './topic-menu.component';

/** Стан дошки: завантажуємо, є колонки чи не вдалося. */
type BoardState = { readonly status: 'loading' } | { readonly status: 'ready'; readonly topics: readonly ProjectTopic[] } | { readonly status: 'failed' };

/**
 * Дошка проєкту (вкладка «Дошка» планера): колонки (`ProjectsService.topics`, за позицією) в один ряд
 * праворуч скільки завгодно, ряд прокручується по горизонталі. Колонка — 385px, на
 * телефоні — 90% ширини екрана, на всю висоту. Шапка колонки: назва, праворуч «+» (з `CreateTask`;
 * вікно нового завдання `app-task-dialog`) і три крапки (з `UpdateProject`; меню
 * `app-topic-menu`: «Редагувати», «Видалити») — кнопки видно при наведенні на колонку (чи фокусі в
 * ній; на сенсорних екранах — завжди). Під шапкою — картки завдань колонки (`app-board-tasks`:
 * сторінками при прокрутці; три крапки картки — меню `app-task-menu`: «Редагувати» — те саме вікно
 * завдання), список прокручується всередині колонки. Клік по картці чи «Детальніше» — `openTask`.
 * Поки праворуч за межею екрана є ще колонки — по правому краю вузький градієнт у колір бокових
 * меню. Колонок нема — порожній стан; не вдалося — повідомлення з «Спробувати ще раз». Після змін
 * колонок (`topicsVersion`) — заново (завдання колонки перечитує сама після `TasksService.version`).
 */
@Component({
  selector: 'app-planner-board',
  // Градієнт праворуч — лише коли там є колонки за межею екрана.
  host: { '[class.board--more-right]': 'moreRight()' },
  templateUrl: './planner-board.component.html',
  styleUrls: ['./planner-board.component.scss'],
  imports: [BoardTasksComponent, EmptyStateComponent, IonIcon, TaskDialogComponent, TaskMenuComponent, TopicMenuComponent, TranslatePipe],
})
export class PlannerBoardComponent {
  /** `id` проєкту. */
  readonly projectId = input.required<string>();
  /** Відкрити завдання в боковому вікні (клік по картці чи «Детальніше»). */
  readonly openTask = output<ProjectTask>();

  private readonly projects = inject(ProjectsService);
  private readonly currentUser = inject(CurrentUserService);

  /** «+» у шапці колонки (нове завдання) — з правом `CreateTask`. */
  protected readonly canAddTask = computed(() => this.currentUser.permissions().includes(PERMISSIONS.createTask));
  private readonly taskAccess = inject(TaskAccessService);
  /** «Редагувати» в меню картки й підвищення стану — `UpdateTask` і доступ у проєкті (не переглядач). */
  protected readonly canEditTasks = computed(() => this.taskAccess.canEdit(this.projectId()));
  /** «Видалити» в меню картки — `DeleteTask` і доступ у проєкті (не переглядач). */
  protected readonly canDeleteTasks = computed(() => this.taskAccess.canDelete(this.projectId()));
  /** Три крапки колонки («Редагувати», «Видалити») — з правом `UpdateProject`. */
  protected readonly canEditTopics = computed(() => this.currentUser.permissions().includes(PERMISSIONS.updateProject));

  protected readonly state = signal<BoardState>({ status: 'loading' });
  /** Праворуч за межею екрана є ще колонки (тоді — градієнт по правому краю). */
  protected readonly moreRight = signal(false);
  private readonly columns = viewChild<ElementRef<HTMLElement>>('columns');
  /** Запит, що тече: відповідь для іншого проєкту нічого не змінює. */
  private loadId = 0;

  constructor() {
    effect(() => {
      const id = this.projectId();
      this.projects.topicsVersion();
      untracked(() => void this.load(id));
    });
    // Ряд колонок зʼявився чи змінив розмір (вікно, нові колонки) — чи сховано щось праворуч.
    effect((onCleanup) => {
      const row = this.columns()?.nativeElement;
      if (!row) {
        this.moreRight.set(false);
        return;
      }
      this.measure(row);
      if (typeof ResizeObserver === 'function') {
        const observer = new ResizeObserver(() => this.measure(row));
        observer.observe(row);
        for (const column of Array.from(row.children)) {
          observer.observe(column);
        }
        onCleanup(() => observer.disconnect());
      }
    });
  }

  /** Прокрутили ряд: чи лишилось щось праворуч. */
  protected onScroll(event: Event): void {
    this.measure(event.target as HTMLElement);
  }

  private measure(row: HTMLElement): void {
    // Пів пікселя запасу: дробові ширини дають scrollWidth на 1 більший навіть без прокрутки.
    this.moreRight.set(row.scrollLeft + row.clientWidth < row.scrollWidth - 1);
  }

  protected retry(): void {
    void this.load(this.projectId());
  }

  private async load(id: string): Promise<void> {
    const request = ++this.loadId;
    // Оновлення після нової колонки чи завдання не ховає наявні, поки йде запит.
    if (this.state().status !== 'ready') {
      this.state.set({ status: 'loading' });
    }
    const topics = await this.projects.topics(id);
    if (request === this.loadId) {
      this.state.set(typeof topics === 'string' ? { status: 'failed' } : { status: 'ready', topics });
    }
  }
}
