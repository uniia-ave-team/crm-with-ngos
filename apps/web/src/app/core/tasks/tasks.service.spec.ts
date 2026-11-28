import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { NewTask, TasksService } from './tasks.service';

describe('TasksService', () => {
  const task: NewTask = {
    title: 'Афіша',
    description: null,
    status: 0,
    priority: 1,
    assigneeIds: ['u1'],
    tags: ['Дизайн'],
    startDate: '2026-12-01',
    endDate: '2026-12-05',
  };

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  afterEach(() => vi.restoreAllMocks());

  it('reads a page of the tasks of a column, skipping a broken one', async () => {
    const service = TestBed.inject(TasksService);
    const http = TestBed.inject(HttpTestingController);
    const reading = service.page('p1', 1, 20, { topicId: 't1' });
    const first = http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1/tasks'));
    expect(first.request.params.get('PageSize')).toBe('20');
    expect(first.request.params.get('TopicId')).toBe('t1');
    expect(first.request.params.get('Filter')).toBeNull();
    first.flush({
      items: [
        {
          id: 'a',
          topicId: 't1',
          title: 'Афіша',
          description: 'Макет',
          status: 1,
          priority: 2,
          startDate: '2026-03-20T00:00:00Z',
          endDate: null,
          unreadCommentsCount: 3,
          tags: [{ id: 'g1', title: 'Дизайн' }, { id: '', title: 'Без id' }],
          assignedUsers: [{ id: 'u1', firstName: 'Імʼя', lastName: 'Прізвище', patronymic: null, avatarUrl: null }, { id: '' }],
        },
        { id: 'b', topicId: 't1', title: 'Без стану', status: 9, priority: 1, startDate: '2026-03-20T00:00:00Z' },
      ],
      hasNextPage: true,
    });
    expect(await reading).toEqual({ hasNextPage: true, items: [
      {
        id: 'a',
        topicId: 't1',
        title: 'Афіша',
        description: 'Макет',
        status: 1,
        priority: 2,
        startDate: '2026-03-20',
        endDate: null,
        assignees: [{ id: 'u1', firstName: 'Імʼя', lastName: 'Прізвище', patronymic: null, avatarUrl: null }],
        tags: [{ id: 'g1', title: 'Дизайн' }],
        unreadComments: 3,
      },
    ] });
  });

  it('reads one task, the comments page by page, marks them read and adds one', async () => {
    const service = TestBed.inject(TasksService);
    const http = TestBed.inject(HttpTestingController);
    const getting = service.get('p1', 'a');
    http
      .expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a'))
      .flush({ id: 'a', topicId: 't1', title: 'Афіша', status: 0, priority: 1, startDate: '2026-03-20T00:00:00Z' });
    expect(await getting).toEqual(expect.objectContaining({ id: 'a', title: 'Афіша' }));

    const reading = service.comments('p1', 'a', 2, 20);
    const list = http.expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a/comments'));
    expect(list.request.params.get('PageNumber')).toBe('2');
    list.flush({
      items: [
        { id: 'c1', userId: 'u1', authorFirstName: 'Імʼя', authorLastName: 'Прізвище', authorAvatarUrl: null, content: 'Готово', createdAt: '2026-03-20T10:00:00Z' },
        { id: 'c2', content: 'Без часу' },
      ],
      hasNextPage: false,
    });
    expect(await reading).toEqual({
      items: [{ id: 'c1', userId: 'u1', firstName: 'Імʼя', lastName: 'Прізвище', avatarUrl: null, content: 'Готово', createdAt: '2026-03-20T10:00:00Z' }],
      hasNextPage: false,
    });

    const marking = service.markCommentsRead('p1', 'a');
    http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a/comments/read')).flush(null, { status: 204, statusText: 'No Content' });
    expect(await marking).toBe('done');
    expect(service.version()).toBe(1);

    const adding = service.addComment('p1', 'a', 'Привіт');
    const post = http.expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a/comments'));
    expect(post.request.body).toEqual({ content: 'Привіт' });
    post.flush('c3', { status: 201, statusText: 'Created' });
    expect(await adding).toEqual({ id: 'c3' });

    const updating = service.updateComment('p1', 'a', 'c3', 'Привіт!');
    const put = http.expectOne((candidate) => candidate.method === 'PUT' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a/comments/c3'));
    expect(put.request.body).toEqual({ content: 'Привіт!' });
    put.flush(null, { status: 204, statusText: 'No Content' });
    expect(await updating).toBe('done');

    const removing = service.removeComment('p1', 'a', 'c3');
    http.expectOne((candidate) => candidate.method === 'DELETE' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a/comments/c3')).flush(null, { status: 404, statusText: 'Not Found' });
    expect(await removing).toBe('done');
  });

  it('deletes a task (one already gone counts as done), and the lists ask again', async () => {
    const service = TestBed.inject(TasksService);
    const http = TestBed.inject(HttpTestingController);
    const removing = service.remove('p1', 'a');
    http.expectOne((candidate) => candidate.method === 'DELETE' && candidate.url.endsWith('/api/v1/projects/p1/tasks/a')).flush(null, { status: 204, statusText: 'No Content' });
    expect(await removing).toBe('done');
    expect(service.version()).toBe(1);
    const forbidden = service.remove('p1', 'a');
    http.expectOne((candidate) => candidate.method === 'DELETE').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(await forbidden).toBe('forbidden');
  });

  it('asks a page of own tasks with the sort and the search', async () => {
    const service = TestBed.inject(TasksService);
    const reading = service.page('p1', 2, 20, { mine: true, orderBy: 'Priority', sortOrder: 'desc', search: ' афіша ' });
    const request = TestBed.inject(HttpTestingController).expectOne((candidate) => candidate.method === 'GET' && candidate.url.endsWith('/api/v1/projects/p1/tasks'));
    expect(request.request.params.get('Filter')).toBe('mine');
    expect(request.request.params.get('PageNumber')).toBe('2');
    expect(request.request.params.get('PageSize')).toBe('20');
    expect(request.request.params.get('OrderBy')).toBe('Priority');
    expect(request.request.params.get('SortOrder')).toBe('desc');
    expect(request.request.params.get('SearchTerm')).toBe('афіша');
    request.flush({ items: [], hasNextPage: true });
    expect(await reading).toEqual({ items: [], hasNextPage: true });
  });

  it('creates a task in the column with the tags and the days as the start of the day in UTC and gives its id', async () => {
    const service = TestBed.inject(TasksService);
    const creating = service.create('p1', 't1', task, ['g1']);
    const post = TestBed.inject(HttpTestingController).expectOne((candidate) => candidate.method === 'POST' && candidate.url.endsWith('/api/v1/projects/p1/tasks'));
    expect(post.request.body).toEqual({
      topicId: 't1',
      title: 'Афіша',
      description: null,
      status: 0,
      priority: 1,
      assigneeIds: ['u1'],
      tagIds: ['g1'],
      startDate: '2026-12-01T00:00:00Z',
      endDate: '2026-12-05T00:00:00Z',
    });
    post.flush('task-1', { status: 201, statusText: 'Created' });
    expect(await creating).toEqual({ id: 'task-1' });
    expect(service.version()).toBe(1);
  });

  it('says gone when the project, the column or a colleague is no more (404), and gives other reasons', async () => {
    const service = TestBed.inject(TasksService);
    const http = TestBed.inject(HttpTestingController);
    const gone = service.create('p1', 't1', { ...task, endDate: null }, []);
    const first = http.expectOne((candidate) => candidate.method === 'POST');
    expect(first.request.body.endDate).toBeNull();
    first.flush(null, { status: 404, statusText: 'Not Found' });
    expect(await gone).toBe('gone');
    const invalid = service.create('p1', 't1', task, ['g1']);
    http.expectOne((candidate) => candidate.method === 'POST').flush(null, { status: 400, statusText: 'Bad Request' });
    expect(await invalid).toBe('invalid');
    expect(service.version()).toBe(0);
  });

  it('saves a task by PUT in its column with exactly the given tags, leaving the links and the metadata as they are', async () => {
    const service = TestBed.inject(TasksService);
    const http = TestBed.inject(HttpTestingController);
    const existing = { id: 'k1', topicId: 't1', title: 'Афіша', description: null, status: 0, priority: 1, startDate: '2026-12-01', endDate: null, assignees: [], tags: [], unreadComments: 0 } as const;
    const saving = service.update('p1', existing, task, ['g1']);
    const put = http.expectOne((candidate) => candidate.method === 'PUT' && candidate.url.endsWith('/api/v1/projects/p1/tasks/k1'));
    expect(put.request.body).toEqual({
      topicId: 't1',
      title: 'Афіша',
      description: null,
      status: 0,
      priority: 1,
      assigneeIds: ['u1'],
      tagIds: ['g1'],
      externalLinks: null,
      metadata: null,
      startDate: '2026-12-01T00:00:00Z',
      endDate: '2026-12-05T00:00:00Z',
    });
    put.flush(null, { status: 204, statusText: 'No Content' });
    expect(await saving).toBe('done');
    expect(service.version()).toBe(1);
    const gone = service.update('p1', existing, task, ['g1']);
    http.expectOne((candidate) => candidate.method === 'PUT').flush(null, { status: 404, statusText: 'Not Found' });
    expect(await gone).toBe('gone');
  });
});
