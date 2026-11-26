import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { TableTemplate, TableTemplatesService } from '../../core/custom-tables/table-templates.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { ActionButtonComponent } from '../../shared/action-button/action-button.component';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { KbTemplateFormComponent } from './kb-template-form/kb-template-form.component';
import { KbTemplatesComponent } from './kb-templates.component';

describe('KbTemplatesComponent', () => {
  const donors: TableTemplate = {
    id: 'tpl1',
    name: 'База донорів',
    description: 'Опис шаблону',
    isSystemPreset: true,
    fields: [
      { id: 'c1', name: 'Назва', key: 'name', order: 0 },
      { id: 'c2', name: 'Терміни', key: 'terms', order: 1 },
    ],
  };

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideHttpClient(), provideHttpClientTesting(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  afterEach(() => vi.restoreAllMocks());

  async function render(permissions: string[] = []) {
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    const fixture = TestBed.createComponent(KbTemplatesComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    /** Кнопки біля назви шаблону `index` (підписи). */
    const buttons = (index: number) =>
      fixture.debugElement
        .queryAll(By.css('.kb-template'))
        [index].queryAll(By.directive(ActionButtonComponent))
        .map((button) => button.componentInstance as ActionButtonComponent);
    /** Чекає вікно з компонентом `type` (вікно відкривається з анімацією Ionic). */
    const dialog = async <T>(type: new (...args: never[]) => T) => {
      await vi.waitFor(
        () => {
          fixture.detectChanges();
          expect(fixture.debugElement.query(By.directive(type))).not.toBeNull();
        },
        { timeout: 3000 },
      );
      return fixture.debugElement.query(By.directive(type)).componentInstance as T;
    };
    return { fixture, element, settle, buttons, dialog };
  }

  it('shows every template as the table made from it: the name (system ones marked), the description and the header row', async () => {
    vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([donors, { ...donors, id: 'tpl2', name: 'Своя', description: null, isSystemPreset: false }]);
    const { element, settle } = await render();
    expect(element.querySelector('h1')?.textContent?.trim()).toBe(uk.knowledgeBase.templates.title);
    expect(element.querySelector('.kb-view__message')?.textContent?.trim()).toBe(uk.knowledgeBase.templates.loading);
    await settle();

    const [first, second] = Array.from(element.querySelectorAll('.kb-template'));
    expect(first.querySelector('h2')?.textContent?.trim()).toBe('База донорів');
    expect(first.querySelector('.kb-template__system')?.textContent?.trim()).toBe(uk.knowledgeBase.templates.system);
    expect(first.querySelector('.kb-template__description')?.textContent?.trim()).toBe('Опис шаблону');
    expect(Array.from(first.querySelectorAll('th')).map((cell) => cell.textContent?.trim())).toEqual(['Назва', 'Терміни']);
    expect(second.querySelector('.kb-template__system')).toBeNull();
    expect(second.querySelector('.kb-template__description')).toBeNull();
  });

  it('shows the column names with their formatting and background colour, as in the table', async () => {
    vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([
      { ...donors, fields: [{ id: 'c1', name: '<div style="background-color: #fde2e4"><b>Назва</b> фонду</div>', key: 'name', order: 0 }] },
    ]);
    const { element, settle } = await render();
    await settle();
    const head = element.querySelector<HTMLElement>('.kb-template th')!;
    expect(head.innerHTML).toBe('<b>Назва</b> фонду');
    expect(head.style.background).toBe('rgb(253, 226, 228)');
    expect(head.style.color).not.toBe('');
  });

  describe('editing and deleting (Permissions.UpdateTableTemplate, Permissions.DeleteTableTemplate)', () => {
    const own: TableTemplate = { ...donors, id: 'tpl2', name: 'Своя', description: null, isSystemPreset: false };
    const rights = ['Permissions.UpdateTableTemplate', 'Permissions.DeleteTableTemplate'];

    it('offers «Edit» and «Delete» only for own templates and only with the rights; a system one has its mark instead', async () => {
      vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([donors, own]);
      const { settle, buttons } = await render(rights);
      await settle();
      expect(buttons(0)).toEqual([]);
      expect(buttons(1).map((button) => [button.label(), button.icon()])).toEqual([
        [uk.knowledgeBase.actions.edit, 'create-outline'],
        [uk.knowledgeBase.actions.delete, 'trash-outline'],
      ]);
    });

    it('shows no buttons without the rights', async () => {
      vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([own]);
      const { settle, buttons } = await render(['Permissions.UpdateTableTemplate']);
      await settle();
      expect(buttons(0).map((button) => button.label())).toEqual([uk.knowledgeBase.actions.edit]);
    });

    it('saves the edited name, description and columns, then shows the fresh list', async () => {
      const list = vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValueOnce([own]).mockResolvedValueOnce([{ ...own, name: 'Нова' }]);
      const update = vi.spyOn(TestBed.inject(TableTemplatesService), 'update').mockResolvedValue('done');
      const { settle, dialog, element } = await render(rights);
      await settle();
      element.querySelector<HTMLElement>('.kb-template app-action-button')!.dispatchEvent(new Event('click'));
      const form = await dialog(KbTemplateFormComponent);
      expect(form.template()).toEqual(own);

      const value = { name: 'Нова', description: null, fields: [{ id: 'c1', name: 'Назва', key: 'name' }] };
      form.submitted.emit(value);
      await vi.waitFor(() => expect(list).toHaveBeenCalledTimes(2));
      expect(update).toHaveBeenCalledWith('tpl2', value);
      await settle();
      expect(element.querySelector('.kb-template h2')?.textContent?.trim()).toBe('Нова');
    });

    it('asks before deleting, deletes on «Yes» and takes the template off the list, or shows why it was not deleted', async () => {
      vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([donors, own]);
      const remove = vi.spyOn(TestBed.inject(TableTemplatesService), 'remove').mockResolvedValueOnce('unreachable').mockResolvedValueOnce('done');
      const { settle, dialog, element, fixture } = await render(rights);
      await settle();
      element.querySelectorAll<HTMLElement>('.kb-template')[1].querySelectorAll<HTMLElement>('app-action-button')[1].dispatchEvent(new Event('click'));
      const confirm = await dialog(ConfirmDialogComponent);
      expect(confirm.message()).toBe(uk.knowledgeBase.templates.deleteMessage.replace('{{name}}', 'Своя'));
      expect(remove).not.toHaveBeenCalled();

      confirm.confirm.emit();
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(confirm.error()).toBe(uk.knowledgeBase.failure.unreachable);
      });
      confirm.confirm.emit();
      await vi.waitFor(() => expect(element.querySelectorAll('.kb-template')).toHaveLength(1));
      expect(remove).toHaveBeenCalledWith('tpl2');
    });
  });

  it('says when there are no templates', async () => {
    vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValue([]);
    const { element, settle } = await render();
    await settle();
    expect(element.querySelector('.kb-view__message')?.textContent?.trim()).toBe(uk.knowledgeBase.templates.empty);
  });

  it('says why the templates were not loaded, and asks again by «Try again»', async () => {
    const list = vi.spyOn(TestBed.inject(TableTemplatesService), 'listAll').mockResolvedValueOnce('forbidden').mockResolvedValueOnce([]);
    const { element, settle } = await render();
    await settle();
    expect(element.querySelector('.kb-view__message--error')?.textContent).toContain(uk.knowledgeBase.failure.forbidden);
    (element.querySelector('.kb-view__retry') as HTMLButtonElement).click();
    await settle();
    expect(list).toHaveBeenCalledTimes(2);
  });
});
