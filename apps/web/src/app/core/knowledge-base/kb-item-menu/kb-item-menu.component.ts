import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { ActionListComponent, ActionListItem } from '../../../shared/action-list/action-list.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { PopoverComponent } from '../../../shared/popover/popover.component';
import { KbItemFormComponent, KbItemFormValue } from '../kb-item-form/kb-item-form.component';
import { KbMoveDialogComponent } from '../kb-move-dialog/kb-move-dialog.component';
import { KnowledgeBaseAction, KnowledgeBaseEntry, isKnowledgeBaseFolder } from '../knowledge-base';
import { KnowledgeBaseEditing, KnowledgeBaseService } from '../knowledge-base.service';

/** Ширина екрана, нижче якої «Додати в папку» — у меню (на ширшому це «+» у плашці папки). */
export const KB_MENU_COMPACT_QUERY = '(max-width: 991px)';

const ITEMS: Record<KnowledgeBaseAction, ActionListItem<KnowledgeBaseAction>> = {
  add: { id: 'add', labelKey: 'knowledgeBase.actions.add', icon: 'add-outline' },
  edit: { id: 'edit', labelKey: 'knowledgeBase.actions.edit', icon: 'create-outline' },
  // Переміщення заплановане: сервер ще не вміє (пункт у розмітці, але прихований).
  move: { id: 'move', labelKey: 'knowledgeBase.actions.move', icon: 'move-outline', planned: true },
  delete: { id: 'delete', labelKey: 'knowledgeBase.actions.delete', icon: 'trash-outline' },
};

/** Дія з вікном: редагувати, перемістити чи видалити. */
type DialogAction = Exclude<KnowledgeBaseAction, 'add'>;

/** Стан вікна «Редагувати»: завантажуємо матеріал, форма чи не вдалося завантажити (ключ помилки). */
type EditState = { readonly status: 'loading' } | { readonly status: 'ready'; readonly editing: KnowledgeBaseEditing } | { readonly status: 'failed'; readonly errorKey: string };

/**
 * Меню біля трьох крапок матеріалу чи папки «Бази знань» (у дереві бокового меню й у «Посібнику»):
 * та сама плашка (`app-popover`) і список дій (`app-action-list`), що й у колег. Відкривається
 * `open(anchor, target)` під кнопкою.
 *
 * Пункти — лише ті, на які є права (`KnowledgeBaseService.can`): для папки — «Додати в папку»
 * (лише до 992px: на ширшому це «+» у плашці папки), «Редагувати», «Видалити»; для таблиці й
 * сторінки — «Редагувати», «Перемістити» (запланована, прихована), «Видалити». «Додати в папку» —
 * `KnowledgeBaseService.requestInsert` з папкою; решта відкриває вікно по центру екрана
 * (`app-modal`, шапка з назвою дії й хрестиком):
 * - «Редагувати» — підвантажує матеріал (`openForEdit`), форма з назвою й описом, «Зберегти»;
 * - «Видалити» — підтвердження з назвою (для папки — що весь її зміст буде втрачено);
 * - «Перемістити» — «З папки» й «До папки» (усі папки й «Без папки»).
 */
@Component({
  selector: 'app-kb-item-menu',
  templateUrl: './kb-item-menu.component.html',
  styleUrls: ['./kb-item-menu.component.scss'],
  imports: [ActionListComponent, ConfirmDialogComponent, DialogHeaderComponent, KbItemFormComponent, KbMoveDialogComponent, ModalComponent, PopoverComponent, TranslatePipe],
})
export class KbItemMenuComponent {
  private readonly knowledgeBase = inject(KnowledgeBaseService);
  private readonly popover = viewChild.required(PopoverComponent);
  private readonly dialogModal = viewChild.required<ModalComponent>('actionDialog');

