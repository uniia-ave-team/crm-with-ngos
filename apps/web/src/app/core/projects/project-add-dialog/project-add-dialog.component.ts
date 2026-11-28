import { Component, inject, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { ProjectFormComponent } from '../project-form/project-form.component';
import { NewProject, ProjectsService } from '../projects.service';

/**
 * Вікно «Новий проєкт» (одне на весь застосунок, стоїть у `app.component`; відкриває «+» «Планера»
 * — у шапці бокового меню чи плаваюча на телефоні), як вікно «Додати» «Бази знань»: `app-modal`,
 * `app-dialog-header` і форма `app-project-form` (щоразу нова, тож відкривається порожньою).
 * «Створити» — `ProjectsService.create`; успіх закриває вікно (список проєктів оновлюється),
 * невдача — причина у вікні.
 */
@Component({
  selector: 'app-project-add-dialog',
  templateUrl: './project-add-dialog.component.html',
  imports: [DialogHeaderComponent, ModalComponent, ProjectFormComponent, TranslatePipe],
})
export class ProjectAddDialogComponent {
  private readonly projects = inject(ProjectsService);
  private readonly modal = viewChild.required(ModalComponent);

  /** Вікно відкрите (форма в ньому). */
  protected readonly opened = signal(false);
  protected readonly busy = signal(false);
  /** Невдача запиту (ключ мовного файлу) або `null`. */
  protected readonly error = signal<string | null>(null);

  /** Відкриває вікно з порожньою формою (лише з правом `CreateProject`). */
  open(): void {
    if (!this.projects.canCreate()) {
      return;
    }
    this.busy.set(false);
    this.error.set(null);
    this.opened.set(true);
    this.modal().open();
  }

  protected async create(project: NewProject): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    const result = await this.projects.create(project);
    this.busy.set(false);
    if (typeof result === 'string') {
      this.error.set(`planner.dialogs.add.failure.${result}`);
      return;
    }
    void this.close();
  }

  protected close(): Promise<void> {
    return this.modal().close();
  }

  protected onClosed(): void {
    this.opened.set(false);
  }
}
