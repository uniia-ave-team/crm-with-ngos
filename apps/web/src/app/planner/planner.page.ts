import { Component, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { IonButtons, IonContent, IonFooter, IonHeader, IonMenuButton, IonToolbar } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { ProjectMember, ProjectsService, projectLink } from '../core/projects/projects.service';
import { ActionButtonComponent } from '../shared/action-button/action-button.component';
import { EmptyStateComponent } from '../shared/empty-state/empty-state.component';
import { MemberAvatarsComponent } from './member-avatars/member-avatars.component';
import { MyTasksComponent } from './my-tasks/my-tasks.component';
import { TaskAccessService } from '../core/tasks/task-access.service';
import { CurrentUserService } from '../core/users/current-user.service';
import { TaskDetailsComponent } from './task-details/task-details.component';
import { PlannerBoardComponent } from './planner-board/planner-board.component';
import { SearchHeaderComponent } from '../shared/search-header/search-header.component';
import { ProjectTask } from '../core/tasks/tasks.service';

/** Вкладка планера проєкту: «Мої завдання», «Дошка», «Про проєкт». */
export type PlannerView = 'my' | 'board' | 'about';
export const PLANNER_VIEWS: readonly PlannerView[] = ['my', 'board', 'about'];
/** Заплановані вкладки: у розмітці, але приховані класом `planned` (і не обираються з адреси). */
export const PLANNED_VIEWS: readonly PlannerView[] = ['about'];
/** Вкладка, коли в адресі її нема. */
const DEFAULT_VIEW: PlannerView = 'board';

/**
 * «Планер» (`/planner`) одразу відкриває перший проєкт зі списку (`ProjectsService`, без запису в
 * історії); доступних проєктів нема — лише порожній стан (`app-empty-state`, без заголовка). Планер
 * одного проєкту (`/planner/projects/:id`, клік по проєкту в боковому
 * меню): `id` обраного проєкту приходить входом `id` (`withComponentInputBinding`).
 *
 * Шапка планера проєкту — спільна `app-search-header`: замість заголовка три вкладки («Мої
 * завдання», «Дошка», «Про проєкт»; активна підкреслена зеленим тієї теми), що обирають вид і зміст
 * сторінки (у адресі — `?view=my|board|about`, без нього — «Дошка»). Праворуч — учасники проєкту
 * (`ProjectsService.members`, лише активні акаунти; `app-member-avatars`: дві аватарки внахлест, а
 * якщо учасників більше — третя з «...» й пунктирною обвідкою); клік по них — вікно доступів проєкту
 * («Редагувати доступи» чи «Переглянути доступи»); далі «Створити» (лише на «Дошці» й з
 * `UpdateProject`: вікно «Нова колонка»; як і всі кнопки в шапці з пошуком, до 600px — лише іконка) і пошук
 * (лише на «Моїх завданнях» і «Дошці»).
 * На телефоні (до 768px) у шапці замість вкладок — назва проєкту, а вкладки — окремою тонкою
 * панеллю внизу (`ion-footer`) з підкресленням згори. «Про проєкт» запланована: у розмітці, але
 * прихована класом `planned`. «Дошка» — колонки проєкту (`app-planner-board`); клік по картці
 * завдання чи «Детальніше» відкриває бокове вікно завдання (`app-task-details`). «Мої
 * завдання» — таблиця своїх завдань (`app-my-tasks`; пошук шапки шукає в них), на телефоні — картки
 * й кнопка з фільтром у шапці, що відкриває рядок сортування.
 */
@Component({
  selector: 'app-planner',
  templateUrl: './planner.page.html',
  styleUrls: ['./planner.page.scss'],
  imports: [
    ActionButtonComponent,
    EmptyStateComponent,
    IonButtons,
    IonContent,
    IonFooter,
    IonHeader,
    IonMenuButton,
    IonToolbar,
    NgTemplateOutlet,
    MemberAvatarsComponent,
    MyTasksComponent,
    PlannerBoardComponent,
    SearchHeaderComponent,
    TaskDetailsComponent,
    TranslatePipe,
  ],
})
export class PlannerPage {
  /** `id` обраного проєкту з адреси; без нього — загальний «Планер». */
  readonly id = input<string>();
  /** Вкладка з адреси (`?view=`). */
  readonly view = input<string>();

  private readonly projects = inject(ProjectsService);
  private readonly router = inject(Router);

  protected readonly views = PLANNER_VIEWS;
  protected readonly isPlanned = (view: PlannerView) => PLANNED_VIEWS.includes(view);
  /** Активна вкладка (невідома чи запланована в адресі — «Дошка»). */
  protected readonly activeView = computed<PlannerView>(
    () => PLANNER_VIEWS.find((view) => view === this.view() && !PLANNED_VIEWS.includes(view)) ?? DEFAULT_VIEW,
  );

  /** Обраний проєкт зі списку; `null` — проєкт не обрано чи його ще (або вже) нема в списку. */
  protected readonly project = computed(() => {
    const id = this.id();
    return id ? (this.projects.state().projects.find((candidate) => candidate.id === id) ?? null) : null;
  });

  /** `/planner` без проєкту, а доступних проєктів нема: порожній стан. */
  protected readonly noProjects = computed(() => {
    const state = this.projects.state();
    return !this.id() && state.status === 'ready' && state.projects.length === 0;
  });

  /** Учасники обраного проєкту з активними акаунтами (не вдалося — нікого). */
  protected readonly members = signal<readonly ProjectMember[]>([]);
  private readonly taskAccess = inject(TaskAccessService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly taskDetails = viewChild<TaskDetailsComponent>('taskDetails');

  /** Клік по картці чи рядку, «Детальніше»: бокове вікно завдання. */
  protected openTask(task: ProjectTask): void {
    this.taskDetails()?.open(task);
  }

  /** «Створити» (нова колонка дошки) — лише на «Дошці» й з правом `UpdateProject`. */
  protected readonly canCreate = computed(() => this.activeView() === 'board' && this.projects.can('topic'));

  /** «Створити»: вікно «Нова колонка» для поточного проєкту (`app-project-action-dialog`). */
  protected createTopic(): void {
    const project = this.project();
    if (project) {
      this.projects.requestAction('topic', project);
    }
  }
  /** Пошук — лише на «Моїх завданнях» і «Дошці». */
  protected readonly searchable = computed(() => this.activeView() !== 'about');
  /** Текст пошуку: шукає в «Моїх завданнях» (на дошці поки ні на що не впливає). */
  protected readonly search = signal('');
  /** Рядок сортування «Моїх завдань» на телефоні відкритий (кнопка з фільтром у шапці). */
  protected readonly sortOpen = signal(false);

  /** Запит учасників, що тече: відповідь для іншого проєкту нічого не змінює. */
  private membersId = 0;

  constructor() {
    void this.projects.load();
    // Учасники — для обраного проєкту й заново після змін доступів (`membersVersion`).
    effect(() => {
      const id = this.id();
      this.projects.membersVersion();
      untracked(() => void this.loadMembers(id));
    });
    // `/planner` без проєкту одразу відкриває перший зі списку (без запису в історії, тож «Назад»
    // не повертає на порожній «Планер»).
    effect(() => {
      const state = this.projects.state();
      const [first] = state.projects;
      if (!this.id() && state.status === 'ready' && first) {
        untracked(() => void this.router.navigateByUrl(projectLink(first.id), { replaceUrl: true }));
      }
    });
  }

  /** Назва дії доступів: «Редагувати доступи» з `UpdateProject`, інакше «Переглянути доступи». */
  protected readonly accessKey = computed(() => (this.projects.canEditAccess() ? 'planner.actions.access' : 'planner.actions.viewAccess'));

  /** Клік по аватарках: вікно доступів поточного проєкту (`app-project-action-dialog`). */
  protected openAccess(): void {
    const project = this.project();
    if (project) {
      this.projects.requestAction('access', project);
    }
  }

  /** Обрали вкладку: вона — в адресі (без запису в історії), тож переживає перезавантаження. */
  protected choose(view: PlannerView): void {
    void this.router.navigate([], { queryParams: { view }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  private async loadMembers(id: string | undefined): Promise<void> {
    const request = ++this.membersId;
    this.members.set([]);
    if (!id) {
      return;
    }
    const members = await this.projects.members(id);
    if (request === this.membersId) {
      this.members.set(typeof members === 'string' ? [] : members.filter((member) => member.isActive));
      // Рівень доступу самого користувача — для дій із завданнями (переглядач їх не змінює).
      if (typeof members !== 'string') {
        const self = this.currentUser.user()?.id;
        this.taskAccess.setClaim(id, members.find((member) => member.userId === self)?.claim ?? null);
      }
    }
  }
}
