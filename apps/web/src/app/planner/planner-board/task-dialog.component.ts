import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/auth/permissions';
import { ColleagueSource } from '../../core/projects/colleague-search/colleague-search.component';
import { ProjectTopic, ProjectsService } from '../../core/projects/projects.service';
import { TaskFormComponent, TaskFormInitial } from '../../core/tasks/task-form/task-form.component';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { NewTask, ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { DialogHeaderComponent } from '../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../shared/modal/modal.component';

/** Відкрите вікно: нове завдання в колонці чи зміна наявного. */
type TaskDialog =
  | { readonly mode: 'create'; readonly projectId: string; readonly topic: ProjectTopic }
  | { readonly mode: 'edit'; readonly projectId: string; readonly task: ProjectTask };

/** Однакові дані завдання (нічого не змінили — запит не потрібен). */
const sameTask = (a: NewTask, b: NewTask) => JSON.stringify(a) === JSON.stringify(b);

/**
 * Вікно завдання по центру екрана (`app-modal`, шапка з хрестиком) з формою завдання
 * (`app-task-form`):
 * - «+» у заголовку колонки (`create`) — «Нове завдання в «колонка»», порожня форма, «Створити» —
 *   `TasksService.create` у цій колонці;
 * - «Редагувати» з меню картки (`edit`) — «Редагувати завдання», форма з даними завдання, «Зберегти»
 *   — `TasksService.update` (нічого не змінили — вікно просто закривається). Лише з
 *   `TaskAccessService.canEdit` (право `UpdateTask` і не переглядач): без нього вікно не відкривається,
 *   а «Зберегти» нічого не надсилає.
 * Виконавців шукаємо серед усіх колег (з правом бачити колег), але в приватному проєкті (чи без
 * того права) — лише серед учасників проєкту: інших сервер не призначить. Підказки тегів — теги
 * проєкту; нові теги — лише з правом `UpdateProject` (сервер інакше їх не створить). Перед запитом
 * назви тегів стають `id` (`ProjectsService.tagIds`: наявні — за назвою без регістру, нові —
 * створюються); у «Редагувати» сервер лишає в завданні рівно ці теги, тож прибрані з поля зникають і
 * з завдання. Успіх закриває вікно, невдача — причина у вікні.
 */
@Component({
  selector: 'app-task-dialog',
  templateUrl: './task-dialog.component.html',
  imports: [DialogHeaderComponent, ModalComponent, TaskFormComponent, TranslatePipe],
})
export class TaskDialogComponent {
  private readonly projects = inject(ProjectsService);
  private readonly tasks = inject(TasksService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly taskAccess = inject(TaskAccessService);
  private readonly modal = viewChild.required(ModalComponent);

  protected readonly dialog = signal<TaskDialog | null>(null);
  /** Звідки колеги для «Виконавців». */
  protected readonly colleagues = signal<ColleagueSource>(null);
  /** Теги проєкту (назви) — підказки поля тегів. */
  protected readonly tagSuggestions = signal<readonly string[]>([]);
  /** Нові теги — з правом `UpdateProject`. */
  protected readonly allowNewTags = computed(() => this.currentUser.permissions().includes(PERMISSIONS.updateProject));
  /** Запит іде. */
  protected readonly busy = signal(false);
  /** Помилка запиту (ключ мовного файлу). */
  protected readonly error = signal<string | null>(null);

  /** Назва вікна (ключ і колонка для перекладу). */
  protected readonly title = computed(() => {
    const dialog = this.dialog();
    return dialog?.mode === 'edit' ? { key: 'planner.tasks.edit.title', topic: '' } : { key: 'planner.tasks.add.title', topic: dialog?.topic.title ?? '' };
  });

  /** Дані завдання для форми у вікні «Редагувати». */
  protected readonly initial = computed<TaskFormInitial | null>(() => {
    const dialog = this.dialog();
    return dialog?.mode === 'edit' ? toInitial(dialog.task) : null;
  });

  /** Відкриває вікно нового завдання в колонці `topic` проєкту `projectId`. */
  create(projectId: string, topic: ProjectTopic): void {
    this.show({ mode: 'create', projectId, topic });
  }

  /** Відкриває вікно зміни завдання `task` проєкту `projectId` (без права чи переглядачу — ні). */
  edit(projectId: string, task: ProjectTask): void {
    if (this.taskAccess.canEdit(projectId)) {
      this.show({ mode: 'edit', projectId, task });
    }
  }

  private show(target: TaskDialog): void {
    this.busy.set(false);
    this.error.set(null);
    this.dialog.set(target);
    this.modal().open();
    const visibility = this.projects.state().projects.find((project) => project.id === target.projectId)?.visibility ?? null;
    const canSearch = this.currentUser.permissions().includes(PERMISSIONS.viewUser);
    // Доступність невідома — як приватний: учасників сервер призначить напевно.
    if (canSearch && visibility !== null && visibility !== 0) {
      this.colleagues.set(null);
    } else {
      void this.loadMembers(target);
    }
    void this.loadTags(target);
  }

  private async loadTags(target: TaskDialog): Promise<void> {
    this.tagSuggestions.set([]);
    const tags = await this.projects.tags(target.projectId);
    if (this.dialog() === target && typeof tags !== 'string') {
      this.tagSuggestions.set(tags.map((tag) => tag.title));
    }
  }

  private async loadMembers(target: TaskDialog): Promise<void> {
    this.colleagues.set('loading');
    const members = await this.projects.members(target.projectId);
    if (this.dialog() !== target) {
      return;
    }
    this.colleagues.set(typeof members === 'string' ? 'failed' : members.map((member) => ({ ...member, id: member.userId })));
  }

  protected async submit(task: NewTask): Promise<void> {
    const target = this.dialog();
    if (!target || this.busy()) {
      return;
    }
    // Право могли забрати, поки вікно відкрите: без нього зміни не надсилаються.
    if (target.mode === 'edit' && !this.taskAccess.canEdit(target.projectId)) {
      this.error.set('planner.tasks.failure.forbidden');
      return;
    }
    if (target.mode === 'edit' && sameTask(task, toNewTask(target.task))) {
      void this.close();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const tagIds = await this.projects.tagIds(target.projectId, task.tags);
    const result =
      typeof tagIds === 'string'
        ? tagIds
        : target.mode === 'create'
          ? await this.tasks.create(target.projectId, target.topic.id, task, tagIds)
          : await this.tasks.update(target.projectId, target.task, task, tagIds);
    this.busy.set(false);
    if (typeof result === 'string' && result !== 'done') {
      this.error.set(`planner.tasks.failure.${result}`);
    } else {
      void this.close();
    }
  }

  protected close(): Promise<void> {
    return this.modal().close();
  }

  protected onClosed(): void {
    this.dialog.set(null);
  }
}

/** Виконавці завдання — як колеги у формі (акаунти тут невідомі, тож — увімкнені). */
function toInitial(task: ProjectTask): TaskFormInitial {
  return {
    title: task.title,
    description: task.description,
    status: task.status,
    priority: task.priority,
    startDate: task.startDate,
    endDate: task.endDate,
    assignees: task.assignees.map((assignee) => ({ ...assignee, isActive: true })),
    tags: task.tags.map((tag) => tag.title),
  };
}

/** Завдання як дані форми (щоб порівняти зі зміненими). */
function toNewTask(task: ProjectTask): NewTask {
  return {
    title: task.title,
    description: task.description,
    status: task.status,
    priority: task.priority,
    assigneeIds: task.assignees.map((assignee) => assignee.id),
    tags: task.tags.map((tag) => tag.title),
    startDate: task.startDate,
    endDate: task.endDate,
  };
}
