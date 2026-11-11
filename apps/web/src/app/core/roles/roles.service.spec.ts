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

  describe('create (POST /roles, then POST /roles/{id}/claims/{claim} for every permission)', () => {
    const draft = { name: 'Куратор', feminitiveName: null, pluralName: 'Куратори' };
    const claimUrl = (id: string, claim: string) => `${url}/${id}/claims/${claim}`;
    const created = (id = 'r1') => http.expectOne((request) => request.url === url && request.method === 'POST').flush({ id }, { status: 201, statusText: 'Created' });
    const nextClaim = (id: string, claim: string) => vi.waitFor(() => http.expectOne(claimUrl(id, claim)));

    it('posts the draft as the body to /roles', async () => {
      const result = service.create(draft, []);
      const request = http.expectOne((candidate) => candidate.url === url && candidate.method === 'POST');
      expect(request.request.body).toEqual(draft);
      request.flush({ id: 'r1' }, { status: 201, statusText: 'Created' });
      expect(await result).toEqual({ id: 'r1', failedClaims: [] });
    });

    it('takes the id from the answer given as a bare string too', async () => {
      const result = service.create(draft, []);
      http.expectOne((candidate) => candidate.url === url && candidate.method === 'POST').flush('r7', { status: 201, statusText: 'Created' });
      expect(await result).toEqual({ id: 'r7', failedClaims: [] });
    });

    it('adds every permission by its own request without a body, one after another, and tells that all were added', async () => {
      const result = service.create(draft, ['Permissions.ViewUser', 'Permissions.ViewNgo']);
      created();

      const first = await nextClaim('r1', 'Permissions.ViewUser');
      expect(first.request.method).toBe('POST');
      expect(first.request.body).toBeNull();
      // Пока перше право не додане, друге не питаємо: вони змінюють ту саму роль.
      http.expectNone(claimUrl('r1', 'Permissions.ViewNgo'));
      first.flush(null, { status: 204, statusText: 'No Content' });

      (await nextClaim('r1', 'Permissions.ViewNgo')).flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toEqual({ id: 'r1', failedClaims: [] });
    });

    it('puts the id of the role into the address safely', async () => {
      const result = service.create(draft, ['Permissions.ViewUser']);
      created('a/b');
      (await vi.waitFor(() => http.expectOne(`${url}/a%2Fb/claims/Permissions.ViewUser`))).flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toEqual({ id: 'a/b', failedClaims: [] });
    });

    it('does not ask for any permission when the role is not created, and gives the reason', async () => {
      const result = service.create(draft, ['Permissions.ViewUser']);
      http.expectOne((candidate) => candidate.url === url && candidate.method === 'POST').flush({}, { status: 403, statusText: 'Forbidden' });
      expect(await result).toBe('forbidden');
      http.expectNone((candidate) => candidate.url.includes('/claims/'));
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [500, 'error'],
    ])('reports HTTP %i of the creating as %s', async (status, failure) => {
      const result = service.create(draft, []);
      http.expectOne((candidate) => candidate.url === url && candidate.method === 'POST').flush({}, { status, statusText: 'x' });
      expect(await result).toBe(failure);
    });

    it('reports an unreachable server', async () => {
      const result = service.create(draft, []);
      http.expectOne((candidate) => candidate.url === url && candidate.method === 'POST').error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });

    it('is an error when the answer has no id', async () => {
      const result = service.create(draft, ['Permissions.ViewUser']);
      http.expectOne((candidate) => candidate.url === url && candidate.method === 'POST').flush({}, { status: 201, statusText: 'Created' });
      expect(await result).toBe('error');
      http.expectNone((candidate) => candidate.url.includes('/claims/'));
    });

    it('goes on with the other permissions when one of them fails, and lists the failed ones', async () => {
      const result = service.create(draft, ['Permissions.A', 'Permissions.B', 'Permissions.C']);
      created();
      (await nextClaim('r1', 'Permissions.A')).flush({}, { status: 400, statusText: 'Bad Request' });
      (await nextClaim('r1', 'Permissions.B')).flush(null, { status: 204, statusText: 'No Content' });
      (await nextClaim('r1', 'Permissions.C')).error(new ProgressEvent('error'));
      expect(await result).toEqual({ id: 'r1', failedClaims: ['Permissions.A', 'Permissions.C'] });
    });
  });

  describe('update (PUT /roles/{id}, then POST and DELETE /roles/{id}/claims/{claim})', () => {
    const draft = { name: 'Куратор', feminitiveName: 'Кураторка', pluralName: null };
    const roleUrl = (id: string) => `${url}/${id}`;
    const claimUrl = (id: string, claim: string) => `${url}/${id}/claims/${claim}`;
    const noContent = { status: 204, statusText: 'No Content' };
    const next = (urlText: string) => vi.waitFor(() => http.expectOne(urlText));

    it('puts the new names in the body of the request, as newName, feminitiveName and pluralName', async () => {
      const result = service.update('r1', draft, { add: [], remove: [] });
      const request = http.expectOne(roleUrl('r1'));
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ newName: 'Куратор', feminitiveName: 'Кураторка', pluralName: null });
      request.flush(null, noContent);
      expect(await result).toEqual({ failedClaims: [] });
    });

    it('adds the permissions to add by POST and takes away the ones to remove by DELETE, one after another, after the names', async () => {
      const result = service.update('r1', draft, { add: ['Permissions.ViewUser'], remove: ['Permissions.CanResetPassword'] });
      http.expectOne(roleUrl('r1')).flush(null, noContent);

      const added = await next(claimUrl('r1', 'Permissions.ViewUser'));
      expect(added.request.method).toBe('POST');
      expect(added.request.body).toBeNull();
      // Пока додавання не завершилось, прибирання не починається.
      http.expectNone(claimUrl('r1', 'Permissions.CanResetPassword'));
      added.flush(null, noContent);

      const removed = await next(claimUrl('r1', 'Permissions.CanResetPassword'));
      expect(removed.request.method).toBe('DELETE');
      removed.flush(null, noContent);
      expect(await result).toEqual({ failedClaims: [] });
    });

    it('does not touch the permissions when the names are not saved, and gives the reason', async () => {
      const result = service.update('r1', draft, { add: ['Permissions.ViewUser'], remove: ['Permissions.CanResetPassword'] });
      http.expectOne(roleUrl('r1')).flush({}, { status: 403, statusText: 'Forbidden' });
      expect(await result).toBe('forbidden');
      http.expectNone((request) => request.url.includes('/claims/'));
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [500, 'error'],
    ])('reports HTTP %i of the saving of the names as %s', async (status, failure) => {
      const result = service.update('r1', draft, { add: [], remove: [] });
      http.expectOne(roleUrl('r1')).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(failure);
    });

    it('reports an unreachable server', async () => {
      const result = service.update('r1', draft, { add: [], remove: [] });
      http.expectOne(roleUrl('r1')).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });

    it('goes on after a failed permission and lists the failed ones, those to add and those to remove', async () => {
      const result = service.update('r1', draft, { add: ['Permissions.A', 'Permissions.B'], remove: ['Permissions.C', 'Permissions.D'] });
      http.expectOne(roleUrl('r1')).flush(null, noContent);
      (await next(claimUrl('r1', 'Permissions.A'))).flush({}, { status: 400, statusText: 'Bad Request' });
      (await next(claimUrl('r1', 'Permissions.B'))).flush(null, noContent);
      (await next(claimUrl('r1', 'Permissions.C'))).error(new ProgressEvent('error'));
      (await next(claimUrl('r1', 'Permissions.D'))).flush(null, noContent);
      expect(await result).toEqual({ failedClaims: ['Permissions.A', 'Permissions.C'] });
    });

    it('puts the id of the role into the addresses safely', async () => {
      const result = service.update('a/b', draft, { add: ['Permissions.ViewUser'], remove: [] });
      http.expectOne(`${url}/a%2Fb`).flush(null, noContent);
      (await next(`${url}/a%2Fb/claims/Permissions.ViewUser`)).flush(null, noContent);
      expect(await result).toEqual({ failedClaims: [] });
    });
  });
});
