import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { ConnectionService } from '../connection/connection.service';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;
  let service: AuthService;
  let url: string;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    service = TestBed.inject(AuthService);
    url = `${connection.serverUrl()}/api/v1/auth/login`;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts the credentials and keeps the tokens', async () => {
    const result = service.login('a@b.org', 'Abcde1');
    const request = http.expectOne(url);
    expect(request.request.body).toEqual({ email: 'a@b.org', password: 'Abcde1' });
    request.flush({ accessToken: 'access', refreshToken: 'refresh' });

    expect(await result).toBe('ok');
    expect(service.accessToken()).toBe('access');
    expect(service.refreshToken()).toBe('refresh');
  });

  it('writes both tokens to localStorage under per-server keys', async () => {
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });
    await result;

    const serverUrl = TestBed.inject(ConnectionService).serverUrl();
    expect(localStorage.getItem(`yavir.accessToken.${serverUrl}`)).toBe('access');
    expect(localStorage.getItem(`yavir.refreshToken.${serverUrl}`)).toBe('refresh');
  });

  it('still logs in when localStorage is unavailable', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('denied');
    });
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });
    expect(await result).toBe('ok');
    expect(service.accessToken()).toBe('access');
    expect(service.refreshToken()).toBe('refresh');
  });

  it('writes nothing to localStorage when the login fails', async () => {
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    await result;
    expect(localStorage.length).toBe(0);
  });

  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [500, 'error'],
  ])('maps HTTP %i to %s and keeps no tokens', async (status, expected) => {
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(expected);
    expect(service.accessToken()).toBeNull();
  });

  it('reports an unreachable server', async () => {
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });

  describe('token lifecycle', () => {
    const server = () => TestBed.inject(ConnectionService).serverUrl();
    const refreshUrl = () => `${server()}/api/v1/auth/refresh-token`;
    const store = (access: string, refresh: string) => {
      localStorage.setItem(`yavir.accessToken.${server()}`, access);
      localStorage.setItem(`yavir.refreshToken.${server()}`, refresh);
    };
    const jwtWith = (exp: number) => `h.${btoa(JSON.stringify({ exp })).replace(/=+$/, '')}.s`;

    it('restores both tokens from localStorage', () => {
      store('a', 'r');
      service.restoreTokens();
      expect(service.hasTokens()).toBe(true);
      expect(service.accessToken()).toBe('a');
      expect(service.refreshToken()).toBe('r');
    });

    it('does not restore a half-stored pair', () => {
      localStorage.setItem(`yavir.refreshToken.${server()}`, 'r');
      service.restoreTokens();
      expect(service.hasTokens()).toBe(false);
    });

    it('detects an expired or nearly expired access token from its exp claim', () => {
      const now = Math.floor(Date.now() / 1000);
      for (const [exp, expired] of [[now - 10, true], [now + 10, true], [now + 600, false]] as const) {
        store(jwtWith(exp), 'r');
        service.clearTokens();
        store(jwtWith(exp), 'r');
        service.restoreTokens();
        expect(service.accessTokenExpired()).toBe(expired);
      }
    });

    it('treats an unreadable access token as not expired', () => {
      store('not-a-jwt', 'r');
      service.restoreTokens();
      expect(service.accessTokenExpired()).toBe(false);
    });

    it('sends the stored pair on refresh and keeps the new one', async () => {
      store('a', 'r');
      service.restoreTokens();
      const result = service.refresh();
      const request = http.expectOne(refreshUrl());
      expect(request.request.body).toEqual({ accessToken: 'a', refreshToken: 'r' });
      request.flush({ accessToken: 'a2', refreshToken: 'r2' });
      expect(await result).toBe('ok');
      expect(localStorage.getItem(`yavir.refreshToken.${server()}`)).toBe('r2');
    });

    it('shares one request between simultaneous refresh calls', async () => {
      store('a', 'r');
      service.restoreTokens();
      const first = service.refresh();
      const second = service.refresh();
      http.expectOne(refreshUrl()).flush({ accessToken: 'a2', refreshToken: 'r2' });
      expect(await first).toBe('ok');
      expect(await second).toBe('ok');
    });

    it('adopts the pair another tab saved while waiting, without a request', async () => {
      store('a', 'r');
      service.restoreTokens();
      localStorage.setItem(`yavir.accessToken.${server()}`, jwtWith(Math.floor(Date.now() / 1000) + 600));
      localStorage.setItem(`yavir.refreshToken.${server()}`, 'r-from-other-tab');
      expect(await service.refresh()).toBe('ok');
      http.expectNone(refreshUrl());
      expect(service.refreshToken()).toBe('r-from-other-tab');
    });

    it('keeps the pair another tab saved when the server rejects ours, instead of wiping it', async () => {
      store('a', 'r');
      service.restoreTokens();
      const result = service.refresh();
      const request = http.expectOne(refreshUrl());
      // Інша вкладка встигла першою й записала нову пару.
      localStorage.setItem(`yavir.accessToken.${server()}`, 'a-other');
      localStorage.setItem(`yavir.refreshToken.${server()}`, 'r-other');
      request.flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(await result).toBe('ok');
      expect(service.refreshToken()).toBe('r-other');
      expect(localStorage.getItem(`yavir.refreshToken.${server()}`)).toBe('r-other');
    });

    it('drops the tokens when the server rejects them and nothing newer is stored', async () => {
      store('a', 'r');
      service.restoreTokens();
      const result = service.refresh();
      http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(await result).toBe('unauthorized');
      expect(service.hasTokens()).toBe(false);
      expect(localStorage.getItem(`yavir.refreshToken.${server()}`)).toBeNull();
    });

    it('keeps the tokens when the server does not answer', async () => {
      store('a', 'r');
      service.restoreTokens();
      const result = service.refresh();
      http.expectOne(refreshUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
      expect(service.hasTokens()).toBe(true);
    });

    it('follows another tab through the storage event, including its sign-out', () => {
      store('a', 'r');
      service.restoreTokens();

      store('a2', 'r2');
      window.dispatchEvent(new StorageEvent('storage', { key: `yavir.refreshToken.${server()}` }));
      expect(service.refreshToken()).toBe('r2');

      localStorage.removeItem(`yavir.accessToken.${server()}`);
      localStorage.removeItem(`yavir.refreshToken.${server()}`);
      window.dispatchEvent(new StorageEvent('storage', { key: `yavir.refreshToken.${server()}` }));
      expect(service.hasTokens()).toBe(false);
    });

    it('signs out: forgets the tokens and goes to the login page', () => {
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      store('a', 'r');
      service.restoreTokens();
      service.signOut();
      expect(service.hasTokens()).toBe(false);
      expect(localStorage.getItem(`yavir.accessToken.${server()}`)).toBeNull();
      expect(navigate).toHaveBeenCalledWith('/login');
    });
  });
});
