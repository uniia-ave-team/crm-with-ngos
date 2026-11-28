import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ProjectsService } from './projects.service';

describe('ProjectsService', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  afterEach(() => vi.restoreAllMocks());

  it('reads all the pages of 100, skips a project without an id or a name, and asks once while loading', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const loading = service.load();
    void service.load();
    const first = http.expectOne((request) => request.url.endsWith('/api/v1/projects'));
    expect(first.request.params.get('PageSize')).toBe('100');
    first.flush({ items: [{ id: 'p1', title: 'Явір' }, { id: '', title: 'Без id' }, { id: 'p2', title: ' ' }], hasNextPage: true });
    await new Promise((resolve) => setTimeout(resolve));
    const second = http.expectOne((request) => request.params.get('PageNumber') === '2');
    second.flush({ items: [{ id: 'p3', title: 'Унія' }], hasNextPage: false });
    await loading;
    expect(service.state()).toEqual({
      status: 'ready',
      projects: [
        { id: 'p1', title: 'Явір', visibility: null },
        { id: 'p3', title: 'Унія', visibility: null },
      ],
    });
    // Уже є — заново не запитує.
    await service.load();
    http.expectNone((request) => request.url.endsWith('/api/v1/projects'));
  });

  it('creates a project with the days as the start of the day in UTC, then loads the list again', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const creating = service.create({ title: 'Явір', description: null, visibility: 1, startDate: '2026-03-20', endDate: null });
    const request = http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects'));
    expect(request.request.body).toEqual({ title: 'Явір', description: null, visibility: 1, startDate: '2026-03-20T00:00:00Z', endDate: null });
    request.flush('p1');
    expect(await creating).toEqual({ id: 'p1' });
    http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects')).flush({ items: [{ id: 'p1', title: 'Явір' }], hasNextPage: false });
    await vi.waitFor(() => expect(service.state().projects).toEqual([{ id: 'p1', title: 'Явір', visibility: null }]));
  });

  it('gives the reason when the server did not create the project', async () => {
    const creating = TestBed.inject(ProjectsService).create({ title: 'Явір', description: null, visibility: 0, startDate: '2026-03-20', endDate: '2026-03-19' });
    TestBed.inject(HttpTestingController)
      .expectOne((candidate) => candidate.method === 'POST')
      .flush(null, { status: 400, statusText: 'Bad Request' });
    expect(await creating).toBe('invalid');
  });

  it('reads a project for the form with the days in UTC, and says gone (loading the list again) when it was deleted', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const reading = service.get('p1');
    http
      .expectOne((candidate) => candidate.url.endsWith('/api/v1/projects/p1'))
      .flush({ id: 'p1', title: 'Явір', description: 'Опис', visibility: 2, startDate: '2026-03-20T00:00:00Z', endDate: '2026-03-25T00:00:00Z' });
    expect(await reading).toEqual({ title: 'Явір', description: 'Опис', visibility: 2, startDate: '2026-03-20', endDate: '2026-03-25' });

    const gone = service.get('p2');
    http.expectOne((candidate) => candidate.url.endsWith('/api/v1/projects/p2')).flush(null, { status: 404, statusText: 'Not Found' });
    expect(await gone).toBe('gone');
    http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects'));
  });

  it('saves a project by PUT with the days as the start of the day in UTC, then loads the list again', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const saving = service.update('p1', { title: 'Явір', description: null, visibility: 1, startDate: '2026-03-20', endDate: '2026-03-25' });
    const request = http.expectOne((candidate) => candidate.method === 'PUT' && candidate.url.endsWith('/api/v1/projects/p1'));
    expect(request.request.body).toEqual({
      title: 'Явір',
      description: null,
      visibility: 1,
      startDate: '2026-03-20T00:00:00Z',
      endDate: '2026-03-25T00:00:00Z',
    });
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(await saving).toBe('done');
    http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects'));
  });

  it('deletes a project (one already deleted counts as done), then loads the list again; other failures give the reason', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const list = () => http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects'));
    const deleting = service.remove('p1');
    http.expectOne((candidate) => candidate.method === 'DELETE' && candidate.url.endsWith('/api/v1/projects/p1')).flush(null, { status: 204, statusText: 'No Content' });
    expect(await deleting).toBe('done');
    list().flush({ items: [], hasNextPage: false });

    const again = service.remove('p1');
    http.expectOne((candidate) => candidate.method === 'DELETE').flush(null, { status: 404, statusText: 'Not Found' });
    expect(await again).toBe('done');
    list().flush({ items: [], hasNextPage: false });

    const forbidden = service.remove('p2');
    http.expectOne((candidate) => candidate.method === 'DELETE').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(await forbidden).toBe('forbidden');
  });

  it('reads the members of a project, skipping a record without a user', async () => {
    const reading = TestBed.inject(ProjectsService).members('p1');
    TestBed.inject(HttpTestingController)
      .expectOne((candidate) => candidate.url.endsWith('/api/v1/projects/p1/members'))
      .flush([
        { projectId: 'p1', userId: 'u1', firstName: 'А', lastName: 'Н', patronymic: null, avatarUrl: null, roleTitle: 'Керівник', claim: 2, isActive: true },
        { projectId: 'p1', userId: '', firstName: 'Без', lastName: 'id' },
        { projectId: 'p1', userId: 'u2', firstName: 'С', lastName: 'Ф', avatarUrl: '/files/a.png', roleTitle: null, claim: 0, isActive: false },
      ]);
    expect(await reading).toEqual([
      { userId: 'u1', firstName: 'А', lastName: 'Н', patronymic: null, avatarUrl: null, roleTitle: 'Керівник', claim: 2, isActive: true },
      { userId: 'u2', firstName: 'С', lastName: 'Ф', patronymic: null, avatarUrl: '/files/a.png', roleTitle: null, claim: 0, isActive: false },
    ]);
  });

  it('changes the level of a member keeping the role title, and removes a member', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const member = { userId: 'u1', firstName: 'А', lastName: 'Н', patronymic: null, avatarUrl: null, roleTitle: 'Дизайнер', claim: 0 as const, isActive: true };
    const changing = service.updateMember('p1', member, 1);
    const put = http.expectOne((candidate) => candidate.method === 'PUT' && candidate.url.endsWith('/api/v1/projects/p1/members/u1'));
    expect(put.request.body).toEqual({ roleTitle: 'Дизайнер', claim: 1 });
    put.flush(null, { status: 204, statusText: 'No Content' });
    expect(await changing).toBe('done');

    const removing = service.removeMember('p1', 'u1');
    http.expectOne((candidate) => candidate.method === 'DELETE' && candidate.url.endsWith('/api/v1/projects/p1/members/u1')).flush(null, { status: 400, statusText: 'Bad Request' });
    expect(await removing).toBe('invalid');
  });

  it('adds a member with the level and the role title; a colleague already in the project is «exists»', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const adding = service.addMember('p1', 'u1', 2, 'Дизайнер');
    const post = http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects/p1/members'));
    expect(post.request.body).toEqual({ userId: 'u1', roleTitle: 'Дизайнер', claim: 2 });
    post.flush(null, { status: 201, statusText: 'Created' });
    expect(await adding).toBe('done');
    expect(service.membersVersion()).toBe(1);

    const again = service.addMember('p1', 'u1', 0);
    http.expectOne((candidate) => candidate.method === 'POST').flush(null, { status: 409, statusText: 'Conflict' });
    expect(await again).toBe('exists');
  });

  it('creates a column at the end of the board: its order is the number of the columns there are', async () => {
    const service = TestBed.inject(ProjectsService);
    const http = TestBed.inject(HttpTestingController);
    const creating = service.createTopic('p1', 'До виконання');
    const list = await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1/topics')));
    expect(list.request.params.get('PageSize')).toBe('1');
    list.flush({ items: [{ id: 't1', projectId: 'p1', title: 'Інша', order: 0 }], totalCount: 2, hasNextPage: true });
    const post = await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects/p1/topics')));
    expect(post.request.body).toEqual({ title: 'До виконання', order: 2 });
    post.flush('t3', { status: 201, statusText: 'Created' });
    expect(await creating).toBe('done');
    expect(service.topicsVersion()).toBe(1);
  });

  it('says a column with that title exists (409)', async () => {
    const http = TestBed.inject(HttpTestingController);
    const creating = TestBed.inject(ProjectsService).createTopic('p1', 'До виконання');
    (await vi.waitFor(() => http.expectOne((candidate) => candidate.url.endsWith('/api/v1/projects/p1/topics') && candidate.method === 'GET'))).flush({
      items: [],
      totalCount: 0,
    });
    (await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'POST'))).flush(null, { status: 409, statusText: 'Conflict' });
    expect(await creating).toBe('exists');
  });

  describe('editing a column', () => {
    const project = { id: 'p1', title: 'Явір', topics: [{ id: 't1', projectId: 'p1', title: 'Розробка', order: 1 }] };

    async function edit(title = 'Дизайн') {
      const service = TestBed.inject(ProjectsService);
      const http = TestBed.inject(HttpTestingController);
      const saving = service.updateTopic('p1', 't1', title);
      http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1')).flush(project);
      return { service, http, saving };
    }

    it('sends the new title with the position the column has now, and the board asks the columns again', async () => {
      const { service, http, saving } = await edit();
      const put = await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'PUT' && candidate.url.endsWith('/api/v1/projects/p1/topics/t1')));
      expect(put.request.body).toEqual({ title: 'Дизайн', order: 1 });
      put.flush(null, { status: 204, statusText: 'No Content' });
      expect(await saving).toBe('done');
      expect(service.topicsVersion()).toBe(1);
    });

    it('says a column with that title exists (409)', async () => {
      const { http, saving } = await edit();
      (await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'PUT'))).flush(null, { status: 409, statusText: 'Conflict' });
      expect(await saving).toBe('exists');
    });

    it('says the column is gone when the project has it no more, without sending', async () => {
      const service = TestBed.inject(ProjectsService);
      const http = TestBed.inject(HttpTestingController);
      const saving = service.updateTopic('p1', 't2', 'Дизайн');
      http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1')).flush(project);
      expect(await saving).toBe('topicGone');
      http.expectNone((candidate) => candidate.method === 'PUT');
      expect(service.topicsVersion()).toBe(1);
    });
  });

  describe('deleting a column', () => {
    async function remove(status: number) {
      const service = TestBed.inject(ProjectsService);
      const removing = service.removeTopic('p1', 't1');
      const request = TestBed.inject(HttpTestingController).expectOne(
        (candidate) => candidate.method === 'DELETE' && candidate.url.endsWith('/api/v1/projects/p1/topics/t1'),
      );
      request.flush(null, { status, statusText: String(status) });
      return { service, result: await removing };
    }

    it('deletes it and the board asks the columns again; a column already gone is deleted too', async () => {
      for (const status of [204, 404]) {
        TestBed.resetTestingModule();
        TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
        const { service, result } = await remove(status);
        expect(result).toBe('done');
        expect(service.topicsVersion()).toBe(1);
      }
    });

    it('says the column has tasks when the server refuses it (400)', async () => {
      const { service, result } = await remove(400);
      expect(result).toBe('hasTasks');
      expect(service.topicsVersion()).toBe(0);
    });
  });

  describe('tags of a task', () => {
    const project = (tags: { id: string; title: string }[]) => ({ id: 'p1', title: 'Явір', tags });

    it('takes the ids of the existing tags whatever the case and creates the new ones', async () => {
      const http = TestBed.inject(HttpTestingController);
      const resolving = TestBed.inject(ProjectsService).tagIds('p1', ['дизайн', ' Друк ']);
      http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1')).flush(project([{ id: 'g1', title: 'Дизайн' }]));
      const post = await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects/p1/tags')));
      expect(post.request.body).toEqual({ title: 'Друк' });
      post.flush('g2', { status: 201, statusText: 'Created' });
      expect(await resolving).toEqual(['g1', 'g2']);
    });

    it('takes a tag someone else has just created (409), and gives the reason of a failure', async () => {
      const http = TestBed.inject(HttpTestingController);
      const service = TestBed.inject(ProjectsService);
      const resolving = service.tagIds('p1', ['Друк']);
      http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1')).flush(project([]));
      (await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'POST'))).flush(null, { status: 409, statusText: 'Conflict' });
      (await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1')))).flush(
        project([{ id: 'g2', title: 'друк' }]),
      );
      expect(await resolving).toEqual(['g2']);

      const forbidden = service.tagIds('p1', ['Новий']);
      http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1')).flush(project([]));
      (await vi.waitFor(() => http.expectOne((candidate) => candidate.method === 'POST'))).flush(null, { status: 403, statusText: 'Forbidden' });
      expect(await forbidden).toBe('forbidden');
      expect(await service.tagIds('p1', [])).toEqual([]);
    });
  });

  it('takes the columns of the board from the project, in their order, skipping a broken one', async () => {
    const reading = TestBed.inject(ProjectsService).topics('p1');
    TestBed.inject(HttpTestingController)
      .expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1'))
      .flush({
        id: 'p1',
        title: 'Явір',
        topics: [
          { id: 't1', projectId: 'p1', title: 'Розробка', order: 0 },
          { id: '', title: 'Без id', order: 1 },
          { id: 't2', projectId: 'p1', title: 'SMM', order: 1 },
        ],
      });
    expect(await reading).toEqual([
      { id: 't1', title: 'Розробка', order: 0 },
      { id: 't2', title: 'SMM', order: 1 },
    ]);
  });
});