  /** Матеріал чи папка, для якої відкрите меню. */
  private readonly target = signal<KnowledgeBaseEntry | null>(null);
  /** Меню відкрили на вузькому екрані (див. `KB_MENU_COMPACT_QUERY`). */
  private readonly compact = signal(false);
  /** Відкрите вікно: дія й матеріал чи папка. */
  protected readonly dialog = signal<{ action: DialogAction; target: KnowledgeBaseEntry } | null>(null);
  protected readonly edit = signal<EditState>({ status: 'loading' });
  /** Запит із вікна йде (збереження, видалення). */
  protected readonly busy = signal(false);
  /** Помилка запиту з вікна (ключ мовного файлу). */
  protected readonly error = signal<string | null>(null);

  protected readonly isFolder = isKnowledgeBaseFolder;
  protected readonly folders = computed(() => this.knowledgeBase.folders().map(({ id, title }) => ({ id, title })));

  protected readonly items = computed<readonly ActionListItem<KnowledgeBaseAction>[]>(() => {
    const target = this.target();
    if (!target) {
      return [];
    }
    const actions: readonly KnowledgeBaseAction[] = isKnowledgeBaseFolder(target)
      ? [...(this.compact() ? (['add'] as const) : []), 'edit', 'delete']
      : ['edit', 'move', 'delete'];
    return actions.filter((action) => ITEMS[action].planned || this.knowledgeBase.can(action, target)).map((action) => ITEMS[action]);
  });

  /** Відкриває меню під кнопкою `anchor` для матеріалу чи папки `target`. */
  open(anchor: Event | HTMLElement, target: KnowledgeBaseEntry): void {
    this.target.set(target);
    this.compact.set(typeof matchMedia === 'function' && matchMedia(KB_MENU_COMPACT_QUERY).matches);
    this.popover().open(anchor);
  }

  /** Папка, де матеріал зараз (для «З папки»); `null` — без папки. */
  protected folderOf(target: KnowledgeBaseEntry): string | null {
    return this.knowledgeBase.folderOf(target.id)?.id ?? null;
  }

  protected async onChoose(action: KnowledgeBaseAction): Promise<void> {
    const target = this.target();
    // Вікно відкриваємо, коли меню вже закрилося: інакше фокус повернувся б на три крапки під вікном.
    await this.popover().close();
    if (!target) {
      return;
    }
    if (action === 'add') {
      this.knowledgeBase.requestInsert({ folder: { id: target.id, title: target.title } });
      return;
    }
    this.busy.set(false);
    this.error.set(null);
    this.dialog.set({ action, target });
    this.dialogModal().open();
    if (action === 'edit') {
      void this.loadForEdit(target);
    }
  }

  private async loadForEdit(target: KnowledgeBaseEntry): Promise<void> {
    this.edit.set({ status: 'loading' });
    const editing = await this.knowledgeBase.openForEdit(target);
    if (this.dialog()?.target !== target) {
      return;
    }
    this.edit.set(typeof editing === 'string' ? { status: 'failed', errorKey: `knowledgeBase.failure.${editing}` } : { status: 'ready', editing });
  }

  protected async save(editing: KnowledgeBaseEditing, value: KbItemFormValue): Promise<void> {
    if (value.title === editing.title && value.description === editing.description) {
      void this.closeDialog();
      return;
    }
    await this.run(() => this.knowledgeBase.saveEdit(editing, { title: value.title, description: value.description }));
  }

  protected async remove(target: KnowledgeBaseEntry): Promise<void> {
    await this.run(() => this.knowledgeBase.delete(target));
  }

  protected move(target: KnowledgeBaseEntry, folderId: string | null): void {
    this.knowledgeBase.move(target.id, folderId);
    void this.closeDialog();
  }

  /** Виконує запит вікна: успіх — закриває вікно, невдача — показує помилку в ньому. */
  private async run(request: () => Promise<string>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    const result = await request();
    this.busy.set(false);
    if (result === 'done') {
      void this.closeDialog();
    } else {
      this.error.set(`knowledgeBase.failure.${result}`);
    }
  }

  protected closeDialog(): Promise<void> {
    return this.dialogModal().close();
  }

  protected onDialogClosed(): void {
    this.dialog.set(null);
  }
}
