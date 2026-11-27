import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { KNOWLEDGE_BASE_HOME } from './knowledge-base';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import {
  EMPTY_TABLE_FIELD_KEY,
  KNOWLEDGE_BASE_SAMPLE_URL,
  KNOWLEDGE_BASE_SOURCE,
  KNOWLEDGE_BASE_TREE_PATH,
  KnowledgeBaseSource,
  KnowledgeBaseService,
} from './knowledge-base.service';

describe('KnowledgeBaseService', () => {
  let http: HttpTestingController;

  function setUp(source: KnowledgeBaseSource = 'sample') {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        { provide: KNOWLEDGE_BASE_SOURCE, useValue: source },
      ],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    http.match(() => true);
    return TestBed.inject(KnowledgeBaseService);
  }

  afterEach(() => vi.restoreAllMocks());

  it('loads the sample contents from the sample source, once for everyone who asks, puts the home page first and keeps it', async () => {
    const service = setUp();
    expect(service.state().status).toBe('loading');
    const first = service.load();
    const second = service.load();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush([{ id: 't1', type: 'customtable', title: 'Таблиця' }]);
    await Promise.all([first, second]);
    expect(service.state()).toEqual({ status: 'ready', entries: [KNOWLEDGE_BASE_HOME, { id: 't1', type: 'customtable', title: 'Таблиця' }] });

    await service.load();
    http.expectNone(KNOWLEDGE_BASE_SAMPLE_URL);
  });

  it('forgets the contents of the previous organisation when another account becomes active, and loads them again', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        { provide: KNOWLEDGE_BASE_SOURCE, useValue: 'sample' },
      ],
    });
    const session = signal<string | null>('a');
    (TestBed.inject(AuthService) as { sessionId: Signal<string | null> }).sessionId = session;
    http = TestBed.inject(HttpTestingController);
    http.match(() => true);
    const service = TestBed.inject(KnowledgeBaseService);
    const first = service.load();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush([{ id: 't1', type: 'customtable', title: 'Таблиця першої' }]);
    await first;
    expect(service.state().status).toBe('ready');

    session.set('b');
    TestBed.tick();
    expect(service.state()).toEqual({ status: 'loading', entries: [] });
    const second = service.load();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush([{ id: 't2', type: 'customtable', title: 'Таблиця другої' }]);
    await second;
    expect(service.state().entries.map((entry) => entry.title)).toContain('Таблиця другої');
    expect(service.state().entries.map((entry) => entry.title)).not.toContain('Таблиця першої');
  });

  it('asks the tree of the server of the active account when the source is the general request', async () => {
    const service = setUp('tree');
    const done = service.load();
    const request = http.expectOne(`${TestBed.inject(ConnectionService).serverUrl()}${KNOWLEDGE_BASE_TREE_PATH}`);
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'f1', type: 'folder', title: 'Розділ', items: [] }]);
    await done;
    expect(service.state().entries).toEqual([KNOWLEDGE_BASE_HOME, { id: 'f1', type: 'folder', title: 'Розділ', items: [] }]);
    http.expectNone(KNOWLEDGE_BASE_SAMPLE_URL);
  });

  it('says it failed and asks again on the next load', async () => {
    const service = setUp();
    const first = service.load();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush({}, { status: 404, statusText: 'x' });
    await first;
    expect(service.state()).toEqual({ status: 'failed', entries: [] });

    const again = service.load();
    expect(service.state().status).toBe('loading');
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush([]);
    await again;
    expect(service.state()).toEqual({ status: 'ready', entries: [KNOWLEDGE_BASE_HOME] });
  });

  it('remembers where the last «+» asked to add a material', () => {
    const service = setUp();
    expect(service.insertRequest()).toBeNull();
    service.requestInsert({ folder: { id: 'f1', title: 'Розділ' } });
    expect(service.insertRequest()).toEqual({ folder: { id: 'f1', title: 'Розділ' } });
    service.requestInsert({ folder: null });
    expect(service.insertRequest()).toEqual({ folder: null });
  });
  describe('changes the contents in memory', () => {
    const contents = [
      { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
      { id: 'f2', type: 'folder', title: 'Інший розділ', items: [] },
      { id: 't1', type: 'customtable', title: 'Таблиця' },
    ];

    async function ready() {
      const service = setUp();
      const done = service.load();
      http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush(contents);
      await done;
      return { service, entries: () => service.state().entries.slice(1) };
    }

    it('lists the folders and says where a material is', async () => {
      const { service } = await ready();
      expect(service.folders().map((folder) => folder.id)).toEqual(['f1', 'f2']);
      expect(service.folderOf('p1')?.id).toBe('f1');
      expect(service.folderOf('t1')).toBeNull();
    });

    it('renames a material in a folder, a material at the top and a folder', async () => {
      const { service, entries } = await ready();
      service.rename('p1', 'Новий документ');
      service.rename('t1', 'Нова таблиця');
      service.rename('f2', 'Новий розділ');
      expect(entries()).toEqual([
        { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Новий документ' }] },
        { id: 'f2', type: 'folder', title: 'Новий розділ', items: [] },
        { id: 't1', type: 'customtable', title: 'Нова таблиця' },
      ]);
    });

    it('removes a material, and a folder with all its contents', async () => {
      const { service, entries } = await ready();
      service.remove('t1');
      expect(entries().map((entry) => entry.id)).toEqual(['f1', 'f2']);
      service.remove('f1');
      expect(entries().map((entry) => entry.id)).toEqual(['f2']);
      expect(service.folderOf('p1')).toBeNull();
    });

    it('moves a material to the end of another folder or out of any folder', async () => {
      const { service, entries } = await ready();
      service.move('t1', 'f2');
      expect(service.folderOf('t1')?.id).toBe('f2');
      expect(entries().map((entry) => entry.id)).toEqual(['f1', 'f2']);

      service.move('p1', null);
      expect(entries().map((entry) => entry.id)).toEqual(['f1', 'f2', 'p1']);
      expect(service.folderOf('p1')).toBeNull();
    });
  });
  describe('tables of the organisation (the source for now)', () => {
    const server = () => TestBed.inject(ConnectionService).serverUrl();
    const tablesUrl = () => `${server()}/api/v1/customtables`;
    const table = { id: 't1', type: 'customtable', title: 'Таблиця' } as const;

    /** Сервіс з активним акаунтом, у якого права `permissions`, і завантаженим змістом `tables`. */
    async function ready(permissions: string[] = [], tables: object[] = [{ id: 't1', name: 'Таблиця', description: null }]) {
      const service = setUp('customtables');
      const accounts = TestBed.inject(AccountsService);
      accounts.add({ server: server(), accessToken: 'a', refreshToken: 'r', identity: 'u1' });
      accounts.update(accounts.activeId()!, { permissions });
      http.match(() => true);
      const done = service.load();
      answerList(tables);
      await done;
      return service;
    }

    function answerList(tables: object[], hasNextPage = false) {
      const request = http.expectOne((candidate) => candidate.url === tablesUrl() && candidate.method === 'GET');
      request.flush({ items: tables, hasNextPage, pageNumber: 1, pageSize: 100, totalCount: tables.length });
      return request;
    }

    it('builds the contents from the tables by name, page after page, the home page first', async () => {
      const service = setUp('customtables');
      const done = service.load();
      const first = answerList([{ id: 't1', name: 'Перша' }, { id: '', name: 'Без id' }], true);
      expect(first.request.params.get('PageSize')).toBe('100');
      expect(first.request.params.get('OrderBy')).toBe('Name');
      await vi.waitFor(() => answerList([{ id: 't2', name: 'Друга', description: 'Опис' }]));
      await done;
      expect(service.state()).toEqual({
        status: 'ready',
        entries: [KNOWLEDGE_BASE_HOME, { id: 't1', type: 'customtable', title: 'Перша' }, { id: 't2', type: 'customtable', title: 'Друга' }],
      });
    });

    it('allows the actions with tables by the rights for tables, «Move» never (it is planned), nothing with the home page', async () => {
      const service = await ready(['Permissions.UpdateCustomTable']);
      expect(service.can('edit', table)).toBe(true);
      expect(service.can('delete', table)).toBe(false);
      expect(service.can('move', table)).toBe(false);
      expect(service.can('edit', KNOWLEDGE_BASE_HOME)).toBe(false);
      expect(service.canCreate()).toBe(false);
      expect(service.hasActions(table)).toBe(true);
    });

    it('allows nothing with pages and folders yet (there are no rights for them)', async () => {
      const service = await ready(['Permissions.CreateCustomTable', 'Permissions.UpdateCustomTable', 'Permissions.DeleteCustomTable']);
      const page = { id: 'p1', type: 'page', title: 'Документ' } as const;
      expect(service.hasActions(page)).toBe(false);
      expect(service.can('edit', { id: 'f1', type: 'folder', title: 'Розділ', items: [] })).toBe(false);
      // Можна створити таблицю — тож «+» є; додати в папку теж можна.
      expect(service.canCreate()).toBe(true);
      expect(service.creatableTypes()).toEqual(['customtable']);
      expect(service.can('add', { id: 'f1', type: 'folder', title: 'Розділ', items: [] })).toBe(true);
    });

    it('creates a table from a template and loads the contents again', async () => {
      const service = await ready();
      const done = service.create({ type: 'customtable', title: 'Донори', description: null, templateId: 'tpl1', folderId: null });
      const request = http.expectOne(`${tablesUrl()}/from-template`);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ sourceTemplateId: 'tpl1', name: 'Донори', description: null });
      request.flush('t2');
      await vi.waitFor(() => answerList([{ id: 't2', name: 'Донори' }]));
      expect(await done).toBe('done');
      expect(service.state().entries.map((entry) => entry.id)).toEqual(['home', 't2']);
    });

    it('creates a table without a template with one text column (the server asks for at least one)', async () => {
      const service = await ready();
      const done = service.create({ type: 'customtable', title: 'Нотатки', description: 'Опис', templateId: null, folderId: null });
      const request = http.expectOne((candidate) => candidate.url === tablesUrl() && candidate.method === 'POST');
      expect(request.request.body).toEqual({
        name: 'Нотатки',
        description: 'Опис',
        fields: [{ name: uk.knowledgeBase.dialogs.add.emptyTableField, key: EMPTY_TABLE_FIELD_KEY, dataType: 0, isRequired: false, order: 0 }],
      });
      request.flush('t2');
      await vi.waitFor(() => answerList([]));
      expect(await done).toBe('done');
    });

    it('says why a table was not created and keeps the contents', async () => {
      const service = await ready();
      const done = service.create({ type: 'customtable', title: 'Нотатки', description: null, templateId: null, folderId: null });
      http.expectOne((candidate) => candidate.url === tablesUrl() && candidate.method === 'POST').flush({}, { status: 403, statusText: 'x' });
      expect(await done).toBe('forbidden');
      http.expectNone((candidate) => candidate.method === 'GET');
    });

    it('does not create pages and folders yet', async () => {
      const service = await ready();
      for (const type of ['page', 'folder'] as const) {
        expect(await service.create({ type, title: 'Х', description: null, templateId: null, folderId: null })).toBe('unsupported');
      }
      http.expectNone(() => true);
    });

    it('opens a table for editing and saves the new name and description with the fresh columns (keeping one added meanwhile)', async () => {
      const service = await ready();
      const opening = service.openForEdit(table);
      const fields = [
        { id: 'c1', name: 'Назва', key: 'name', dataType: 0, isRequired: true, order: 0 },
        { id: 'c2', name: 'Терміни', key: 'terms', dataType: 3, isRequired: false, order: 1 },
      ];
      http.expectOne(`${tablesUrl()}/t1`).flush({ id: 't1', ngoId: 'n1', name: 'Таблиця', description: 'Старий опис', sourceTemplateId: null, fields });
      const editing = await opening;
      if (typeof editing === 'string') {
        throw new Error(editing);
      }
      expect([editing.title, editing.description]).toEqual(['Таблиця', 'Старий опис']);

      const saving = service.saveEdit(editing, { title: 'Нова назва', description: null });
      // Перед збереженням — свіжа структура: тим часом хтось додав «Email», його не можна видалити.
      const withEmail = [...fields, { id: 'c3', name: 'Email', key: 'email', dataType: 0, isRequired: false, order: 2 }];
      http.expectOne({ method: 'GET', url: `${tablesUrl()}/t1` }).flush({ id: 't1', ngoId: 'n1', name: 'Таблиця', description: 'Старий опис', sourceTemplateId: null, fields: withEmail });
      await new Promise((resolve) => setTimeout(resolve));
      const request = http.expectOne({ method: 'PUT', url: `${tablesUrl()}/t1` });
      expect(request.request.body).toEqual({ name: 'Нова назва', description: null, fields: withEmail });
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await saving).toBe('done');
      expect(service.state().entries[1]).toEqual({ ...table, title: 'Нова назва' });
    });

    it('does not open a table for editing whose columns came in an unknown form', async () => {
      const service = await ready();
      const opening = service.openForEdit(table);
      http.expectOne(`${tablesUrl()}/t1`).flush({ id: 't1', name: 'Таблиця', fields: [{ id: 'c1', name: 'Назва', key: 'name', dataType: 'Text' }] });
      expect(await opening).toBe('error');
    });

    it('deletes a table and takes it out of the contents', async () => {
      const service = await ready();
      const deleting = service.delete(table);
      const request = http.expectOne(`${tablesUrl()}/t1`);
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await deleting).toBe('done');
      expect(service.state().entries).toEqual([KNOWLEDGE_BASE_HOME]);
    });
  });
});
