import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { CurrentUserService } from './current-user.service';

describe('CurrentUserService', () => {
  let http: HttpTestingController;
  let service: CurrentUserService;
  let auth: AuthService;
  let url: string;
  const profile = { id: 'u1', email: 'a@b.org', firstName: 'Олена', lastName: 'Іваненко', roles: [], customFields: [] };

  function signIn(): void {
    const server = TestBed.inject(ConnectionService).serverUrl();
    TestBed.inject(AccountsService).add({ server, accessToken: 'access', refreshToken: 'refresh', identity: 'u1' });
  }

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(CurrentUserService);
    url = `${connection.serverUrl()}/api/v1/users/me`;
    signIn();
    TestBed.tick();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('requests the profile once, keeps it and does not ask again', async () => {
    const first = service.ensure();
    http.expectOne(url).flush(profile);
    expect(await first).toBe('ok');
    expect(service.user()).toEqual(profile);

    expect(await service.ensure()).toBe('ok');
    http.expectNone(url);
  });

  it('shares one request between simultaneous checks', async () => {
    const first = service.ensure();
    const second = service.ensure();
    http.expectOne(url).flush(profile);
    expect(await first).toBe('ok');
    expect(await second).toBe('ok');
  });

  it('reports unauthorized when the server rejects the token', async () => {
    const result = service.ensure();
    http.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await result).toBe('unauthorized');
    expect(service.user()).toBeNull();
  });

  it.each([
    ['a server error', 500],
    ['a missing profile', 404],
  ])('reports unavailable (not unauthorized) for %s, and asks again next time', async (_name, status) => {
    const result = service.ensure();
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe('unavailable');

    const again = service.ensure();
    http.expectOne(url).flush(profile);
    expect(await again).toBe('ok');
  });

  it('reports unavailable when the server does not answer', async () => {
    const result = service.ensure();
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unavailable');
  });

  it('forgets the profile on sign-out and on a new sign-in', async () => {
    const first = service.ensure();
    http.expectOne(url).flush(profile);
    await first;
    expect(service.user()).not.toBeNull();

    auth.clearTokens();
    TestBed.tick();
    expect(service.user()).toBeNull();

    signIn();
    const login = auth.login('a@b.org', 'x');
    http.expectOne(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/auth/login`).flush({ accessToken: 'a2', refreshToken: 'r2' });
    await login;
    const second = service.ensure();
    http.expectOne(url).flush({ ...profile, id: 'u2' });
    await second;
    expect(service.user()?.id).toBe('u2');
  });

  it('does not take a profile that arrived after the user signed out', async () => {
    const result = service.ensure();
    const request = http.expectOne(url);
    auth.clearTokens();
    request.flush(profile);
    expect(await result).toBe('unauthorized');
    expect(service.user()).toBeNull();
  });

  it('keeps a profile that arrived before the first reset check ran', async () => {
    // Сервіс уже створено в beforeEach, але ефекти ще не прокручувались.
    const fresh = TestBed.inject(CurrentUserService);
    const result = fresh.ensure();
    http.expectOne(url).flush(profile);
    await result;
    TestBed.tick();
    expect(fresh.user()).toEqual(profile);
  });
});
