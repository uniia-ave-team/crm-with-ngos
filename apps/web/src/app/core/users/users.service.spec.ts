import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let http: HttpTestingController;
  let service: UsersService;
  let url: string;
  const user = { firstName: ' ', lastName: ' ', email: 'a@b.org', password: 'Abcde1', confirmPassword: 'Abcde1' };

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
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

  describe('get', () => {
    const profileUrl = (id: string) => `${url}/${id}`;

    it('requests the profile of the given user and returns it', async () => {
      const body = { id: 'u-1', email: 'a@b.org', firstName: ' ', lastName: ' ', roles: [], customFields: [] };
      const result = service.get('u-1');
      const request = http.expectOne(profileUrl('u-1'));
      expect(request.request.method).toBe('GET');
      request.flush(body);
      expect(await result).toEqual(body);
    });

    it('escapes the id so that it stays one path segment', async () => {
      const result = service.get('a/b');
      http.expectOne(profileUrl('a%2Fb')).flush({}, { status: 404, statusText: 'Not Found' });
      expect(await result).toBe('error');
    });

    it.each([
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [404, 'error'],
    ])('returns the failure for HTTP %i (%s)', async (status, expected) => {
      const result = service.get('u-1');
      http.expectOne(profileUrl('u-1')).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.get('u-1');
      http.expectOne(profileUrl('u-1')).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('updateProfile', () => {
    const profileUrl = (id: string) => `${url}/${id}`;
    const body = {
      firstName: ' ',
      lastName: ' ',
      patronymic: null,
      internalPosition: null,
      phoneNumber: null,
      country: null,
      emergencyContact: null,
      preferredLanguage: null,
      pronouns: ' ',
      pronounCategory: 2,
      avatarUrl: null,
    };

    it('puts the whole profile to /users/{id} and treats 204 as updated', async () => {
      const result = service.updateProfile('u-1', body);
      const request = http.expectOne(profileUrl('u-1'));
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
      const result = service.updateProfile('u-1', body);
      http.expectOne(profileUrl('u-1')).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.updateProfile('u-1', body);
      http.expectOne(profileUrl('u-1')).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('changePassword', () => {
    const passwordUrl = () => `${url}/me/password`;

    it('posts both passwords to /users/me/password and treats 204 as changed', async () => {
      const result = service.changePassword('Old1!aaa', 'New1!bbb');
      const request = http.expectOne(passwordUrl());
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ currentPassword: 'Old1!aaa', newPassword: 'New1!bbb' });
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe('changed');
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
      const log = vi.mocked(console.log);
      const warn = vi.mocked(console.warn);
      const result = service.changePassword('Old1!aaa', 'New1!bbb');
      http.expectOne(passwordUrl()).flush({ detail: 'Old1!aaa' }, { status: 400, statusText: 'Bad Request' });
      await result;
      const printed = JSON.stringify([...log.mock.calls, ...warn.mock.calls]);
      expect(printed).not.toContain('Old1!aaa');
      expect(printed).not.toContain('New1!bbb');
    });
  });
});
