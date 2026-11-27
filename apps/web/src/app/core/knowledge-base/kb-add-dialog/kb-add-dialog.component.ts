import { Component, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { PERMISSIONS } from '../../auth/permissions';
import { TableTemplateOption, TableTemplatesService } from '../../custom-tables/table-templates.service';
import { CurrentUserService } from '../../users/current-user.service';
import { KbItemFormComponent, KbItemFormValue } from '../kb-item-form/kb-item-form.component';
import { KnowledgeBaseInsertPlace } from '../knowledge-base';
import { KnowledgeBaseService } from '../knowledge-base.service';

/**
 * Вікно «Додати» «Бази знань» (одне на весь застосунок, стоїть у `app.component`): відкривається,
 * коли натиснули будь-який «+» (`KnowledgeBaseService.insertRequest`). Форма — `app-kb-item-form`
 * з типом і шаблоном; шаблони підвантажуються при відкритті, якщо є право `ViewTableTemplate`
 * (без нього — лише «Без шаблону»). «Створити» — `KnowledgeBaseService.create`.
 */
@Component({
  selector: 'app-kb-add-dialog',
  templateUrl: './kb-add-dialog.component.html',
  imports: [DialogHeaderComponent, KbItemFormComponent, ModalComponent, TranslatePipe],
})
export class KbAddDialogComponent {
  private readonly knowledgeBase = inject(KnowledgeBaseService);
  private readonly templatesService = inject(TableTemplatesService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly modal = viewChild.required(ModalComponent);

  /** Куди додати (з «+»); `null` — вікно закрите. */
  protected readonly place = signal<KnowledgeBaseInsertPlace | null>(null);
  protected readonly templates = signal<readonly TableTemplateOption[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const request = this.knowledgeBase.insertRequest();
      if (request) {
        untracked(() => this.open(request));
      }
    });
  }

  private open(place: KnowledgeBaseInsertPlace): void {
    this.knowledgeBase.clearInsertRequest();
    if (!this.knowledgeBase.canCreate()) {
      return;
    }
    this.place.set(place);
    this.error.set(null);
    this.busy.set(false);
    this.templates.set([]);
    void this.loadTemplates();
    this.modal().open();
  }

  private async loadTemplates(): Promise<void> {
    if (!this.currentUser.permissions().includes(PERMISSIONS.viewTableTemplate)) {
      return;
    }
    const templates = await this.templatesService.listAll();
    if (typeof templates !== 'string') {
      this.templates.set(templates);
    }
  }

  protected async create(value: KbItemFormValue): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    const result = await this.knowledgeBase.create({ ...value, folderId: this.place()?.folder?.id ?? null });
    this.busy.set(false);
    if (result === 'done') {
      void this.close();
    } else {
      this.error.set(`knowledgeBase.failure.${result}`);
    }
  }

  protected close(): Promise<void> {
    return this.modal().close();
  }

  protected onClosed(): void {
    this.place.set(null);
  }
}
