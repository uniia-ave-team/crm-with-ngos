import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { ConnectionService } from '../connection/connection.service';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

/** JWT без підпису: тут важливий лише `exp`. */
function jwt(expiresInSeconds: number): string {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + expiresInSeconds }));
  return `header.${payload.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.signature`;
}

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let auth: AuthService;
  let server: string;
  let navigate: ReturnType<typeof vi.spyOn>;

  function storeTokens(access: string, refresh: string): void {
    localStorage.setItem(`yavir.accessToken.${server}`, access);
    localStorage.setItem(`yavir.refreshToken.${server}`, refresh);
    auth.restoreTokens();
  }

  beforeEach(async () => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    const connection = TestBed.inject(ConnectionService);
    await vi.waitFor(() => http.expectOne(connection.apiHealthUrl()));
    server = connection.serverUrl();
    auth = TestBed.inject(AuthService);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('adds the access token to API requests', async () => {
    storeTokens(jwt(600), 'refresh');
    client.get(`${server}/api/v1/ngo`).subscribe();
    const request = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${auth.accessToken()}`);
    request.flush({});
  });

  it('sends no Authorization header without tokens', async () => {
    client.get(`${server}/api/v1/ngo`).subscribe({ error: () => undefined });
    const request = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    expect(request.request.headers.has('Authorization')).toBe(false);
  });

  it.each([
    ['GET', '/api/v1/system/status'],
    ['POST', '/api/v1/auth/login'],
    ['POST', '/api/v1/auth/refresh-token'],
    ['POST', '/api/v1/users'],
  ])('leaves %s %s anonymous', async (method, path) => {
    storeTokens(jwt(600), 'refresh');
    client.request(method, `${server}${path}`, { body: {} }).subscribe();
    const request = await vi.waitFor(() => http.expectOne(`${server}${path}`));
    expect(request.request.headers.has('Authorization')).toBe(false);
  });

  it('does not touch requests outside /api (for example /health)', async () => {
    storeTokens(jwt(600), 'refresh');
    client.get(`${server}/health`).subscribe();
    const request = await vi.waitFor(() => http.expectOne(`${server}/health`));
    expect(request.request.headers.has('Authorization')).toBe(false);
  });

  it('does not refresh or sign out when an anonymous request fails with 401', async () => {
    storeTokens(jwt(600), 'refresh');
    let status = 0;
    client.post(`${server}/api/v1/auth/login`, {}).subscribe({ error: (e) => (status = e.status) });
    const request = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/login`));
    request.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(status).toBe(401);
    http.expectNone(`${server}/api/v1/auth/refresh-token`);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('refreshes an expired access token before sending the request', async () => {
    storeTokens(jwt(-60), 'refresh');
    const old = auth.accessToken();
    client.get(`${server}/api/v1/ngo`).subscribe();

    const refresh = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`));
    expect(refresh.request.body).toEqual({ accessToken: old, refreshToken: 'refresh' });
    const fresh = jwt(600);
    refresh.flush({ accessToken: fresh, refreshToken: 'refresh-2' });

    const request = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${fresh}`);
    request.flush({});
  });

  it('refreshes on 401 and repeats the request once with the new token', async () => {
    storeTokens(jwt(600), 'refresh');
    let body: unknown;
    client.get(`${server}/api/v1/ngo`).subscribe((value) => (body = value));
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`))).flush({}, { status: 401, statusText: 'Unauthorized' });

    const refresh = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`));
    const fresh = jwt(600);
    refresh.flush({ accessToken: fresh, refreshToken: 'refresh-2' });

    const retry = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    expect(retry.request.headers.get('Authorization')).toBe(`Bearer ${fresh}`);
    retry.flush({ name: 'ГО' });
    expect(body).toEqual({ name: 'ГО' });
  });

  it('does not loop: a second 401 after a successful refresh is passed on', async () => {
    storeTokens(jwt(600), 'refresh');
    let status = 0;
    client.get(`${server}/api/v1/ngo`).subscribe({ error: (e) => (status = e.status) });
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    const refresh = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`));
    refresh.flush({ accessToken: jwt(600), refreshToken: 'refresh-2' });
    const retry = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    retry.flush({}, { status: 401, statusText: 'Unauthorized' });

    await vi.waitFor(() => expect(status).toBe(401));
    http.expectNone(`${server}/api/v1/auth/refresh-token`);
  });

  it('signs out and goes to the login page when the refresh fails', async () => {
    storeTokens(jwt(600), 'refresh');
    let status = 0;
    client.get(`${server}/api/v1/ngo`).subscribe({ error: (e) => (status = e.status) });
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    const refresh = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`));
    refresh.flush({}, { status: 401, statusText: 'Unauthorized' });

    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/login'));
    expect(status).toBe(401);
    expect(auth.hasTokens()).toBe(false);
    expect(localStorage.getItem(`yavir.refreshToken.${server}`)).toBeNull();
  });

  it('signs out when the refresh fails because the server does not answer', async () => {
    storeTokens(jwt(600), 'refresh');
    client.get(`${server}/api/v1/ngo`).subscribe({ error: () => undefined });
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    const refresh = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`));
    refresh.error(new ProgressEvent('error'));

    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/login'));
    expect(auth.hasTokens()).toBe(false);
  });

  it('shares one refresh between simultaneous requests', async () => {
    storeTokens(jwt(-60), 'refresh');
    client.get(`${server}/api/v1/ngo`).subscribe();
    client.get(`${server}/api/v1/users/me`).subscribe();

    const refresh = await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`));
    refresh.flush({ accessToken: jwt(600), refreshToken: 'refresh-2' });
    const first = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    const second = await vi.waitFor(() => http.expectOne(`${server}/api/v1/users/me`));
    expect(first.request.headers.get('Authorization')).toBe(second.request.headers.get('Authorization'));
    first.flush({});
    second.flush({});
  });

  it('reuses a pair refreshed meanwhile instead of refreshing again after a 401', async () => {
    const stale = jwt(600);
    storeTokens(stale, 'refresh');
    client.get(`${server}/api/v1/ngo`).subscribe();
    const request = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    // Поки відповідь 401 летіла, інший запит уже оновив токени (токен має
    // відрізнятися від попереднього, тож інший `exp`).
    const fresh = jwt(900);
    localStorage.setItem(`yavir.accessToken.${server}`, fresh);
    localStorage.setItem(`yavir.refreshToken.${server}`, 'refresh-2');
    auth.restoreTokens();
    window.dispatchEvent(new StorageEvent('storage', { key: `yavir.accessToken.${server}` }));
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    const retry = await vi.waitFor(() => http.expectOne(`${server}/api/v1/ngo`));
    expect(retry.request.headers.get('Authorization')).toBe(`Bearer ${fresh}`);
    http.expectNone(`${server}/api/v1/auth/refresh-token`);
    retry.flush({});
  });
});
