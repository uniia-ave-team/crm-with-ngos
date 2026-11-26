import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/auth/permissions';
import { TableTemplate, TableTemplateUpdate, TableTemplatesService } from '../../core/custom-tables/table-templates.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { ActionButtonComponent } from '../../shared/action-button/action-button.component';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { DialogHeaderComponent } from '../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { KbTemplateFormComponent } from './kb-template-form/kb-template-form.component';
import { cellTextColor } from '../../shared/color/cell-colors';
import { sanitizeRichText, splitBackground } from '../../shared/rich-text/rich-text';

/** Стан списку шаблонів: завантажується, є чи не вдалося (ключ причини). */
type TemplatesState =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly templates: readonly TableTemplate[] }
  | { readonly status: 'failed'; readonly errorKey: string };

/**
 * Вид «Менеджер шаблонів» (`/knowledge-base/templates`, право `ViewTableTemplate`): заголовок і
 * шаблони таблиць (`GET /api/v1/tabletemplates`). Кожен шаблон — візуалізація таблиці, яку з
 * нього буде створено: назва (системний — з позначкою), опис і рядок заголовків зі стовпчиків у
 * їхньому порядку — з форматуванням і кольором тла, як у таблиці (шаблон, створений зі
 * стовпчиків таблиці, зберігає назви заголовків як є). Створюється шаблон у виді таблиці («Створити
 * шаблон»).
 *
 * Біля назви шаблону, створеного в організації (системний сервер не дає ні змінити, ні видалити), —
 * кнопки-іконки «Редагувати» (з `UpdateTableTemplate`): вікно з назвою, описом і стовпчиками
 * (`app-kb-template-form`), «Зберегти» — `PUT /api/v1/tabletemplates/{id}`; і «Видалити» (з
 * `DeleteTableTemplate`): вікно підтвердження, «Так» — `DELETE`. Невдача — причина у вікні, воно
 * лишається відкритим; успіх — вікно закривається, список оновлюється.
 */
@Component({
  selector: 'app-kb-templates',
  templateUrl: './kb-templates.component.html',
  styleUrls: ['../kb-view.scss', './kb-templates.component.scss'],
  imports: [ActionButtonComponent, ConfirmDialogComponent, DialogHeaderComponent, KbTemplateFormComponent, ModalComponent, TranslatePipe],
})
export class KbTemplatesComponent {
  private readonly templatesService = inject(TableTemplatesService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly translate = inject(TranslateService);

  protected readonly state = signal<TemplatesState>({ status: 'loading' });
  protected readonly canUpdate = computed(() => this.currentUser.permissions().includes(PERMISSIONS.updateTableTemplate));
  protected readonly canDelete = computed(() => this.currentUser.permissions().includes(PERMISSIONS.deleteTableTemplate));

  /** Вікно «Редагувати»: який шаблон, запит іде, причина невдачі (ключ мовного файлу). */
  private readonly editDialog = viewChild<ModalComponent>('editDialog');
  protected readonly editing = signal<TableTemplate | null>(null);
  protected readonly editBusy = signal(false);
  protected readonly editError = signal<string | null>(null);

  /** Вікно «Видалити»: який шаблон, запит іде, причина невдачі (вже перекладена). */
  private readonly deleteDialog = viewChild<ModalComponent>('deleteDialog');
  protected readonly deleting = signal<TableTemplate | null>(null);
  protected readonly deleteBusy = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  /** Назва стовпчика з форматуванням (лише `b`, `i`, `u`, `s`, `br`; без тегу кольору тла). */
  protected headerHtml(name: string): string {
    return sanitizeRichText(splitBackground(name).html);
  }

  /** Тло заголовка з назви стовпчика й текст того самого відтінку з контрастом 7:1; нема — `null`. */
  protected headerColors(name: string): { readonly background: string; readonly color: string } | null {
    const background = splitBackground(name).background;
    return background ? { background, color: cellTextColor(background) } : null;
  }

  protected openEdit(template: TableTemplate): void {
    this.editing.set(template);
    this.editBusy.set(false);
    this.editError.set(null);
    this.editDialog()?.open();
  }

  protected closeEdit(): void {
    void this.editDialog()?.close();
  }

  protected async submitEdit(value: TableTemplateUpdate): Promise<void> {
    const template = this.editing();
    if (!template) {
      return;
    }
    this.editBusy.set(true);
    this.editError.set(null);
    const result = await this.templatesService.update(template.id, value);
    this.editBusy.set(false);
    if (result !== 'done') {
      this.editError.set(`knowledgeBase.failure.${result}`);
      return;
    }
    void this.editDialog()?.close();
    await this.refresh();
  }

  protected openDelete(template: TableTemplate): void {
    this.deleting.set(template);
    this.deleteBusy.set(false);
    this.deleteError.set(null);
    this.deleteDialog()?.open();
  }

  protected closeDelete(): void {
    void this.deleteDialog()?.close();
  }

  protected async confirmDelete(): Promise<void> {
    const template = this.deleting();
    if (!template) {
      return;
    }
    this.deleteBusy.set(true);
    this.deleteError.set(null);
    const result = await this.templatesService.remove(template.id);
    this.deleteBusy.set(false);
    if (result !== 'done') {
      this.deleteError.set(this.translate.instant(`knowledgeBase.failure.${result}`) as string);
      return;
    }
    void this.deleteDialog()?.close();
    const state = this.state();
    if (state.status === 'ready') {
      this.state.set({ status: 'ready', templates: state.templates.filter((candidate) => candidate.id !== template.id) });
    }
  }

  /** Список заново без «Завантажуємо…» (після зміни шаблону); не вдалося — лишається показаний. */
  private async refresh(): Promise<void> {
    const templates = await this.templatesService.listAll();
    if (typeof templates !== 'string') {
      this.state.set({ status: 'ready', templates });
    }
  }

  protected retry(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.state.set({ status: 'loading' });
    const templates = await this.templatesService.listAll();
    this.state.set(
      typeof templates === 'string' ? { status: 'failed', errorKey: `knowledgeBase.failure.${templates}` } : { status: 'ready', templates },
    );
  }
}
