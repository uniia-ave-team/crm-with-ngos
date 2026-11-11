import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { InvitationsService } from './invitations.service';

describe('InvitationsService', () => {
  let http: HttpTestingController;
  let service: InvitationsService;
  let url: string;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    url = `${connection.serverUrl()}/api/v1/invitations`;
    service = TestBed.inject(InvitationsService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts the email and the ids of the roles to /invitations', async () => {
    const result = service.invite('a@b.org', ['r1', 'r2']);
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'a@b.org', roleIds: ['r1', 'r2'] });
    request.flush({ token: 'tok', expiresAt: '2026-11-20T10:00:00Z' });
    expect(await result).toEqual({ token: 'tok', expiresAt: '2026-11-20T10:00:00Z' });
  });

  it('gives an empty date when the answer has none', async () => {
    const result = service.invite('a@b.org', ['r1']);
    http.expectOne(url).flush({ token: 'tok' });
    expect(await result).toEqual({ token: 'tok', expiresAt: '' });
  });

  it.each([{}, { token: '' }, { token: 5 }, null])('is an error when the answer has no token (%j)', async (body) => {
    const result = service.invite('a@b.org', ['r1']);
    http.expectOne(url).flush(body as never);
    expect(await result).toBe('error');
  });

  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [500, 'error'],
  ])('reports HTTP %i as %s', async (status, failure) => {
    const result = service.invite('a@b.org', ['r1']);
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(failure);
  });

  it('reports an unreachable server', async () => {
    const result = service.invite('a@b.org', ['r1']);
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });

  describe('validate', () => {
    const server = 'https://invited.example:5065';

    it('asks the given server about the token, which goes in the query encoded', async () => {
      const result = service.validate(server, 'a+b/c=');
      const request = http.expectOne((r) => r.url === `${server}/api/v1/invitations/validate`);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.get('token')).toBe('a+b/c=');
      expect(request.request.urlWithParams).toContain('token=a%2Bb/c=');
      request.flush({ email: 'a@b.org', ngoId: 'n', roleId: 'r' });
      expect(await result).toEqual({ email: 'a@b.org' });
    });

    it.each([{}, { email: '' }, { email: 5 }, null])('is an error when the answer has no e-mail (%j)', async (body) => {
      const result = service.validate(server, 'tok');
      http.expectOne((r) => r.url.endsWith('/validate')).flush(body as never);
      expect(await result).toBe('error');
    });

    it.each([
      [400, 'invalid'],
      [500, 'error'],
    ])('reports HTTP %i as %s', async (status, failure) => {
      const result = service.validate(server, 'tok');
      http.expectOne((r) => r.url.endsWith('/validate')).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(failure);
    });

    it('reports an unreachable server', async () => {
      const result = service.validate(server, 'tok');
      http.expectOne((r) => r.url.endsWith('/validate')).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('accept', () => {
    const server = 'https://invited.example:5065';
    const registration = {
      firstName: 'Перший',
      lastName: 'Другий',
      patronymic: null,
      password: 'Abcde1!x',
      confirmPassword: 'Abcde1!x',
    };

    it('posts the token together with the data of the colleague and returns the tokens', async () => {
      const result = service.accept(server, 'tok', registration);
      const request = http.expectOne(`${server}/api/v1/invitations/accept`);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ token: 'tok', ...registration });
      request.flush({ accessToken: 'access', refreshToken: 'refresh' });
      expect(await result).toEqual({ accessToken: 'access', refreshToken: 'refresh' });
    });

    it.each([{}, { accessToken: 'a' }, { refreshToken: 'r' }, { accessToken: '', refreshToken: 'r' }, null])(
      'is an error when the answer lacks a token (%j)',
      async (body) => {
        const result = service.accept(server, 'tok', registration);
        http.expectOne((r) => r.url.endsWith('/accept')).flush(body as never);
        expect(await result).toBe('error');
      },
    );

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [500, 'error'],
    ])('reports HTTP %i as %s', async (status, failure) => {
      const result = service.accept(server, 'tok', registration);
      http.expectOne((r) => r.url.endsWith('/accept')).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(failure);
    });

    it('reports an unreachable server', async () => {
      const result = service.accept(server, 'tok', registration);
      http.expectOne((r) => r.url.endsWith('/accept')).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });
});
