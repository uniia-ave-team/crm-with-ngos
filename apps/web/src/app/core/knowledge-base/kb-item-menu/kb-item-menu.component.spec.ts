import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ActionListComponent } from '../../../shared/action-list/action-list.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { PopoverComponent } from '../../../shared/popover/popover.component';
import { KbItemFormComponent } from '../kb-item-form/kb-item-form.component';
import { KbMoveDialogComponent } from '../kb-move-dialog/kb-move-dialog.component';
import { KnowledgeBaseAction, KnowledgeBaseEntry } from '../knowledge-base';
import { KnowledgeBaseEditing, KnowledgeBaseService } from '../knowledge-base.service';
import { KbItemMenuComponent } from './kb-item-menu.component';

describe('KbItemMenuComponent', () => {
  const folder: KnowledgeBaseEntry = { id: 'f1', type: 'folder', title: 'Розділ', items: [] };
  const page: KnowledgeBaseEntry = { id: 'p1', type: 'page', title: 'Документ' };
  const table: KnowledgeBaseEntry = { id: 't1', type: 'customtable', title: 'Таблиця' };

  let service: KnowledgeBaseService;
  /** Дії, на які є права (`KnowledgeBaseService.can`); за замовчуванням — усі. */
  let allowed: readonly KnowledgeBaseAction[];

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    service = TestBed.inject(KnowledgeBaseService);
    allowed = ['add', 'edit', 'move', 'delete'];
    vi.spyOn(service, 'can').mockImplementation((action) => allowed.includes(action));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    // Тестові файли не ізольовані: підмінений `matchMedia` (без `addListener`) інакше лишився б для
    // наступних файлів, і `ion-split-pane` падав би в них.
    vi.unstubAllGlobals();
  });

  /** Відкриває меню для `target`; `compact` — вузький екран (телефон). */
  function openFor(target: KnowledgeBaseEntry, compact = false) {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: compact && query.includes('max-width'), media: query }));
    const fixture = TestBed.createComponent(KbItemMenuComponent);
    fixture.detectChanges();
    const popover = fixture.debugElement.query(By.directive(PopoverComponent)).componentInstance as PopoverComponent;
    const openPopover = vi.spyOn(popover, 'open').mockImplementation(() => undefined);
    vi.spyOn(popover, 'close').mockResolvedValue();
    const anchor = document.createElement('button');
    fixture.componentInstance.open(anchor, target);
    fixture.detectChanges();
    const list = fixture.debugElement.query(By.directive(ActionListComponent)).componentInstance as ActionListComponent;
    return { fixture, openPopover, anchor, list, ids: () => list.items().map((item) => item.id) };
  }

  it('opens the popover under the button', () => {
    const { openPopover, anchor } = openFor(page);
    expect(openPopover).toHaveBeenCalledWith(anchor);
  });

  it.each([
    ['a page', page, ['edit', 'move', 'delete']],
    ['a table', table, ['edit', 'move', 'delete']],
    ['a folder on a wide screen («+» is in its plate there)', folder, ['edit', 'delete']],
  ])('offers %s: %j', (_name, target, expected) => {
    expect(openFor(target).ids()).toEqual(expected);
  });

  it('offers «Add to the folder» first for a folder on a phone', () => {
    expect(openFor(folder, true).ids()).toEqual(['add', 'edit', 'delete']);
    expect(openFor(page, true).ids()).toEqual(['edit', 'move', 'delete']);
  });

  it('offers only the actions the user has the rights for, and keeps «Move» planned (hidden)', () => {
    allowed = ['edit'];
    const { list } = openFor(table);
    expect(list.items().map((item) => [item.id, !!item.planned])).toEqual([
      ['edit', false],
      ['move', true],
    ]);
    expect(service.can).toHaveBeenCalledWith('edit', table);
  });

  it('takes the texts and the icons from the language file and the icon list', () => {
    const { list } = openFor(folder, true);
    expect(list.items().map((item) => item.labelKey)).toEqual(['knowledgeBase.actions.add', 'knowledgeBase.actions.edit', 'knowledgeBase.actions.delete']);
    expect(list.items().map((item) => item.icon)).toEqual(['add-outline', 'create-outline', 'trash-outline']);
    expect(Object.keys(uk.knowledgeBase.actions)).toEqual(['label', 'add', 'edit', 'move', 'delete']);
  });

  it('asks to add to the folder by «Add to the folder»', async () => {
    const { list } = openFor(folder, true);
    list.choose.emit('add');
    await vi.waitFor(() => expect(service.insertRequest()).toEqual({ folder: { id: 'f1', title: 'Розділ' } }));
  });

  /** Вибирає дію в меню й чекає, поки відкриється вікно (`close` вікна підмінене). */
  async function chooseAction(target: KnowledgeBaseEntry, action: KnowledgeBaseAction) {
    const opened = openFor(target);
    const modal = opened.fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent;
    const closeModal = vi.spyOn(modal, 'close').mockResolvedValue();
    opened.list.choose.emit(action);
    // Зміст вікна монтується, коли `ion-modal` відкривається.
    await vi.waitFor(() => {
      opened.fixture.detectChanges();
      expect(opened.fixture.debugElement.query(By.directive(DialogHeaderComponent))).not.toBeNull();
    });
    const child = <T>(type: new (...args: never[]) => T) => opened.fixture.debugElement.query(By.directive(type))?.componentInstance as T | undefined;
    const text = () => (document.querySelector('.kb-dialog__message')?.textContent ?? '').trim();
    return { ...opened, closeModal, child, text };
  }

  it.each([
    ['edit', table, uk.knowledgeBase.actions.edit],
    ['move', page, uk.knowledgeBase.actions.move],
    ['delete', folder, uk.knowledgeBase.actions.delete],
  ] as const)('opens the window «%s» with the name of the action and a cross that closes it', async (action, target, title) => {
    vi.spyOn(service, 'openForEdit').mockReturnValue(new Promise(() => undefined));
    const { child, closeModal } = await chooseAction(target, action);
    const header = child(DialogHeaderComponent);
    expect(header?.title()).toBe(title);
    header?.dismiss.emit();
    expect(closeModal).toHaveBeenCalled();
  });

  describe('«Edit»', () => {
    const editing: KnowledgeBaseEditing = { entry: table, table: null, title: 'Таблиця', description: 'Опис' };

    it('loads the material, fills the form with its name and description, and saves the changes', async () => {
      vi.spyOn(service, 'openForEdit').mockResolvedValue(editing);
      const saveEdit = vi.spyOn(service, 'saveEdit').mockResolvedValue('done');
      const { fixture, child, closeModal } = await chooseAction(table, 'edit');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(child(KbItemFormComponent)).toBeDefined();
      });
      const form = child(KbItemFormComponent)!;
      expect(form.initial()).toEqual(editing);
      // У вікні «Редагувати» нема типу й шаблону.
      expect(form.templates()).toBeNull();

      form.submitted.emit({ title: 'Нова назва', description: null, type: 'customtable', templateId: null });
      await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
      expect(saveEdit).toHaveBeenCalledWith(editing, { title: 'Нова назва', description: null });
    });

    it('closes without a request when nothing changed', async () => {
      vi.spyOn(service, 'openForEdit').mockResolvedValue(editing);
      const saveEdit = vi.spyOn(service, 'saveEdit');
      const { fixture, child, closeModal } = await chooseAction(table, 'edit');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(child(KbItemFormComponent)).toBeDefined();
      });
      child(KbItemFormComponent)!.submitted.emit({ title: 'Таблиця', description: 'Опис', type: 'customtable', templateId: null });
      await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
      expect(saveEdit).not.toHaveBeenCalled();
    });

    it('shows the failure of saving in the form, and of loading instead of it', async () => {
      vi.spyOn(service, 'openForEdit').mockResolvedValue(editing);
      vi.spyOn(service, 'saveEdit').mockResolvedValue('forbidden');
      const { fixture, child, closeModal } = await chooseAction(table, 'edit');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(child(KbItemFormComponent)).toBeDefined();
      });
      child(KbItemFormComponent)!.submitted.emit({ title: 'Нова назва', description: null, type: 'customtable', templateId: null });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(child(KbItemFormComponent)!.error()).toBe('knowledgeBase.failure.forbidden');
      });
      expect(closeModal).not.toHaveBeenCalled();
    });

    it('says it is loading the material until the answer', async () => {
      vi.spyOn(service, 'openForEdit').mockReturnValue(new Promise(() => undefined));
      const { text } = await chooseAction(table, 'edit');
      expect(text()).toBe(uk.knowledgeBase.dialogs.edit.loading);
    });

    it('says when the material cannot be loaded', async () => {
      vi.spyOn(service, 'openForEdit').mockResolvedValue('unreachable');
      const { fixture, text } = await chooseAction(table, 'edit');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(text()).toBe(uk.knowledgeBase.failure.unreachable);
      });
    });
  });

  it.each([
    ['a material', table, uk.knowledgeBase.dialogs.delete.page.replace('{{title}}', 'Таблиця')],
    ['a folder (all its contents will be lost)', folder, uk.knowledgeBase.dialogs.delete.folder.replace('{{title}}', 'Розділ')],
  ])('asks before deleting %s, with its name, and deletes on «Yes»', async (_name, target, message) => {
    const remove = vi.spyOn(service, 'delete').mockResolvedValue('done');
    const { child, closeModal } = await chooseAction(target, 'delete');
    const confirm = child(ConfirmDialogComponent)!;
    expect(confirm.message()).toBe(message);
    confirm.confirm.emit();
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
    expect(remove).toHaveBeenCalledWith(target);
  });

  it('keeps the window open with the failure when deleting did not work', async () => {
    vi.spyOn(service, 'delete').mockResolvedValue('unreachable');
    const { fixture, child, closeModal } = await chooseAction(table, 'delete');
    child(ConfirmDialogComponent)!.confirm.emit();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(child(ConfirmDialogComponent)!.error()).toBe(uk.knowledgeBase.failure.unreachable);
    });
    expect(closeModal).not.toHaveBeenCalled();
  });

  it('moves to the chosen folder, starting from the folder of the material', async () => {
    vi.spyOn(service, 'folderOf').mockReturnValue({ id: 'f1', type: 'folder', title: 'Розділ', items: [] });
    const move = vi.spyOn(service, 'move').mockImplementation(() => undefined);
    const { child } = await chooseAction(page, 'move');
    const dialog = child(KbMoveDialogComponent)!;
    expect(dialog.from()).toBe('f1');
    dialog.move.emit(null);
    expect(move).toHaveBeenCalledWith('p1', null);
  });
});
