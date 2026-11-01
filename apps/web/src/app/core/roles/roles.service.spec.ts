import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { Role, RolesService } from './roles.service';

describe('RolesService', () => {
  let http: HttpTestingController;
  let service: RolesService;
  let url: string;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    url = `${connection.serverUrl()}/api/v1/roles`;
    service = TestBed.inject(RolesService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const page = (pageNumber: number) => http.expectOne((request) => request.url === url && request.params.get('PageNumber') === String(pageNumber));
  const item = (id: string, name: string, extra: Record<string, unknown> = {}) => ({ id, name, feminitiveName: null, pluralName: null, ...extra });

  it('asks for the first page with the biggest page size the server takes (GET, no body)', async () => {
    const result = service.all();
    const request = page(1);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('PageSize')).toBe('100');
    request.flush({ items: [], hasNextPage: false });
    expect(await result).toEqual([]);
  });

  it('gives the roles with their feminitive and plural names, in the order of the server', async () => {
    const result = service.all();
    page(1).flush({
      items: [item('r1', 'Куратор', { feminitiveName: 'Кураторка', pluralName: 'Куратори' }), item('r2', 'Член')],
      hasNextPage: false,
    });
    expect(await result).toEqual<Role[]>([
      { id: 'r1', name: 'Куратор', feminitiveName: 'Кураторка', pluralName: 'Куратори' },
      { id: 'r2', name: 'Член', feminitiveName: null, pluralName: null },
    ]);
  });

  it('follows the next pages until the server says there is no more, and joins them', async () => {
    const result = service.all();
    page(1).flush({ items: [item('r1', 'Перша')], hasNextPage: true });
    await vi.waitFor(() => page(2).flush({ items: [item('r2', 'Друга')], hasNextPage: true }));
    await vi.waitFor(() => page(3).flush({ items: [item('r3', 'Третя')], hasNextPage: false }));
    expect((await result as Role[]).map((role) => role.id)).toEqual(['r1', 'r2', 'r3']);
    http.expectNone((request) => request.params.get('PageNumber') === '4');
  });

  it('skips the items without an id or a name and trims the names', async () => {
    const result = service.all();
    page(1).flush({
      items: [item('r1', '  Куратор  '), { name: 'Без id' }, item('r3', '   '), { id: 'r4' }, null, item('r5', 'Член', { feminitiveName: '  ', pluralName: 5 })],
      hasNextPage: false,
    });
    expect(await result).toEqual([
      { id: 'r1', name: 'Куратор', feminitiveName: null, pluralName: null },
      { id: 'r5', name: 'Член', feminitiveName: null, pluralName: null },
    ]);
  });

  it('is an error when the answer has no list of roles', async () => {
    const result = service.all();
    page(1).flush({ total: 1 });
    expect(await result).toBe('error');
  });

  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [404, 'error'],
    [500, 'error'],
  ])('maps HTTP %i to %s', async (status, expected) => {
    const result = service.all();
    page(1).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(expected);
  });

  it('reports an unreachable server', async () => {
    const result = service.all();
    page(1).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });

  it('fails as a whole when a later page fails (an incomplete list is not shown as a complete one)', async () => {
    const result = service.all();
    page(1).flush({ items: [item('r1', 'Перша')], hasNextPage: true });
    await vi.waitFor(() => page(2).flush({}, { status: 500, statusText: 'x' }));
    expect(await result).toBe('error');
  });

  it('stops after a limit of pages when the server keeps saying that there is one more', async () => {
    const result = service.all();
    for (let pageNumber = 1; pageNumber <= 50; pageNumber++) {
      await vi.waitFor(() => page(pageNumber).flush({ items: [item(`r${pageNumber}`, `Роль ${pageNumber}`)], hasNextPage: true }));
    }
    expect(await result).toHaveLength(50);
    http.expectNone((request) => request.params.get('PageNumber') === '51');
  });

  describe('one role (GET /api/v1/roles/{id})', () => {
    const roleUrl = (id: string) => `${url}/${id}`;

    it('asks for the role by its id and gives it with the names and the claims', async () => {
      const result = service.get('r1');
      const request = http.expectOne(roleUrl('r1'));
      expect(request.request.method).toBe('GET');
      request.flush(item('r1', ' Куратор ', { feminitiveName: 'Кураторка', pluralName: 'Куратори', claims: ['Permissions.ViewUser', 7, 'Permissions.ManageRole'] }));
      expect(await result).toEqual({
        id: 'r1',
        name: 'Куратор',
        feminitiveName: 'Кураторка',
        pluralName: 'Куратори',
        claims: ['Permissions.ViewUser', 'Permissions.ManageRole'],
      });
    });

    it('gives no claims when the answer has none, and null for the empty names', async () => {
      const result = service.get('r1');
      http.expectOne(roleUrl('r1')).flush({ id: 'r1', name: 'Член', feminitiveName: '  ', pluralName: null });
      expect(await result).toEqual({ id: 'r1', name: 'Член', feminitiveName: null, pluralName: null, claims: [] });
    });

    it('puts the id into the address safely', async () => {
      const result = service.get('a/b');
      http.expectOne(roleUrl('a%2Fb')).flush(item('a/b', 'Член'));
      expect((await result) as Role).toMatchObject({ id: 'a/b' });
    });

    it('is an error when the answer has no id or name', async () => {
      const result = service.get('r1');
      http.expectOne(roleUrl('r1')).flush({ id: 'r1' });
      expect(await result).toBe('error');
    });

    it.each([
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [404, 'error'],
      [500, 'error'],
    ])('reports HTTP %i as %s', async (status, failure) => {
      const result = service.get('r1');
      http.expectOne(roleUrl('r1')).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(failure);
    });

    it('reports an unreachable server', async () => {
      const result = service.get('r1');
      http.expectOne(roleUrl('r1')).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });
});
