import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import type { MockInstance } from 'vitest';

import uk from '../../../../assets/i18n/uk.json';
import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { TableTemplatesService } from '../../custom-tables/table-templates.service';
import { CurrentUserService } from '../../users/current-user.service';
import { KbItemFormComponent } from '../kb-item-form/kb-item-form.component';
import { KnowledgeBaseService } from '../knowledge-base.service';
import { KbAddDialogComponent } from './kb-add-dialog.component';

describe('KbAddDialogComponent', () => {
  let service: KnowledgeBaseService;
  let listTemplates: MockInstance<TableTemplatesService['listAll']>;

  function setUp(permissions: string[], canCreate = true) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    service = TestBed.inject(KnowledgeBaseService);
    (service as { canCreate: Signal<boolean> }).canCreate = signal(canCreate);
    listTemplates = vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([{ id: 'tpl1', name: 'База донорів', description: null, isSystemPreset: true, fields: [] }]);
    const fixture = TestBed.createComponent(KbAddDialogComponent);
    fixture.detectChanges();
    const modal = fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent;
    const closeModal = vi.spyOn(modal, 'close').mockResolvedValue();
    const child = <T>(type: new (...args: never[]) => T) => fixture.debugElement.query(By.directive(type))?.componentInstance as T | undefined;
    /** Натискає «+» (`place`) і чекає, поки відкриється вікно. */
    const press = async (folder: { id: string; title: string } | null = null) => {
      service.requestInsert({ folder });
      // Вікно відкривається з анімацією Ionic (кадри `requestAnimationFrame`, див. `test-setup.ts`);
      // запас часу — на повільний повний прогін.
      await vi.waitFor(
        () => {
          fixture.detectChanges();
          expect(child(KbItemFormComponent)).toBeDefined();
        },
        { timeout: 3000 },
      );
      return child(KbItemFormComponent)!;
    };
    return { fixture, modal, closeModal, child, press };
  }

  afterEach(() => vi.restoreAllMocks());

  it('opens by «+» with the title, the cross and the templates from the server', async () => {
    const { child, closeModal, press, fixture } = setUp(['Permissions.ViewTableTemplate']);
    const form = await press();
    expect(child(DialogHeaderComponent)?.title()).toBe(uk.knowledgeBase.dialogs.add.title);
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(form.templates()?.map(({ id, name }) => ({ id, name }))).toEqual([{ id: 'tpl1', name: 'База донорів' }]);
    });
    // Запит узято в роботу: наступний «+» відкриє вікно знову.
    expect(service.insertRequest()).toBeNull();
    child(DialogHeaderComponent)?.dismiss.emit();
    expect(closeModal).toHaveBeenCalled();
  });

  it('names the folder in the title when «+» of a folder was pressed', async () => {
    const { child, press } = setUp([]);
    await press({ id: 'f1', title: 'Розділ' });
    expect(child(DialogHeaderComponent)?.title()).toBe(uk.knowledgeBase.dialogs.add.titleInFolder.replace('{{title}}', 'Розділ'));
  });

  it('offers only «No template» without the right to see the templates', async () => {
    const { press } = setUp([]);
    const form = await press();
    expect(form.templates()).toEqual([]);
    expect(listTemplates).not.toHaveBeenCalled();
  });

  it('does not open without the right to create', async () => {
    const { fixture, child } = setUp([], false);
    service.requestInsert({ folder: null });
    fixture.detectChanges();
    await fixture.whenStable();
    expect(child(KbItemFormComponent)).toBeUndefined();
  });

  it('creates and closes, or keeps the window open with the failure', async () => {
    const { press, closeModal, fixture } = setUp([]);
    const create = vi.spyOn(service, 'create').mockResolvedValueOnce('invalid').mockResolvedValueOnce('done');
    const form = await press({ id: 'f1', title: 'Розділ' });
    const value = { title: 'Донори', description: null, type: 'customtable', templateId: null } as const;

    form.submitted.emit(value);
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(form.error()).toBe('knowledgeBase.failure.invalid');
    });
    expect(closeModal).not.toHaveBeenCalled();

    form.submitted.emit(value);
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
    expect(create).toHaveBeenLastCalledWith({ ...value, folderId: 'f1' });
  });
});
