import { Component, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { ProjectAccessComponent } from '../project-access/project-access.component';
import { ProjectFormComponent } from '../project-form/project-form.component';
import { TopicFormComponent } from '../topic-form/topic-form.component';
import { NewProject, ProjectAction, ProjectSummary, ProjectsService, projectLink } from '../projects.service';

/** Стан вікна «Редагувати»: завантажуємо проєкт, форма чи не вдалося завантажити (ключ помилки). */
type EditState =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly project: NewProject }
  | { readonly status: 'failed'; readonly errorKey: string };

/** Однакові дані проєкту (нічого не змінили — запит не потрібен). */
const sameProject = (a: NewProject, b: NewProject) =>
  a.title === b.title && a.description === b.description && a.visibility === b.visibility && a.startDate === b.startDate && a.endDate === b.endDate;

/**
 * Вікно дії з проєктом (одне на весь застосунок, стоїть у `app.component`): відкривається за
 * запитом `ProjectsService.requestAction` — з меню трьох крапок проєкту (`app-project-menu`) чи
 * кліком по аватарках учасників у шапці планера. По центру екрана (`app-modal`, шапка з назвою дії
 * й хрестиком):
 * - «Редагувати» — підвантажує проєкт (`ProjectsService.get`) і показує ту саму форму, що й «Новий
 *   проєкт» (`app-project-form`), заповнену, з кнопкою «Зберегти» (`update`; нічого не змінили —
 *   вікно просто закривається);
 * - «Редагувати доступи» (з `UpdateProject`) чи «Переглянути доступи» (без нього) — учасники
 *   проєкту (`app-project-access`): з правом — зміна рівня доступу й «Прибрати з проєкту»;
 * - «Видалити» — підтвердження з назвою й тим, що разом із проєктом зникнуть його задачі, теми, теги
 *   й учасники (`remove`); якщо цей проєкт відкритий — перехід на «Планер»;
 * - «Нова колонка» («Створити» на «Дошці») — назва колонки (`app-topic-form`, `createTopic`).
 * Успіх закриває вікно (список проєктів оновлюється), невдача — причина у вікні.
 */
@Component({
  selector: 'app-project-action-dialog',
  templateUrl: './project-action-dialog.component.html',
  styleUrls: ['./project-action-dialog.component.scss'],
  imports: [
    ConfirmDialogComponent,
    DialogHeaderComponent,
    ModalComponent,
    ProjectAccessComponent,
    ProjectFormComponent,
    TopicFormComponent,
    TranslatePipe,
  ],
})
export class ProjectActionDialogComponent {
  private readonly projects = inject(ProjectsService);
  private readonly router = inject(Router);
  private readonly modal = viewChild.required(ModalComponent);

  /** Відкрите вікно: дія й проєкт. */
  protected readonly dialog = signal<{ readonly action: ProjectAction; readonly target: ProjectSummary } | null>(null);
  protected readonly edit = signal<EditState>({ status: 'loading' });
  /** Запит із вікна йде (збереження, видалення). */
  protected readonly busy = signal(false);
  /** Помилка запиту з вікна (ключ мовного файлу). */
  protected readonly error = signal<string | null>(null);

  /** Чи можна змінювати доступи (`UpdateProject`): інакше вікно доступів лише показує. */
  protected readonly canEditAccess = this.projects.canEditAccess;
  /** Чи можна редагувати проєкт (рядок доступності у вікні доступів веде на «Редагувати»). */
  protected readonly canEditProject = computed(() => this.projects.can('edit'));

  /** Рядок доступності у вікні доступів: те саме вікно переходить на «Редагувати». */
  protected editFromAccess(target: ProjectSummary): void {
    this.projects.requestAction('edit', target);
  }

  /** Ключ назви дії (шапка вікна): доступи без права змінювати — «Переглянути доступи». */
  protected readonly titleKey = computed(() => {
    const action = this.dialog()?.action ?? 'edit';
    return action === 'access' && !this.projects.canEditAccess() ? 'planner.actions.viewAccess' : `planner.actions.${action}`;
  });

  constructor() {
    effect(() => {
      const request = this.projects.actionRequest();
      if (request) {
        untracked(() => this.open(request.action, request.project));
      }
    });
  }

  private open(action: ProjectAction, target: ProjectSummary): void {
    this.projects.clearActionRequest();
    if (!this.projects.can(action)) {
      return;
    }
    this.busy.set(false);
    this.error.set(null);
    this.dialog.set({ action, target });
    this.modal().open();
    if (action === 'edit') {
      void this.loadForEdit(target);
    }
  }

  private async loadForEdit(target: ProjectSummary): Promise<void> {
    this.edit.set({ status: 'loading' });
    const project = await this.projects.get(target.id);
    if (this.dialog()?.target !== target) {
      return;
    }
    this.edit.set(typeof project === 'string' ? { status: 'failed', errorKey: `planner.dialogs.failure.${project}` } : { status: 'ready', project });
  }

  protected async save(target: ProjectSummary, before: NewProject, after: NewProject): Promise<void> {
    if (sameProject(before, after)) {
      void this.close();
      return;
    }
    await this.run(() => this.projects.update(target.id, after));
  }

  /** «Нова колонка» → «Створити»: колонка в кінці дошки (`createTopic`). */
  protected async createTopic(target: ProjectSummary, title: string): Promise<void> {
    await this.run(() => this.projects.createTopic(target.id, title));
  }

  protected async remove(target: ProjectSummary): Promise<void> {
    const opened = this.router.url.startsWith(projectLink(target.id));
    await this.run(() => this.projects.remove(target.id));
    // Видалений проєкт був відкритий — його планера вже нема.
    if (opened && !this.error()) {
      void this.router.navigateByUrl('/planner');
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
