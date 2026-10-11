import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { ConnectionService } from '../connection/connection.service';
import { AccountsService } from '../accounts/accounts.service';
import { AccountRestoreService } from '../accounts/account-restore.service';
import { AuthService } from './auth.service';
import { SessionService } from './session.service';

/** JWT без підпису: тут важливий лише `exp`. */
function jwt(expiresInSeconds: number): string {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + expiresInSeconds }));
  return `h.${payload.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.s`;
}

describe('SessionService', () => {
  let http: HttpTestingController;
  let auth: AuthService;
  let server: string;
  let navigate: ReturnType<typeof vi.spyOn>;

  function signIn(expiresInSeconds: number, refresh = 'refresh'): void {
    TestBed.inject(AccountsService).add({
      server,
      accessToken: jwt(expiresInSeconds),
      refreshToken: refresh,
      identity: 'user-1',
    });
    TestBed.tick();
  }

  beforeEach(() => {
    vi.useFakeTimers();
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    auth = TestBed.inject(AuthService);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    TestBed.inject(SessionService);
    TestBed.tick();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const refreshUrl = () => `${server}/api/v1/auth/refresh-token`;

  it('refreshes the tokens a minute before the access token expires, and not earlier', async () => {
    signIn(600);
    await vi.advanceTimersByTimeAsync(538_000);
    http.expectNone(refreshUrl());

    await vi.advanceTimersByTimeAsync(4_000);
    const request = http.expectOne(refreshUrl());
    expect(request.request.body).toEqual({ accessToken: auth.accessToken(), refreshToken: 'refresh' });
    request.flush({ accessToken: jwt(600), refreshToken: 'refresh-2' });
  });

  it('schedules the next refresh for the new token', async () => {
    signIn(120);
    await vi.advanceTimersByTimeAsync(61_000);
    http.expectOne(refreshUrl()).flush({ accessToken: jwt(300), refreshToken: 'refresh-2' });
    await vi.advanceTimersByTimeAsync(1);
    TestBed.tick();

    await vi.advanceTimersByTimeAsync(230_000);
    http.expectNone(refreshUrl());
    await vi.advanceTimersByTimeAsync(15_000);
    http.expectOne(refreshUrl()).flush({ accessToken: jwt(300), refreshToken: 'refresh-3' });
  });

  it('refreshes half-way through when the token lives less than the lead time', async () => {
    signIn(40);
    await vi.advanceTimersByTimeAsync(19_000);
    http.expectNone(refreshUrl());
    await vi.advanceTimersByTimeAsync(2_000);
    http.expectOne(refreshUrl()).flush({ accessToken: jwt(600), refreshToken: 'refresh-2' });
  });

  it('does nothing for a token whose expiry cannot be read', async () => {
    TestBed.inject(AccountsService).add({ server, accessToken: 'opaque', refreshToken: 'refresh', identity: 'user-1' });
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(10 * 60_000);
    http.expectNone(refreshUrl());
  });

  it('retries after a network failure while keeping the tokens', async () => {
    signIn(120);
    await vi.advanceTimersByTimeAsync(61_000);
    http.expectOne(refreshUrl()).error(new ProgressEvent('error'));
    await vi.advanceTimersByTimeAsync(1);
    expect(auth.hasTokens()).toBe(true);

    await vi.advanceTimersByTimeAsync(30_000);
    http.expectOne(refreshUrl()).flush({ accessToken: jwt(600), refreshToken: 'refresh-2' });
  });

  it('goes to /login when the server rejects the tokens', async () => {
    signIn(120);
    await vi.advanceTimersByTimeAsync(61_000);
    http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
    await vi.advanceTimersByTimeAsync(1);
    TestBed.tick();

    expect(auth.hasTokens()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('refreshes at once when the tab becomes visible again and the token is nearly expired', async () => {
    signIn(600);
    // Таймер не спрацював, поки вкладка була у фоні; термін уже майже вийшов.
    vi.setSystemTime(Date.now() + 580_000);
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
    document.dispatchEvent(new Event('visibilitychange'));
    await vi.advanceTimersByTimeAsync(5_000);
    http.expectOne(refreshUrl()).flush({ accessToken: jwt(600), refreshToken: 'refresh-2' });
  });

  describe('inactive accounts', () => {
    const DAY = 24 * 60 * 60 * 1000;
    const otherServer = 'http://other.example.org:5065';
    const otherRefreshUrl = `${otherServer}/api/v1/auth/refresh-token`;

    /** Неактивний акаунт із токенами, виданими `ageMs` тому, і активний, що додається після нього. */
    function withInactive(ageMs: number): string {
      const accounts = TestBed.inject(AccountsService);
      const inactive = accounts.add({ server: otherServer, accessToken: 'ia', refreshToken: 'ir', identity: 'user-2' });
      accounts.update(inactive.id, { tokensIssuedAt: Date.now() - ageMs });
      signIn(3600);
      return inactive.id;
    }

    const becomeVisible = () => {
      Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
    };

    it('exchanges the tokens of a due inactive account when the tab becomes visible', async () => {
      withInactive(4 * DAY);
      TestBed.tick();
      await vi.advanceTimersByTimeAsync(10);
      // Старт уже міг оновити його; інакше це робить повернення у фокус.
      const open = http.match({ url: otherRefreshUrl });
      if (open.length === 0) {
        becomeVisible();
        await vi.advanceTimersByTimeAsync(10);
      }
      const request = open[0] ?? http.expectOne(otherRefreshUrl);
      expect(request.request.body).toEqual({ accessToken: 'ia', refreshToken: 'ir' });
      request.flush({ accessToken: 'ia2', refreshToken: 'ir2' });
    });

    it('leaves an inactive account alone while half of the term has not passed', async () => {
      withInactive(2 * DAY);
      becomeVisible();
      await vi.advanceTimersByTimeAsync(10);
      http.expectNone(otherRefreshUrl);
    });

    it('checks the inactive accounts once an hour, even if the tab never changed visibility', async () => {
      const id = withInactive(DAY);
      TestBed.tick();
      await vi.advanceTimersByTimeAsync(10);
      http.expectNone(otherRefreshUrl);

      // Минуло ще кілька днів; щогодинна перевірка знаходить акаунт, якому вже час.
      TestBed.inject(AccountsService).update(id, { tokensIssuedAt: Date.now() - 4 * DAY });
      await vi.advanceTimersByTimeAsync(60 * 60_000);
      http.expectOne(otherRefreshUrl).flush({ accessToken: 'ia2', refreshToken: 'ir2' });
    });
  });

  it('moves to /login when the tokens disappear (for example, a sign-out in another tab)', () => {
    signIn(600);
    auth.clearTokens();
    TestBed.tick();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('does not redirect from /login when the tokens disappear there', () => {
    Object.defineProperty(TestBed.inject(Router), 'url', { get: () => '/login?error=x', configurable: true });
    signIn(600);
    auth.clearTokens();
    TestBed.tick();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not redirect at start-up, when there were no tokens to begin with', () => {
    TestBed.tick();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not move to /login while the search for a valid account is running, even though the active account is switched', () => {
    signIn(600);
    const restore = TestBed.inject(AccountRestoreService);
    // Під час пошуку акаунти по черзі стають активними й тимчасово «зникають».
    (restore as unknown as { searchingSignal: { set(v: boolean): void } }).searchingSignal.set(true);
    TestBed.tick();
    auth.clearTokens();
    TestBed.tick();
    expect(navigate).not.toHaveBeenCalled();
  });
});
