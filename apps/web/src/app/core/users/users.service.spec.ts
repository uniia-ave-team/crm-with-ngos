import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { UsersPage, UsersService } from './users.service';

describe('UsersService', () => {
  let http: HttpTestingController;
  let service: UsersService;
  let url: string;
  const user = { firstName: ' ', lastName: ' ', patronymic: null, email: 'a@b.org', password: 'Abcde1', confirmPassword: 'Abcde1' };

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    service = TestBed.inject(UsersService);
    url = `${connection.serverUrl()}/api/v1/users`;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts the user data as is', async () => {
    const result = service.create(user);
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(user);
    request.flush('id', { status: 201, statusText: 'Created' });
    expect(await result).toBe('created');
  });

  it.each([
    [400, 'invalid'],
    [500, 'error'],
  ])('maps HTTP %i to %s', async (status, expected) => {
    const result = service.create(user);
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(expected);
  });

  it('reports an unreachable server', async () => {
    const result = service.create(user);
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });

  describe('the account of a colleague: deactivate, activate, delete', () => {
    const cases = [
      ['deactivate', 'POST', '/u%201/deactivate'],
      ['activate', 'POST', '/u%201/activate'],
      ['delete', 'DELETE', '/u%201'],
    ] as const;

    it.each(cases)('%s sends %s to the users path with the encoded id, no body, and gives done on 204', async (name, method, path) => {
      const result = service[name]('u 1');
      const request = http.expectOne(`${url}${path}`);
      expect(request.request.method).toBe(method);
      expect(request.request.body).toBeNull();
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe('done');
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [404, 'error'],
      [500, 'error'],
    ])('maps HTTP %i to %s', async (status, failure) => {
      for (const [name, , path] of cases) {
        const result = service[name]('u 1');
        http.expectOne(`${url}${path}`).flush(null, { status, statusText: 'x' });
        expect(await result).toBe(failure);
      }
    });

    it('reports an unreachable server', async () => {
      const result = service.deactivate('u 1');
      http.expectOne(`${url}/u%201/deactivate`).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('list (GET /users, one page of the colleagues)', () => {
    const NO_EXTRA = { patronymic: null, phoneNumber: null, country: null, emergencyContact: null, pronouns: null, pronounCategory: null, avatarUrl: null, roles: [], isActive: true };
    const page = (n: number) => http.expectOne((request) => request.url === url && request.params.get('PageNumber') === String(n));

    it('asks for the page with the number and the size that were given (GET, no body)', async () => {
      const result = service.list(3, 20);
      const request = page(3);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.get('PageSize')).toBe('20');
      // Разом із вимкненими акаунтами: без цього сервер їх не віддає.
      expect(request.request.params.get('ShowDeleted')).toBe('true');
      request.flush({ items: [], hasNextPage: false });
      expect(await result).toEqual({ items: [], hasNextPage: false });
    });

    it('gives no arguments of the sorting when there is none (OrderBy and SortOrder are not in the request)', async () => {
      const result = service.list(1, 20);
      const request = page(1);
      expect(request.request.params.has('OrderBy')).toBe(false);
      expect(request.request.params.has('SortOrder')).toBe(false);
      request.flush({ items: [], hasNextPage: false });
      await result;
    });

    it.each(['asc', 'desc'] as const)('passes OrderBy and SortOrder=%s when the sorting is given', async (sortOrder) => {
      const result = service.list(2, 20, { orderBy: 'Email', sortOrder });
      const request = page(2);
      expect(request.request.params.get('OrderBy')).toBe('Email');
      expect(request.request.params.get('SortOrder')).toBe(sortOrder);
      request.flush({ items: [], hasNextPage: false });
      await result;
    });

    it('passes no sorting when OrderBy is empty (without it the server would sort by its default field in that direction)', async () => {
      const result = service.list(1, 20, { orderBy: '  ', sortOrder: 'desc' });
      const request = page(1);
      expect(request.request.params.has('OrderBy')).toBe(false);
      expect(request.request.params.has('SortOrder')).toBe(false);
      request.flush({ items: [], hasNextPage: false });
      await result;
    });

    it('gives the colleagues with the id, the email and the names, and whether there is a next page', async () => {
      const result = service.list(1, 20);
      page(1).flush({
        items: [{ id: 'u1', email: 'a@b.org', firstName: 'Перший', lastName: 'Колега', extra: 1 }],
        totalCount: 41,
        hasNextPage: true,
      });
      expect(await result).toEqual({
        items: [{ ...NO_EXTRA, id: 'u1', email: 'a@b.org', firstName: 'Перший', lastName: 'Колега' }],
        hasNextPage: true,
      });
    });

    it('takes the phone, the country, the emergency contact, the pronouns with the category, the avatar and the roles from the answer', async () => {
      const result = service.list(1, 20);
      page(1).flush({
        items: [
          {
            id: 'u1',
            phoneNumber: '1',
            country: 'x',
            emergencyContact: 'y',
            pronouns: 'z',
            pronounCategory: 2,
            avatarUrl: '/api/v1/users/u1/avatar',
            roles: [{ id: 'r', name: 'n' }],
          },
        ],
        hasNextPage: false,
      });
      const [colleague] = ((await result) as UsersPage).items;
      expect(colleague).toMatchObject({
        phoneNumber: '1',
        country: 'x',
        emergencyContact: 'y',
        pronouns: 'z',
        pronounCategory: 2,
        avatarUrl: '/api/v1/users/u1/avatar',
        roles: [{ id: 'r', name: 'n' }],
      });
    });

    it.each([
      ['an older server that does not give them', {}],
      ['null values', { patronymic: null, phoneNumber: null, country: null, emergencyContact: null, pronouns: null, pronounCategory: null, avatarUrl: null, roles: null, isActive: null }],
      ['empty or blank texts', { phoneNumber: '', pronouns: '  ', avatarUrl: ' ' }],
      ['values of a wrong type', { phoneNumber: 5, pronouns: {}, pronounCategory: '2', avatarUrl: 7, roles: 'x', isActive: 'false' }],
    ])('gives null, an empty list of roles and an enabled account for %s', async (_name, extra) => {
      const result = service.list(1, 20);
      page(1).flush({ items: [{ id: 'u1', ...extra }], hasNextPage: false });
      const [colleague] = ((await result) as UsersPage).items;
      expect(colleague).toMatchObject(NO_EXTRA);
    });

    it('takes a disabled account only from an explicit isActive: false', async () => {
      const result = service.list(1, 20);
      page(1).flush({ items: [{ id: 'u1', isActive: false }, { id: 'u2', isActive: true }], hasNextPage: false });
      expect(((await result) as UsersPage).items.map((colleague) => colleague.isActive)).toEqual([false, true]);
    });

    it('keeps only the roles that have a text id and a text name', async () => {
      const result = service.list(1, 20);
      page(1).flush({ items: [{ id: 'u1', roles: [{ id: 'r', name: 'n' }, { id: 5, name: 'x' }, { name: 'y' }, null] }], hasNextPage: false });
      expect(((await result) as UsersPage).items[0].roles).toEqual([{ id: 'r', name: 'n' }]);
    });

    it('skips the items without an id, and makes empty strings of the missing texts', async () => {
      const result = service.list(1, 20);
      page(1).flush({ items: [{ email: 'a@b.org' }, null, { id: 'u2', firstName: 5 }], hasNextPage: false });
      expect(await result).toEqual({ items: [{ ...NO_EXTRA, id: 'u2', email: '', firstName: '', lastName: '' }], hasNextPage: false });
    });

    it('is an error when the answer has no list of colleagues', async () => {
      const result = service.list(1, 20);
      page(1).flush({ items: 'x' });
      expect(await result).toBe('error');
    });

    it.each([
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('reports HTTP %i as %s', async (status, failure) => {
      const result = service.list(1, 20);
      page(1).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(failure);
    });

    it('reports an unreachable server', async () => {
      const result = service.list(1, 20);
      page(1).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('getMyPermissions', () => {
    const permissionsUrl = () => `${url}/me/permissions`;

    it('requests the permissions of the current user and returns the list', async () => {
      const result = service.getMyPermissions();
      const request = http.expectOne(permissionsUrl());
      expect(request.request.method).toBe('GET');
      request.flush(['ViewUser', 'UpdateUser']);
      expect(await result).toEqual(['ViewUser', 'UpdateUser']);
    });

    it('takes only the strings of the list', async () => {
      const result = service.getMyPermissions();
      http.expectOne(permissionsUrl()).flush(['ViewUser', 1, null, 'UpdateUser']);
      expect(await result).toEqual(['ViewUser', 'UpdateUser']);
    });

    it('treats an answer that is not a list as an error', async () => {
      const result = service.getMyPermissions();
      http.expectOne(permissionsUrl()).flush({ permissions: [] });
      expect(await result).toBe('error');
    });

    it.each([
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('returns the failure for HTTP %i (%s)', async (status, expected) => {
      const result = service.getMyPermissions();
      http.expectOne(permissionsUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.getMyPermissions();
      http.expectOne(permissionsUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('updateProfile', () => {
    const meUrl = () => `${url}/me`;
    const body = {
      firstName: ' ',
      lastName: ' ',
      patronymic: null,
      phoneNumber: null,
      country: null,
      emergencyContact: null,
      preferredLanguage: null,
      pronouns: ' ',
      pronounCategory: 2,
    };

    it('puts the whole profile to /users/me and treats 204 as updated', async () => {
      const result = service.updateProfile(body);
      const request = http.expectOne(meUrl());
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(body);
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe('updated');
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [404, 'error'],
      [500, 'error'],
    ])('maps HTTP %i to %s', async (status, expected) => {
      const result = service.updateProfile(body);
      http.expectOne(meUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.updateProfile(body);
      http.expectOne(meUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('uploadAvatar', () => {
    const meAvatarUrl = () => `${url}/me/avatar`;
    const file = new File(['image'], 'photo.png', { type: 'image/png' });

    it('posts the image as multipart/form-data in the field "file" and treats 200 as uploaded', async () => {
      const result = service.uploadAvatar(file);
      const request = http.expectOne(meAvatarUrl());
      expect(request.request.method).toBe('POST');
      const form = request.request.body as FormData;
      expect(form).toBeInstanceOf(FormData);
      expect((form.get('file') as File).name).toBe('photo.png');
      request.flush(null);
      expect(await result).toBe('uploaded');
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [404, 'error'],
      [500, 'error'],
    ])('maps HTTP %i to %s', async (status, expected) => {
      const result = service.uploadAvatar(file);
      http.expectOne(meAvatarUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.uploadAvatar(file);
      http.expectOne(meAvatarUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('changePassword', () => {
    const passwordUrl = () => `${url}/me/password`;
    const signIn = () =>
      TestBed.inject(AccountsService).add({
        server: TestBed.inject(ConnectionService).serverUrl(),
        accessToken: 'old-access',
        refreshToken: 'old-refresh',
        identity: 'user-1',
      });
    const tokensOf = (id: string) => {
      const account = TestBed.inject(AccountsService).accounts().find((candidate) => candidate.id === id);
      return { accessToken: account?.accessToken, refreshToken: account?.refreshToken };
    };

    it('posts both passwords to /users/me/password and takes the new token pair for the account that asked', async () => {
      const account = signIn();
      const result = service.changePassword('Old1!aaa', 'New1!bbb');
      const request = http.expectOne(passwordUrl());
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ currentPassword: 'Old1!aaa', newPassword: 'New1!bbb' });
      request.flush({ accessToken: 'new-access', refreshToken: 'new-refresh' });

      expect(await result).toBe('changed');
      // Сервер відкликав старий refresh-токен: зі старою парою сесія закінчилася б при наступному оновленні.
      expect(tokensOf(account.id)).toEqual({ accessToken: 'new-access', refreshToken: 'new-refresh' });
    });

    it('keeps the token pair when an older server answers 204 without a body (it revokes nothing)', async () => {
      const account = signIn();
      const result = service.changePassword('Old1!aaa', 'New1!bbb');
      http.expectOne(passwordUrl()).flush(null, { status: 204, statusText: 'No Content' });

      expect(await result).toBe('changed');
      expect(tokensOf(account.id)).toEqual({ accessToken: 'old-access', refreshToken: 'old-refresh' });
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('maps HTTP %i to %s', async (status, expected) => {
      const result = service.changePassword('a', 'b');
      http.expectOne(passwordUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.changePassword('a', 'b');
      http.expectOne(passwordUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });

    it('never writes the passwords to the console', async () => {
      const warn = vi.mocked(console.warn);
      const result = service.changePassword('Old1!aaa', 'New1!bbb');
      http.expectOne(passwordUrl()).flush({ detail: 'Old1!aaa' }, { status: 400, statusText: 'Bad Request' });
      await result;
      const printed = JSON.stringify(warn.mock.calls);
      expect(printed).not.toContain('Old1!aaa');
      expect(printed).not.toContain('New1!bbb');
    });
  });
});
