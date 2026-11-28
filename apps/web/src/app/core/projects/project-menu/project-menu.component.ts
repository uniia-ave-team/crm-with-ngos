import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { ActionListComponent, ActionListItem } from '../../../shared/action-list/action-list.component';
import { PopoverComponent } from '../../../shared/popover/popover.component';
import { ProjectAction, ProjectSummary, ProjectsService } from '../projects.service';

/**
 * Меню біля трьох крапок проєкту в боковому меню «Планера» — як меню матеріалу «Бази знань»
 * (`app-kb-item-menu`): та сама плашка (`app-popover`) і список дій (`app-action-list`).
 * Відкривається `open(anchor, project)` під кнопкою. Пункти: «Редагувати» (з `UpdateProject`),
 * доступи — є завжди: «Редагувати доступи» з `UpdateProject`, інакше «Переглянути доступи», і
 * «Видалити» (з `DeleteProject`). Вибір відкриває вікно дії (`app-project-action-dialog`, через
 * `ProjectsService.requestAction`).
 */
@Component({
  selector: 'app-project-menu',
  templateUrl: './project-menu.component.html',
  imports: [ActionListComponent, PopoverComponent, TranslatePipe],
})
export class ProjectMenuComponent {
  private readonly projects = inject(ProjectsService);
  private readonly popover = viewChild.required(PopoverComponent);

  /** Проєкт, для якого відкрите меню. */
  private readonly target = signal<ProjectSummary | null>(null);

  protected readonly items = computed<readonly ActionListItem<ProjectAction>[]>(() => {
    if (!this.target()) {
      return [];
    }
    const editAccess = this.projects.canEditAccess();
    const items: readonly ActionListItem<ProjectAction>[] = [
      { id: 'edit', labelKey: 'planner.actions.edit', icon: 'create-outline' },
      {
        id: 'access',
        labelKey: editAccess ? 'planner.actions.access' : 'planner.actions.viewAccess',
        icon: editAccess ? 'people-outline' : 'eye-outline',
      },
      { id: 'delete', labelKey: 'planner.actions.delete', icon: 'trash-outline' },
    ];
    return items.filter((item) => this.projects.can(item.id));
  });

  /** Відкриває меню під кнопкою `anchor` для проєкту `project`. */
  open(anchor: Event | HTMLElement, project: ProjectSummary): void {
    this.target.set(project);
    this.popover().open(anchor);
  }

  protected async onChoose(action: ProjectAction): Promise<void> {
    const target = this.target();
    // Вікно відкриваємо, коли меню вже закрилося: інакше фокус повернувся б на три крапки під вікном.
    await this.popover().close();
    if (target) {
      this.projects.requestAction(action, target);
    }
  }
}
