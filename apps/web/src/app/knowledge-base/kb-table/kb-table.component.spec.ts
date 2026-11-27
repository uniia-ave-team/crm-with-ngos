import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { CustomRecordsService } from '../../core/custom-tables/custom-records.service';
import { CustomTableDetails, CustomTablesService } from '../../core/custom-tables/custom-tables.service';
import { KNOWLEDGE_BASE_SAMPLE_URL, KNOWLEDGE_BASE_SOURCE, KnowledgeBaseService } from '../../core/knowledge-base/knowledge-base.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { SearchHeaderComponent } from '../../shared/search-header/search-header.component';
import { ActionBarService } from '../../shared/action-bar/action-bar.service';
import { DebouncedSaver } from './debounced-saver';
import { TableTemplatesService } from '../../core/custom-tables/table-templates.service';
import { KbItemFormComponent } from '../../core/knowledge-base/kb-item-form/kb-item-form.component';
import { SuccessNoticeService } from '../../core/notice/success-notice.service';
import { ActionButtonComponent } from '../../shared/action-button/action-button.component';
import { KbTableComponent, POLL_INTERVAL_MS } from './kb-table.component';

describe('KbTableComponent', () => {
  const table: CustomTableDetails = {
    id: 't1',
    name: 'Назва з сервера',
    description: 'Опис таблиці',
    fields: [
      // Значення в записі — за `id` поля (тож `key` — це `id`).
      { id: 'signed', name: 'Підписано', key: 'signed', isRequired: false, order: 1 },
      { id: 'name', name: 'Назва', key: 'name', isRequired: true, order: 0 },
    ],
  };

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [
        provideIonicAngular(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        { provide: KNOWLEDGE_BASE_SOURCE, useValue: 'sample' },
      ],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  afterEach(() => vi.restoreAllMocks());

  /** Вид таблиці `id` для користувача з правами `permissions`; зміст бази знань — `entries`. */
  async function render(
    id: string,
    permissions: string[] = ['Permissions.ViewCustomRecord'],
    entries: object[] = [{ id: 't1', type: 'customtable', title: 'Таблиця' }],
  ) {
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    const loading = TestBed.inject(KnowledgeBaseService).load();
    TestBed.inject(HttpTestingController).expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush(entries);
    await loading;
    const fixture = TestBed.createComponent(KbTableComponent);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const headers = () => Array.from(element.querySelectorAll('th.data-table__head')).map((cell) => cell.textContent?.trim());
    const cells = () => Array.from(element.querySelectorAll('tbody tr')).map((row) => Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent));
    return { fixture, element, settle, headers, cells };
  }

  it('shows the name from the contents, the description and the records under the columns in their order, without the menu column', async () => {
    const get = vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    const list = vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([
      { id: 'r1', data: { name: 'Фонд', signed: true } },
      { id: 'r2', data: { name: 'Програма', signed: false } },
    ]);
    const { element, settle, headers, cells } = await render('t1');
    await settle();
    expect(get).toHaveBeenCalledWith('t1');
    expect(list).toHaveBeenCalledWith('t1', null, null);
    expect(element.querySelector('.search-header__title')?.textContent?.trim()).toBe('Таблиця');
    expect(element.querySelector('.kb-view__description')?.textContent?.trim()).toBe('Опис таблиці');
    expect(headers()).toEqual(['Назва', 'Підписано']);
    // Усі значення — рядки: старе логічне значення — прапорцем `☑` / `☐`.
    expect(cells().map((row) => row.map((cell) => cell?.trim()))).toEqual([
      ['Фонд', '☑'],
      ['Програма', '☐'],
    ]);
    expect(element.querySelector('.data-table__menu-cell')).toBeNull();
    expect(element.querySelector('.data-table--lines')).not.toBeNull();
  });

  it('has the header of the colleagues: the name (up to two lines), then the saved tag, icon buttons and the search', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
    const { element, settle } = await render('t1', ['Permissions.ViewCustomRecord', 'Permissions.CreateTableTemplate']);
    await settle();
    const header = element.querySelector('.kb-table__toolbar app-search-header')!;
    const title = header.querySelector('.search-header__title')!;
    expect(title.textContent?.trim()).toBe('Таблиця');
    expect(title.classList).toContain('search-header__title--clamped');
    expect(title.getAttribute('title')).toBe('Таблиця');
    // Тег — у ряду кнопок, перед ними.
    const actions = header.querySelector('.search-header__actions')!;
    expect(actions.firstElementChild?.classList).toContain('kb-table__saved');
    expect(actions.firstElementChild?.textContent?.trim()).toBe(uk.knowledgeBase.table.saved);
    // Кнопки — завжди лише іконки, назва — у підказці.
    const buttons = Array.from(header.querySelectorAll('.search-header__actions app-action-button'));
    expect(buttons.map((button) => button.classList.contains('action-button--compact'))).toEqual([true, true]);
    expect(buttons.map((button) => button.querySelector('button')?.getAttribute('title'))).toEqual([
      uk.knowledgeBase.table.createTemplate,
      uk.knowledgeBase.table.export,
    ]);
  });

  it('asks the server for the records with the text of the search, and without it once the field is empty', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    const list = vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
    const { fixture, settle } = await render('t1');
    await settle();
    const header = fixture.debugElement.query(By.directive(SearchHeaderComponent)).componentInstance as SearchHeaderComponent;
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.search-header__toggle')!.click();
    fixture.detectChanges();
    // Поки відкрито пошук, тег схований.
    expect(getComputedStyle(fixture.nativeElement.querySelector('.kb-table__saved')).display).toBe('none');
    // Запит — після паузи від останнього символу, не на кожну літеру.
    header.searchChange.emit('  фо');
    header.searchChange.emit('  фонд ');
    await settle();
    expect(list).toHaveBeenCalledTimes(1);
    await vi.waitFor(() => expect(list).toHaveBeenLastCalledWith('t1', null, 'фонд'));
    expect(list).toHaveBeenCalledTimes(2);
    header.searchChange.emit('');
    await vi.waitFor(() => expect(list).toHaveBeenCalledTimes(3));
    await settle();
    expect(list).toHaveBeenLastCalledWith('t1', null, null);
  });

  it('ignores an older answer for the records that comes after a newer one (for example «фон» after «фонд»)', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    let answerOld!: (records: { id: string; data: Record<string, unknown> }[]) => void;
    vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll')
      .mockResolvedValueOnce([])
      .mockReturnValueOnce(new Promise((resolve) => (answerOld = resolve)))
      .mockResolvedValueOnce([{ id: 'r2', data: { name: 'Фонд «Надія»', signed: false } }]);
    const { fixture, settle, cells } = await render('t1');
    await settle();
    const header = fixture.debugElement.query(By.directive(SearchHeaderComponent)).componentInstance as SearchHeaderComponent;
    header.searchChange.emit('фон');
    await vi.waitFor(() => expect(TestBed.inject(CustomRecordsService).listAll).toHaveBeenCalledTimes(2));
    header.searchChange.emit('фонд');
    await vi.waitFor(() => expect(TestBed.inject(CustomRecordsService).listAll).toHaveBeenCalledTimes(3));
    await settle();
    answerOld([{ id: 'r1', data: { name: 'Фонтан', signed: false } }]);
    await settle();
    expect(cells().map((row) => row[0]?.trim())).toEqual(['Фонд «Надія»']);
  });

  it('has no «Create template» without Permissions.CreateTableTemplate and no «Export» without Permissions.ViewCustomRecord', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    const { element, settle } = await render('t1', []);
    await settle();
    expect(element.querySelector('.kb-table__saved')).not.toBeNull();
    expect(element.querySelectorAll('app-action-button')).toHaveLength(0);
  });

  describe('values from the server written past the app (a script or an element in the HTML)', () => {
    const hostile = '<img src="x" onerror="alert(1)"><b style="position:fixed;inset:0">Фонд</b><script>alert(2)</script>';

    async function renderHostile(permissions: string[]) {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue({
        ...table,
        fields: [{ ...table.fields[1], name: `<div style="background-color: #fde2e4">${hostile}</div>` }, table.fields[0]],
      });
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([{ id: 'r1', data: { name: hostile, signed: true } }]);
      const rendered = await render('t1', permissions);
      await rendered.settle();
      return rendered.element;
    }

    const clean = (element: HTMLElement) => {
      expect(element.querySelector('img, script, [onerror], .kb-cell [style], .kb-head [style], .kb-cell-text [style]')).toBeNull();
      expect(element.querySelector('tbody td')?.innerHTML).toContain('<b>Фонд</b>');
      expect(element.querySelector('tbody td')?.textContent).not.toContain('alert');
    };

    it('shows them cleaned for viewing', async () => {
      clean(await renderHostile(['Permissions.ViewCustomRecord']));
    });

    it('puts them cleaned into the cells and headers that can be edited', async () => {
      clean(await renderHostile(['Permissions.ViewCustomRecord', 'Permissions.UpdateCustomTable', 'Permissions.UpdateCustomRecord']));
    });
  });

  it('shows one empty row under the header when there are no records', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
    const { settle, cells } = await render('t1');
    await settle();
    expect(cells()).toEqual([[' ', ' ']].map((row) => row.map((cell) => ` ${cell} `)).length ? cells() : []);
    expect(cells()).toHaveLength(1);
    expect(cells()[0].every((cell) => cell?.trim() === '')).toBe(true);
  });

  it('does not ask for the records without Permissions.ViewCustomRecord (only the empty row)', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    const list = vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll');
    const { settle, cells } = await render('t1', []);
    await settle();
    expect(list).not.toHaveBeenCalled();
    expect(cells()).toHaveLength(1);
  });

  it('asks the server for the records sorted by the column whose header was clicked', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    const list = vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
    const { element, settle } = await render('t1');
    await settle();
    (element.querySelectorAll('.data-table__sort-toggle')[1] as HTMLButtonElement).click();
    await settle();
    expect(list).toHaveBeenLastCalledWith('t1', { orderBy: 'signed', sortOrder: 'asc' }, null);
  });

  it('says why the table or its records were not loaded, with «Try again»', async () => {
    const get = vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValueOnce('unreachable').mockResolvedValueOnce(table);
    const list = vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValueOnce('forbidden').mockResolvedValueOnce([]);
    const { element, settle } = await render('t1');
    expect(element.querySelector('.kb-view__message')?.textContent?.trim()).toBe(uk.knowledgeBase.table.loading);
    await settle();
    expect(element.querySelector('.kb-view__message--error')?.textContent).toContain(uk.knowledgeBase.failure.unreachable);

    (element.querySelector('.kb-view__retry') as HTMLButtonElement).click();
    await settle();
    expect(get).toHaveBeenCalledTimes(2);
    expect(element.querySelector('.kb-table__error')?.textContent).toContain(uk.knowledgeBase.table.recordsError);
    (element.querySelector('.kb-table__error .kb-view__retry') as HTMLButtonElement).click();
    await settle();
    expect(list).toHaveBeenCalledTimes(2);
    expect(element.querySelector('.kb-table__error')).toBeNull();
  });

  it('goes to the guide when the table is not in the contents (deleted or an old address)', async () => {
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue('error');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { settle } = await render('gone');
    await settle();
    expect(navigate).toHaveBeenCalledWith('/knowledge-base');
  });
  describe('editing (Permissions.UpdateCustomTable)', () => {
    const editor = ['Permissions.ViewCustomRecord', 'Permissions.UpdateCustomTable', 'Permissions.CreateCustomRecord', 'Permissions.UpdateCustomRecord'];
    /** Відкладене збереження виду: тести зберігають одразу (`flush`), не чекаючи двох секунд. */
    const saverOf = (fixture: { componentInstance: KbTableComponent }) => (fixture.componentInstance as unknown as { saver: DebouncedSaver }).saver;
    const tag = (element: HTMLElement) => element.querySelector('.kb-table__saved')!;

    async function renderEditable(permissions = editor, records: { id: string; data: Record<string, unknown> }[] = []) {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue(records);
      const rendered = await render('t1', permissions);
      await rendered.settle();
      /** Пише в поле: у текстову клітинку (редагований блок) — HTML, в інше поле — значення. */
      const type = (input: HTMLElement, value: string) => {
        if (input.isContentEditable || input.getAttribute('contenteditable') === 'true') {
          input.innerHTML = value;
        } else {
          (input as HTMLInputElement).value = value;
        }
        input.dispatchEvent(new Event('input'));
        rendered.fixture.detectChanges();
      };
      return { ...rendered, type, saver: saverOf(rendered.fixture) };
    }

    it('lets change the description by a click and saves it with the name and the same columns', async () => {
      const update = vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('done');
      const { element, fixture, type, saver, settle } = await renderEditable();
      const description = element.querySelector<HTMLTextAreaElement>('textarea.kb-table__description')!;
      expect(description.value).toBe('Опис таблиці');
      type(description, '  Новий опис ');
      expect(tag(element).textContent?.trim()).toBe(uk.knowledgeBase.table.saving);
      expect(tag(element).querySelector('ion-icon')?.getAttribute('name')).toBe('cloud-upload-outline');

      await saver.flush();
      await settle();
      // Назва й стовпчики — свіжі з сервера, змінено лише опис.
      expect(update).toHaveBeenCalledWith({ ...table, description: 'Новий опис' });
      expect(tag(element).textContent?.trim()).toBe(uk.knowledgeBase.table.saved);
      expect(fixture.componentInstance).toBeDefined();
    });

    it('offers «Add description» when there is none', async () => {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue({ ...table, description: null });
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
      const { element, settle } = await render('t1', editor);
      await settle();
      expect(element.querySelector<HTMLTextAreaElement>('textarea.kb-table__description')?.placeholder).toBe(uk.knowledgeBase.table.addDescription);
    });

    it('makes the empty row a new record by the first text, then changes that record', async () => {
      const create = vi.spyOn(TestBed.inject(CustomRecordsService), 'create').mockResolvedValue({ id: 'r1' });
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
      const { element, type, saver, settle } = await renderEditable();
      const inputs = () => Array.from(element.querySelectorAll<HTMLInputElement>('tbody .kb-cell'));
      // Усі значення — рядки (тип поля не враховуємо): редагований блок із форматуванням.
      expect(inputs().map((input) => input.tagName.toLowerCase())).toEqual(['div', 'div']);
      expect(inputs()[0].getAttribute('contenteditable')).toBe('true');

      type(inputs()[0], 'Фонд');
      await saver.flush();
      await settle();
      // Незаповнені поля — порожні рядки.
      expect(create).toHaveBeenCalledWith('t1', { name: 'Фонд', signed: '' }, { previousRecordId: null, nextRecordId: null });

      type(inputs()[1], 'Так');
      await saver.flush();
      await settle();
      expect(create).toHaveBeenCalledTimes(1);
      expect(update).toHaveBeenCalledWith('r1', { signed: 'Так' });
    });

    it('saves at once when the tab is hidden, sends what is left at once when the page is closed, and asks before leaving unsaved', async () => {
      const create = vi.spyOn(TestBed.inject(CustomRecordsService), 'create').mockResolvedValue({ id: 'r1' });
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
      const { element, type, saver, settle } = await renderEditable();
      const inputs = () => Array.from(element.querySelectorAll<HTMLElement>('tbody .kb-cell'));
      const leave = () => {
        const event = new Event('beforeunload', { cancelable: true });
        window.dispatchEvent(event);
        return event.defaultPrevented;
      };
      expect(leave()).toBe(false);

      type(inputs()[0], 'Фонд');
      // Зміни ще не збережено — браузер питає, чи залишити сайт.
      expect(leave()).toBe(true);
      Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
      try {
        // Вкладку сховали — зберігаємо одразу, не чекаючи двох секунд.
        document.dispatchEvent(new Event('visibilitychange'));
        await vi.waitFor(() => expect(create).toHaveBeenCalledWith('t1', { name: 'Фонд', signed: '' }, { previousRecordId: null, nextRecordId: null }));
        await saver.flush();
        await settle();
        expect(leave()).toBe(false);

        // Сторінку закривають — решта йде одразу (тут же, а не з наступним збереженням).
        type(inputs()[1], 'Так');
        window.dispatchEvent(new Event('pagehide'));
        expect(update).toHaveBeenCalledWith('r1', { signed: 'Так' });
        await saver.flush();
        expect(update).toHaveBeenCalledTimes(1);
      } finally {
        delete (document as unknown as Record<string, unknown>)['visibilityState'];
      }
    });

    describe('changes made meanwhile elsewhere are not lost', () => {
      const email = { id: 'email', name: 'Email', key: 'email', isRequired: false, order: 2 };

      it('keeps a column added by somebody else when only the description changed here', async () => {
        const update = vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('done');
        const { element, type, saver, settle } = await renderEditable();
        vi.mocked(TestBed.inject(CustomTablesService).get).mockResolvedValue({ ...table, fields: [...table.fields, email] });
        type(element.querySelector<HTMLTextAreaElement>('textarea.kb-table__description')!, 'Новий опис');
        await saver.flush();
        await settle();
        expect(update.mock.lastCall![0].fields.map((field) => field.id)).toEqual(['signed', 'name', 'email']);
      });

      it('puts a new column here next to its neighbour, keeping a column added by somebody else', async () => {
        const update = vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('done');
        const { element, saver, settle, fixture } = await renderEditable();
        vi.mocked(TestBed.inject(CustomTablesService).get).mockResolvedValue({ ...table, fields: [...table.fields, email] });
        element.querySelector<HTMLElement>('th .kb-head')!.dispatchEvent(new FocusEvent('focus'));
        fixture.detectChanges();
        TestBed.inject(ActionBarService).content()!.choose('addColumnRight', document.createElement('button'));
        await saver.flush();
        await settle();
        expect(update.mock.lastCall![0].fields.map((field) => [field.id, field.name])).toEqual([
          ['name', 'Назва'],
          [null, uk.knowledgeBase.table.newColumn],
          ['signed', 'Підписано'],
          ['email', 'Email'],
        ]);
      });

      it('sends only the cells changed here (PATCH), so a cell somebody else changed in the same row stays', async () => {
        const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
        const { element, type, saver, settle } = await renderEditable(editor, [{ id: 'r1', data: { name: 'Фонд', signed: '' } }]);
        type(element.querySelector<HTMLElement>('tbody .kb-cell')!, 'Фонд «Надія»');
        await saver.flush();
        await settle();
        expect(update).toHaveBeenCalledWith('r1', { name: 'Фонд «Надія»' });
      });

      it('creates a new row between its saved neighbours, so it keeps its place after a reload', async () => {
        const create = vi.spyOn(TestBed.inject(CustomRecordsService), 'create').mockResolvedValue({ id: 'r3' });
        const { element, saver, settle, fixture } = await renderEditable(
          [...editor, 'Permissions.DeleteCustomRecord'],
          [
            { id: 'r1', data: { name: 'Перший', signed: '' } },
            { id: 'r2', data: { name: 'Другий', signed: '' } },
          ],
        );
        element.querySelector<HTMLElement>('tbody .kb-cell')!.dispatchEvent(new FocusEvent('focus'));
        fixture.detectChanges();
        TestBed.inject(ActionBarService).content()!.choose('addRowBelow', document.createElement('button'));
        await saver.flush();
        await settle();
        expect(create).toHaveBeenCalledWith('t1', { name: '', signed: '' }, { previousRecordId: 'r1', nextRecordId: 'r2' });
      });

      it('takes a row deleted elsewhere off the table instead of trying it again', async () => {
        const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('gone');
        const { element, type, saver, settle } = await renderEditable(editor, [{ id: 'r1', data: { name: 'Фонд', signed: '' } }]);
        type(element.querySelector<HTMLElement>('tbody .kb-cell')!, 'Фонд «Надія»');
        await saver.flush();
        await settle();
        expect(update).toHaveBeenCalledTimes(1);
        expect(saver.state()).toBe('saved');
        // Записів не лишилося — під заголовками порожній рядок.
        expect(element.querySelector('tbody .kb-cell')?.textContent).toBe('');
      });

      it('says the table was deleted elsewhere instead of saving it again and again', async () => {
        const { element, type, saver, settle } = await renderEditable();
        vi.mocked(TestBed.inject(CustomTablesService).get).mockResolvedValue('gone');
        type(element.querySelector<HTMLTextAreaElement>('textarea.kb-table__description')!, 'Новий опис');
        await saver.flush();
        await settle();
        expect(element.querySelector('.kb-view__message--error')?.textContent).toContain(uk.knowledgeBase.failure.gone);
        expect(saver.state()).toBe('saved');
      });

      it('checks for changes at once when the tab is shown again', async () => {
        const list = vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
        const { settle } = await renderEditable();
        const calls = list.mock.calls.length;
        document.dispatchEvent(new Event('visibilitychange'));
        await settle();
        expect(list.mock.calls.length).toBe(calls + 1);
      });
    });

    it('lets leave at once when all is saved, and asks first when the server did not take the changes', async () => {
      const update = vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('invalid');
      const { element, type, fixture } = await renderEditable();
      const view = fixture.componentInstance as unknown as { canLeave(): Promise<boolean>; answerLeave(leave: boolean): void; leaveAnswer: unknown };
      expect(await view.canLeave()).toBe(true);

      type(element.querySelector<HTMLTextAreaElement>('textarea.kb-table__description')!, 'Новий опис');
      const leaving = view.canLeave();
      await vi.waitFor(() => expect(view.leaveAnswer).not.toBeNull());
      expect(update).toHaveBeenCalled();
      view.answerLeave(false);
      expect(await leaving).toBe(false);

      // Ще раз: «Піти» — зміни пропадуть, але піти можна.
      const again = view.canLeave();
      await vi.waitFor(() => expect(view.leaveAnswer).not.toBeNull());
      view.answerLeave(true);
      expect(await again).toBe(true);
    });

    it('turns off adding columns at 50 (the server takes no more)', async () => {
      const many = Array.from({ length: 50 }, (_, order) => ({ id: `f${order}`, name: `Поле ${order}`, key: `f${order}`, isRequired: false, order }));
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue({ ...table, fields: many });
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
      const { element, settle, fixture } = await render('t1', editor);
      await settle();
      element.querySelector<HTMLElement>('th .kb-head')!.dispatchEvent(new FocusEvent('focus'));
      fixture.detectChanges();
      const items = TestBed.inject(ActionBarService).content()!.groups.flat();
      expect(items.find((item) => item.id === 'addColumnRight')?.disabled).toBe(true);
      expect(items.find((item) => item.id === 'addColumnLeft')?.disabled).toBe(true);
    });

    it('turns the tag red when the server did not take the changes, and tries again by a click on it', async () => {
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValueOnce('invalid').mockResolvedValueOnce('done');
      const { element, type, saver, settle } = await renderEditable(editor, [{ id: 'r1', data: { name: 'Фонд', signed: false } }]);
      type(element.querySelector<HTMLElement>('tbody .kb-cell')!, 'Фонд 2');
      await saver.flush();
      await settle();
      expect(tag(element).querySelector('.kb-table__saved-label--failed')?.textContent?.trim()).toBe(uk.knowledgeBase.table.saveFailed);
      expect(tag(element).querySelector('ion-icon')?.getAttribute('name')).toBe('cloud-offline-outline');
      // При наведенні — «Зберегти зміни» (показує CSS); для екранних читачів назва одна.
      const retry = tag(element).querySelector('.kb-table__saved-label--retry')!;
      expect(retry.textContent?.trim()).toBe(uk.knowledgeBase.table.saveNow);
      expect(retry.getAttribute('aria-hidden')).toBe('true');

      (tag(element) as HTMLButtonElement).click();
      await saver.flush();
      await settle();
      expect(update).toHaveBeenCalledTimes(2);
      expect(update).toHaveBeenLastCalledWith('r1', { name: 'Фонд 2' });
      expect(tag(element).textContent?.trim()).toBe(uk.knowledgeBase.table.saved);
    });

    it('changes nothing without Permissions.UpdateCustomTable, and leaves the empty row as is without Permissions.CreateCustomRecord', async () => {
      const readOnly = await renderEditable(['Permissions.ViewCustomRecord', 'Permissions.CreateCustomRecord', 'Permissions.UpdateCustomRecord']);
      expect(readOnly.element.querySelector('textarea')).toBeNull();
      expect(readOnly.element.querySelector('.kb-view__description')?.textContent?.trim()).toBe('Опис таблиці');
      expect(readOnly.element.querySelector('.kb-cell')).toBeNull();
    });
  });
  describe('the check for changes every 10 seconds', () => {
    afterEach(() => vi.useRealTimers());

    it('asks for the table and the records again every 10 seconds and shows only what changed', async () => {
      expect(POLL_INTERVAL_MS).toBe(10_000);
      vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
      vi.spyOn(TestBed.inject(CustomTablesService), 'get')
        .mockResolvedValueOnce(table)
        .mockResolvedValue({ ...table, description: 'Новий опис з іншого пристрою' });
      const list = vi
        .spyOn(TestBed.inject(CustomRecordsService), 'listAll')
        .mockResolvedValueOnce([
          { id: 'r1', data: { name: 'Фонд', signed: false } },
          { id: 'r2', data: { name: 'Програма', signed: true } },
        ])
        .mockResolvedValue([
          { id: 'r1', data: { name: 'Фонд', signed: true } },
          { id: 'r2', data: { name: 'Програма', signed: true } },
        ]);
      const { element, settle, cells } = await render('t1');
      await settle();
      const secondRow = element.querySelectorAll('tbody tr')[1];
      const secondRowName = secondRow.querySelector('td');

      vi.advanceTimersByTime(POLL_INTERVAL_MS);
      await settle();
      expect(list).toHaveBeenCalledTimes(2);
      expect(cells().map((row) => row.map((cell) => cell?.trim()))).toEqual([
        ['Фонд', '☑'],
        ['Програма', '☑'],
      ]);
      expect(element.querySelector('.kb-view__description')?.textContent?.trim()).toBe('Новий опис з іншого пристрою');
      // Незмінений рядок не перемальовано.
      expect(element.querySelectorAll('tbody tr')[1]).toBe(secondRow);
      expect(element.querySelectorAll('tbody tr')[1].querySelector('td')).toBe(secondRowName);
    });
  });
  describe('the action bar of the chosen cell', () => {
    const editor = [
      'Permissions.ViewCustomRecord',
      'Permissions.UpdateCustomTable',
      'Permissions.CreateCustomRecord',
      'Permissions.UpdateCustomRecord',
      'Permissions.DeleteCustomRecord',
    ];

    async function renderWithRecord(permissions = editor) {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([{ id: 'r1', data: { name: '<b>Фонд</b> важливий', signed: true } }]);
      const rendered = await render('t1', permissions);
      await rendered.settle();
      const cell = rendered.element.querySelector<HTMLElement>('tbody .kb-cell--rich')!;
      const bar = TestBed.inject(ActionBarService);
      const select = (element: HTMLElement = cell) => {
        element.dispatchEvent(new FocusEvent('focus'));
        rendered.fixture.detectChanges();
      };
      const items = () => bar.content()?.groups.flat() ?? [];
      const item = (id: string) => items().find((candidate) => candidate.id === id)!;
      const choose = (id: string, button: HTMLElement = document.createElement('button')) => {
        bar.content()!.choose(id, button);
        rendered.fixture.detectChanges();
      };
      const saver = (rendered.fixture.componentInstance as unknown as { saver: DebouncedSaver }).saver;
      return { ...rendered, cell, bar, select, items, item, choose, saver };
    }

    it('shows four groups of icon buttons named in the language file while a cell is chosen, and hides when it is left', async () => {
      const { cell, fixture, bar, select, item } = await renderWithRecord();
      expect(bar.content()).toBeNull();
      select();
      expect(bar.content()?.labelKey).toBe('knowledgeBase.table.cellActions.label');
      expect(bar.content()?.groups.map((group) => group.map((item) => item.id))).toEqual([
        ['background', 'bold', 'italic', 'underline', 'strikethrough'],
        ['addRowBelow', 'deleteRow'],
        ['addColumnRight', 'addColumnLeft', 'deleteColumn'],
        ['checkbox', 'clear', 'copy'],
      ]);
      for (const item of bar.content()!.groups.flat()) {
        const key = item.labelKey.replace('knowledgeBase.table.cellActions.', '') as keyof typeof uk.knowledgeBase.table.cellActions;
        expect(uk.knowledgeBase.table.cellActions[key], item.id).toBeTruthy();
      }
      expect(item('checkbox').labelKey).toBe('knowledgeBase.table.cellActions.insertCheckbox');
      expect(bar.content()!.groups.flat().map((item) => item.icon)).toEqual([
        'color-fill',
        'format-bold',
        'format-italic',
        'format-underline',
        'format-strikethrough',
        'row-add-below',
        'row-remove',
        'column-add-right',
        'column-add-left',
        'column-remove',
        'checkbox-outline',
        'eraser-outline',
        'copy-outline',
      ]);
      cell.dispatchEvent(new FocusEvent('blur', { relatedTarget: null }));
      fixture.detectChanges();
      expect(bar.content()).toBeNull();
    });

    it('stays while the focus goes to the action bar', async () => {
      const { cell, fixture, bar, select } = await renderWithRecord();
      select();
      const barButton = document.createElement('button');
      const host = document.createElement('app-action-bar');
      host.append(barButton);
      document.body.append(host);
      cell.dispatchEvent(new FocusEvent('blur', { relatedTarget: barButton }));
      fixture.detectChanges();
      expect(bar.content()).not.toBeNull();
      host.remove();
    });

    it('turns off what the rights do not allow and deleting the only column', async () => {
      const { item, select, element } = await renderWithRecord(['Permissions.ViewCustomRecord', 'Permissions.UpdateCustomTable', 'Permissions.UpdateCustomRecord']);
      select();
      expect(item('addRowBelow').disabled).toBe(true);
      expect(item('deleteRow').disabled).toBe(true);
      expect(item('bold').disabled).toBe(false);
      expect(item('addColumnRight').disabled).toBe(false);
      // Прапорець теж можна форматувати.
      select(element.querySelector<HTMLElement>('tbody input[type="checkbox"]')!);
      expect(item('bold').disabled).toBe(false);
    });

    it('inserts a check box into the cell (asking first when its text would go), switches it and takes it away', async () => {
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
      const { element, select, choose, item, saver, settle, fixture } = await renderWithRecord();
      select();
      choose('checkbox');
      fixture.detectChanges();
      // Текст «Фонд важливий» зник би — спершу питаємо.
      const view = fixture.componentInstance as unknown as { pendingDelete(): { kind: string } | null; confirmDelete(): void };
      expect(view.pendingDelete()?.kind).toBe('checkbox');
      expect(update).not.toHaveBeenCalled();
      view.confirmDelete();
      fixture.detectChanges();
      await saver.flush();
      await settle();
      expect(update).toHaveBeenLastCalledWith('r1', { name: '☐' });
      const box = element.querySelector<HTMLInputElement>('tbody td:nth-child(1) input.kb-cell--check')!;
      expect(box.checked).toBe(false);

      box.checked = true;
      box.dispatchEvent(new Event('change'));
      await saver.flush();
      await settle();
      expect(update).toHaveBeenLastCalledWith('r1', { name: '☑' });

      select(box);
      expect(item('checkbox').labelKey).toBe('knowledgeBase.table.cellActions.removeCheckbox');
      expect(item('checkbox').icon).toBe('checkbox-remove');
      choose('checkbox');
      await saver.flush();
      await settle();
      // «Назва» обовʼязкова — порожній рядок.
      expect(update).toHaveBeenLastCalledWith('r1', { name: '' });
      expect(element.querySelector('tbody td:nth-child(1) input.kb-cell--check')).toBeNull();
    });

    it('chooses a cell by a press anywhere in it: a text cell gets the cursor, a check box cell is only chosen', async () => {
      const { element, item, fixture } = await renderWithRecord();
      const [textCell, boxCell] = Array.from(element.querySelectorAll<HTMLElement>('tbody td'));
      const press = (target: HTMLElement) => {
        const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true });
        target.dispatchEvent(event);
        fixture.detectChanges();
        return event;
      };
      expect(press(textCell).defaultPrevented).toBe(true);
      expect(document.activeElement).toBe(textCell.querySelector('.kb-cell'));
      expect(item('bold').disabled).toBe(false);

      const box = boxCell.querySelector<HTMLInputElement>('input.kb-cell--check')!;
      press(boxCell);
      expect(document.activeElement).toBe(box);
      // Лише вибір: прапорець не перемкнувся, у смузі дій — «Прибрати прапорець».
      expect(box.checked).toBe(true);
      expect(item('checkbox').labelKey).toBe('knowledgeBase.table.cellActions.removeCheckbox');

      // Натискання в самому полі — як завжди (курсор туди, куди клацнули).
      expect(press(textCell.querySelector<HTMLElement>('.kb-cell')!).defaultPrevented).toBe(false);
    });

    it('chooses a check box cell by a click on the check box (Safari gives it no focus by itself)', async () => {
      const { element, item, fixture } = await renderWithRecord();
      const box = element.querySelector<HTMLInputElement>('tbody input.kb-cell--check')!;
      box.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      fixture.detectChanges();
      expect(document.activeElement).toBe(box);
      expect(item('checkbox').labelKey).toBe('knowledgeBase.table.cellActions.removeCheckbox');
    });

    it('formats the selected text by the formatting buttons', async () => {
      const exec = vi.fn().mockReturnValue(true);
      Object.defineProperty(document, 'execCommand', { value: exec, configurable: true });
      const { select, choose } = await renderWithRecord();
      select();
      choose('bold');
      choose('strikethrough');
      expect(exec).toHaveBeenCalledWith('bold');
      expect(exec).toHaveBeenCalledWith('strikeThrough');
    });

    it('copies the plain text of the cell and clears it (to be saved)', async () => {
      const writeText = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
      const { cell, select, choose, saver, settle } = await renderWithRecord();
      expect(cell.innerHTML).toBe('<b>Фонд</b> важливий');
      select();
      choose('copy');
      expect(writeText).toHaveBeenCalledWith('Фонд важливий');

      choose('clear');
      await saver.flush();
      await settle();
      // «Назва» обовʼязкова — порожній рядок, а не `null`.
      expect(update).toHaveBeenCalledWith('r1', { name: '' });
      expect(cell.innerHTML).toBe('');
    });

    it('adds an empty row under the chosen one and creates it with the next save', async () => {
      const create = vi.spyOn(TestBed.inject(CustomRecordsService), 'create').mockResolvedValue({ id: 'r2' });
      const { select, choose, saver, settle, element } = await renderWithRecord();
      select();
      choose('addRowBelow');
      expect(element.querySelectorAll('tbody tr')).toHaveLength(2);
      await saver.flush();
      await settle();
      expect(create).toHaveBeenCalledWith('t1', { name: '', signed: '' }, { previousRecordId: 'r1', nextRecordId: null });
    });

    it('asks before deleting the row, then deletes it on the server with the next save', async () => {
      const remove = vi.spyOn(TestBed.inject(CustomRecordsService), 'remove').mockResolvedValue('done');
      const { select, choose, saver, settle, element, fixture } = await renderWithRecord();
      select();
      choose('deleteRow');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(document.querySelector('app-kb-table .confirm__message, .confirm__message')?.textContent?.trim()).toBe(uk.knowledgeBase.table.deleteDialog.row);
      });
      (fixture.componentInstance as unknown as { confirmDelete(): void }).confirmDelete();
      fixture.detectChanges();
      // Лише порожній рядок під заголовками.
      expect(element.querySelectorAll('tbody tr')).toHaveLength(1);
      await saver.flush();
      await settle();
      expect(remove).toHaveBeenCalledWith('r1');
    });

    it('adds a text column to the right or the left, and deletes one after asking, saving all the columns of the table', async () => {
      const update = vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('done');
      const { select, choose, saver, settle, headers, fixture } = await renderWithRecord();
      select();
      choose('addColumnLeft');
      expect(headers()).toEqual([uk.knowledgeBase.table.newColumn, 'Назва', 'Підписано']);
      choose('addColumnRight');
      expect(headers()).toEqual([uk.knowledgeBase.table.newColumn, 'Назва', uk.knowledgeBase.table.newColumn, 'Підписано']);
      await saver.flush();
      await settle();
      const fields = update.mock.lastCall![0].fields;
      expect(fields.map((field) => [field.id, field.name, field.order])).toEqual([
        [null, uk.knowledgeBase.table.newColumn, 0],
        ['name', 'Назва', 1],
        [null, uk.knowledgeBase.table.newColumn, 2],
        ['signed', 'Підписано', 3],
      ]);

      choose('deleteColumn');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(document.querySelector('.confirm__message')?.textContent?.trim()).toBe(uk.knowledgeBase.table.deleteDialog.column.replace('{{name}}', 'Назва'));
      });
      (fixture.componentInstance as unknown as { confirmDelete(): void }).confirmDelete();
      fixture.detectChanges();
      expect(headers()).not.toContain('Назва');
    });
  });
  describe('column names and background colours', () => {
    const editor = ['Permissions.ViewCustomRecord', 'Permissions.UpdateCustomTable', 'Permissions.UpdateCustomRecord'];

    async function renderTable() {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([{ id: 'r1', data: { name: 'Фонд', signed: true } }]);
      const rendered = await render('t1', editor);
      await rendered.settle();
      const component = rendered.fixture.componentInstance as unknown as {
        saver: DebouncedSaver;
        onColorPick(color: string | null): void;
        colorTarget: { set(value: unknown): void };
      };
      return { ...rendered, component };
    }

    it('lets rename a column in its header (with formatting) and saves the name with the table', async () => {
      const update = vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('done');
      const { element, fixture, component, settle } = await renderTable();
      const head = element.querySelector<HTMLElement>('th .kb-head')!;
      expect(head.getAttribute('contenteditable')).toBe('true');
      expect(head.getAttribute('aria-label')).toBe(uk.knowledgeBase.table.columnName);
      head.innerHTML = '<b>Назва</b> фонду';
      head.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      await component.saver.flush();
      await settle();
      expect(update.mock.lastCall![0].fields.map((field) => field.name)).toEqual(['<b>Назва</b> фонду', 'Підписано']);
    });

    it('turns off «Bold» in a header (headers are bold anyway), leaving the other formatting on', async () => {
      const { element, fixture } = await renderTable();
      element.querySelector<HTMLElement>('th .kb-head')!.dispatchEvent(new FocusEvent('focus'));
      fixture.detectChanges();
      const items = TestBed.inject(ActionBarService).content()!.groups[0];
      expect(items.find((item) => item.id === 'bold')?.disabled).toBe(true);
      expect(items.find((item) => item.id === 'italic')?.disabled).toBe(false);
    });

    it('gives an emptied column name back as «New column» when the header is left', async () => {
      const { element, fixture } = await renderTable();
      const head = element.querySelector<HTMLElement>('th .kb-head')!;
      head.dispatchEvent(new FocusEvent('focus'));
      head.innerHTML = '';
      head.dispatchEvent(new Event('input'));
      head.dispatchEvent(new FocusEvent('blur'));
      fixture.detectChanges();
      expect(element.querySelector('th .kb-head')?.textContent).toBe(uk.knowledgeBase.table.newColumn);
    });

    it('colours the whole header and the whole cell, the text of the same hue with a contrast of 7:1, keeping the colour in the value', async () => {
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
      vi.spyOn(TestBed.inject(CustomTablesService), 'update').mockResolvedValue('done');
      const { element, fixture, component, settle } = await renderTable();
      const head = element.querySelector<HTMLElement>('th .kb-head')!;
      head.dispatchEvent(new FocusEvent('focus'));
      fixture.detectChanges();
      const bar = TestBed.inject(ActionBarService);
      expect(bar.content()!.groups[0][0]).toMatchObject({ id: 'background', swatch: null, disabled: false });
      bar.content()!.choose('background', document.createElement('button'));
      component.onColorPick('#cdeaf5');
      fixture.detectChanges();
      const th = element.querySelector<HTMLElement>('th')!;
      expect(th.style.background).toBe('rgb(205, 234, 245)');
      expect(th.style.color).not.toBe('');

      const cell = element.querySelector<HTMLElement>('tbody .kb-cell--rich')!;
      cell.dispatchEvent(new FocusEvent('focus'));
      fixture.detectChanges();
      bar.content()!.choose('background', document.createElement('button'));
      component.onColorPick('#fde2e4');
      fixture.detectChanges();
      expect(element.querySelector<HTMLElement>('tbody td')!.style.background).toBe('rgb(253, 226, 228)');
      // Текст клітинки — без тегу кольору.
      expect(cell.innerHTML).toBe('Фонд');
      await component.saver.flush();
      await settle();
      expect(update).toHaveBeenCalledWith('r1', { name: '<div style="background-color: #fde2e4">Фонд</div>' });
    });

    it('colours and formats a cell with a check box as a whole, and keeps that when it is switched', async () => {
      const update = vi.spyOn(TestBed.inject(CustomRecordsService), 'update').mockResolvedValue('done');
      const { element, fixture, component, settle } = await renderTable();
      const box = () => element.querySelector<HTMLInputElement>('tbody input.kb-cell--check')!;
      box().dispatchEvent(new FocusEvent('focus'));
      fixture.detectChanges();
      const bar = TestBed.inject(ActionBarService);
      bar.content()!.choose('background', document.createElement('button'));
      component.onColorPick('#fde2e4');
      bar.content()!.choose('bold', document.createElement('button'));
      fixture.detectChanges();
      expect(element.querySelectorAll<HTMLElement>('tbody td')[1].style.background).toBe('rgb(253, 226, 228)');

      box().checked = false;
      box().dispatchEvent(new Event('change'));
      await component.saver.flush();
      await settle();
      expect(update).toHaveBeenLastCalledWith('r1', { signed: '<div style="background-color: #fde2e4"><b>☐</b></div>' });
      expect(box().checked).toBe(false);
    });
  });

  describe('«Export» (Permissions.ViewCustomRecord)', () => {
    /** Файл, який віддали браузеру: назва й текст. */
    async function captureDownload(action: () => Promise<void>) {
      let blob: Blob | null = null;
      let fileName = '';
      // Тестові файли не ізольовані: після дії повертаємо `URL` як було.
      const real = { createObjectURL: URL.createObjectURL, revokeObjectURL: URL.revokeObjectURL };
      Object.defineProperty(URL, 'createObjectURL', { value: (value: Blob) => ((blob = value), 'blob:table'), configurable: true, writable: true });
      Object.defineProperty(URL, 'revokeObjectURL', { value: () => undefined, configurable: true, writable: true });
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
        fileName = this.download;
      });
      try {
        await action();
        // Адресу звільняють у `setTimeout` — даємо йому спрацювати з підробленою функцією.
        await new Promise((resolve) => setTimeout(resolve));
      } finally {
        Object.assign(URL, real);
      }
      const file = blob as Blob | null;
      if (!file) {
        return { fileName, text: null };
      }
      // У jsdom у `Blob` нема `text()`: читаємо байти й декодуємо, лишаючи BOM.
      const bytes = await new Promise<ArrayBuffer>((resolve) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result as ArrayBuffer);
        reader.readAsArrayBuffer(file);
      });
      // Zip без стиснення: XML усередині читається як текст.
      return { fileName, type: file.type, text: new TextDecoder().decode(new Uint8Array(bytes)) };
    }

    const exportOf = (fixture: { componentInstance: KbTableComponent }) => () => (fixture.componentInstance as unknown as { export(): Promise<void> }).export();

    it('downloads the whole table as the Excel file «<name>.xlsx», keeping the formatting', async () => {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue({
        ...table,
        fields: [{ ...table.fields[1], name: '<div style="background-color: #fde2e4"><b>Назва</b></div>' }, table.fields[0]],
      });
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([
        { id: 'r1', data: { name: '<b>Фонд</b>, «Надія»', signed: true } },
        { id: 'r2', data: { name: 'Програма<br>друга', signed: '☐' } },
      ]);
      const { fixture, settle } = await render('t1');
      await settle();
      const { fileName, type, text } = await captureDownload(exportOf(fixture));
      expect(fileName).toBe('Таблиця.xlsx');
      expect(type).toBe('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
      expect(text?.startsWith('PK')).toBe(true);
      expect(text).toContain('<sheet name="Таблиця"');
      expect(text).toContain('<fgColor rgb="FFFDE2E4"/>');
      for (const value of ['Назва', 'Підписано', 'Фонд', ', «Надія»', '☑', 'Програма\nдруга', '☐']) {
        expect(text).toContain(`<t xml:space="preserve">${value}</t>`);
      }
    });

    it('takes all the records again while a search is open, and says when they did not come', async () => {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
      const list = vi
        .spyOn(TestBed.inject(CustomRecordsService), 'listAll')
        .mockResolvedValueOnce([])
        .mockResolvedValueOnce([{ id: 'r1', data: { name: 'Фонд', signed: true } }])
        .mockResolvedValueOnce([{ id: 'r1', data: { name: 'Фонд', signed: true } }, { id: 'r2', data: { name: 'Програма', signed: false } }])
        .mockResolvedValueOnce('unreachable');
      const { fixture, settle, element } = await render('t1');
      await settle();
      fixture.debugElement.query(By.directive(SearchHeaderComponent)).componentInstance.searchChange.emit('Фонд');
      await vi.waitFor(() => expect(list).toHaveBeenCalledTimes(2));
      await settle();

      const { text } = await captureDownload(exportOf(fixture));
      expect(list).toHaveBeenLastCalledWith('t1', null, null);
      expect(text).toContain('<t xml:space="preserve">Програма</t>');

      await captureDownload(exportOf(fixture));
      fixture.detectChanges();
      const error = element.querySelector('.kb-table__error')!;
      expect(error.textContent).toContain(uk.knowledgeBase.table.exportError);
      expect(error.textContent).toContain(uk.knowledgeBase.failure.unreachable);
    });
  });

  describe('«Create template» (Permissions.CreateTableTemplate)', () => {
    async function renderWithTemplateDialog() {
      vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue({
        ...table,
        fields: [...table.fields, { id: 'amount', name: '<b>Сума</b>', key: 'amount', isRequired: true, order: 2 }],
      });
      vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll').mockResolvedValue([]);
      const rendered = await render('t1', ['Permissions.ViewCustomRecord', 'Permissions.CreateTableTemplate']);
      await rendered.settle();
      const button = rendered.fixture.debugElement
        .queryAll(By.directive(ActionButtonComponent))
        .find((candidate) => (candidate.componentInstance as ActionButtonComponent).label() === uk.knowledgeBase.table.createTemplate)!;
      button.triggerEventHandler('click');
      // Вікно відкривається з анімацією Ionic.
      await vi.waitFor(
        () => {
          rendered.fixture.detectChanges();
          expect(rendered.fixture.debugElement.query(By.directive(KbItemFormComponent))).not.toBeNull();
        },
        { timeout: 3000 },
      );
      const form = rendered.fixture.debugElement.query(By.directive(KbItemFormComponent)).componentInstance as KbItemFormComponent;
      return { ...rendered, form };
    }

    it('asks the name (the table name at first) and the description, creates the template from the headers and says so', async () => {
      const create = vi.spyOn(TestBed.inject(TableTemplatesService), 'create').mockResolvedValue({ id: 'tpl9' });
      const { form, fixture } = await renderWithTemplateDialog();
      expect(form.initial()).toEqual({ title: 'Таблиця', description: 'Опис таблиці' });

      form.submitted.emit({ title: 'Донори', description: null, type: 'customtable', templateId: null });
      await vi.waitFor(() => expect(TestBed.inject(SuccessNoticeService).key()).toBe('knowledgeBase.table.templateDialog.done'));
      // Назви заголовків — як є (з форматуванням), у порядку стовпчиків; тип і обовʼязковість не беремо.
      expect(create).toHaveBeenCalledWith({
        name: 'Донори',
        description: null,
        fields: [
          { name: 'Назва' },
          { name: 'Підписано' },
          { name: '<b>Сума</b>' },
        ],
      });
      fixture.detectChanges();
    });

    it('keeps the window open with the reason when the template was not created', async () => {
      vi.spyOn(TestBed.inject(TableTemplatesService), 'create').mockResolvedValue('invalid');
      const { form, fixture } = await renderWithTemplateDialog();
      form.submitted.emit({ title: 'Донори', description: null, type: 'customtable', templateId: null });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(form.error()).toBe('knowledgeBase.failure.invalid');
      });
      expect(TestBed.inject(SuccessNoticeService).key()).toBeNull();
    });
  });

  it('shows the blue «Updating data» tag while checking for changes', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    vi.spyOn(TestBed.inject(CustomTablesService), 'get').mockResolvedValue(table);
    let answer!: (records: never[]) => void;
    vi.spyOn(TestBed.inject(CustomRecordsService), 'listAll')
      .mockResolvedValueOnce([])
      .mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    const { element, fixture, settle } = await render('t1');
    await settle();
    vi.advanceTimersByTime(POLL_INTERVAL_MS);
    fixture.detectChanges();
    const tag = element.querySelector('.kb-table__saved')!;
    expect(tag.textContent?.trim()).toBe(uk.knowledgeBase.table.syncing);
    expect(tag.querySelector('ion-icon')?.getAttribute('name')).toBe('cloud-download-outline');
    answer([]);
    await settle();
    expect(element.querySelector('.kb-table__saved')?.textContent?.trim()).toBe(uk.knowledgeBase.table.saved);
    vi.useRealTimers();
  });
});
