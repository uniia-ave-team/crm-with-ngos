import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { AccountsService } from '../accounts/accounts.service';
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

  it('adds an account for the server and the user of the token, makes it active and stores it', async () => {
    const jwtWithSub = `h.${btoa(JSON.stringify({ sub: 'user-42', exp: 9999999999 })).replace(/=+$/, '')}.s`;
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).flush({ accessToken: jwtWithSub, refreshToken: 'refresh' });
    await result;

    const accounts = TestBed.inject(AccountsService);
    const serverUrl = TestBed.inject(ConnectionService).serverUrl();
    expect(accounts.accounts()).toHaveLength(1);
    expect(accounts.active()).toMatchObject({
      server: serverUrl,
      userId: 'user-42',
      accessToken: jwtWithSub,
      refreshToken: 'refresh',
    });
    const id = accounts.activeId()!;
    expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([id]);
    expect(localStorage.getItem('yavir.activeAccount')).toBe(id);
    expect(JSON.parse(localStorage.getItem(`yavir.account.${id}`)!)).toMatchObject({ server: serverUrl, refreshToken: 'refresh' });
  });

  it('remembers the e-mail the user signed in with, because /users/me does not return it', async () => {
    const result = service.login('  A@B.org ', 'Abcde1');
    http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });
    await result;
    const accounts = TestBed.inject(AccountsService);
    expect(accounts.active()?.email).toBe('A@B.org');
    expect(JSON.parse(localStorage.getItem(`yavir.account.${accounts.activeId()}`)!).email).toBe('A@B.org');
  });

  it('identifies an account by e-mail when the token has no subject, so a second login updates it instead of duplicating', async () => {
    for (const refresh of ['r1', 'r2']) {
      const result = service.login(' A@B.org ', 'Abcde1');
      http.expectOne(url).flush({ accessToken: 'access', refreshToken: refresh });
      await result;
    }
    const accounts = TestBed.inject(AccountsService);
    expect(accounts.accounts()).toHaveLength(1);
    expect(accounts.active()?.refreshToken).toBe('r2');
  });

  it('keeps several accounts: a second user on the same server is a separate account and becomes active', async () => {
    for (const email of ['a@b.org', 'c@d.org']) {
      const result = service.login(email, 'Abcde1');
      http.expectOne(url).flush({ accessToken: `access-${email}`, refreshToken: `refresh-${email}` });
      await result;
    }
    const accounts = TestBed.inject(AccountsService);
    expect(accounts.accounts()).toHaveLength(2);
    expect(service.accessToken()).toBe('access-c@d.org');
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
    /** Акаунт із такими токенами на поточному сервері (активний). */
    const store = (access: string, refresh: string) => {
      TestBed.inject(AccountsService).add({ server: server(), accessToken: access, refreshToken: refresh, identity: 'user-1' });
    };
    const accountsStore = () => TestBed.inject(AccountsService);
    const storedRecord = () => JSON.parse(localStorage.getItem(`yavir.account.${accountsStore().activeId()}`)!);
    /** Те, що записала б інша вкладка: нові токени прямо в `localStorage`. */
    const writeFromOtherTab = (access: string, refresh: string) => {
      const id = accountsStore().activeId()!;
      const record = JSON.parse(localStorage.getItem(`yavir.account.${id}`)!);
      localStorage.setItem(`yavir.account.${id}`, JSON.stringify({ ...record, accessToken: access, refreshToken: refresh }));
    };
    const jwtWith = (exp: number) => `h.${btoa(JSON.stringify({ exp })).replace(/=+$/, '')}.s`;

    it('detects an expired or nearly expired access token from its exp claim', () => {
      const now = Math.floor(Date.now() / 1000);
      for (const [exp, expired] of [[now - 10, true], [now + 10, true], [now + 600, false]] as const) {
        store(jwtWith(exp), 'r');
        service.clearTokens();
        store(jwtWith(exp), 'r');
        expect(service.accessTokenExpired()).toBe(expired);
      }
    });

    it('treats an unreadable access token as not expired', () => {
      store('not-a-jwt', 'r');
      expect(service.accessTokenExpired()).toBe(false);
    });

    it('sends the stored pair on refresh and keeps the new one', async () => {
      store('a', 'r');
      const result = service.refresh();
      const request = http.expectOne(refreshUrl());
      expect(request.request.body).toEqual({ accessToken: 'a', refreshToken: 'r' });
      request.flush({ accessToken: 'a2', refreshToken: 'r2' });
      expect(await result).toBe('ok');
      expect(storedRecord()).toMatchObject({ accessToken: 'a2', refreshToken: 'r2' });
    });

    it('shares one request between simultaneous refresh calls', async () => {
      store('a', 'r');
      const first = service.refresh();
      const second = service.refresh();
      http.expectOne(refreshUrl()).flush({ accessToken: 'a2', refreshToken: 'r2' });
      expect(await first).toBe('ok');
      expect(await second).toBe('ok');
    });

    it('adopts the pair another tab saved while waiting, without a request', async () => {
      store('a', 'r');
      writeFromOtherTab(jwtWith(Math.floor(Date.now() / 1000) + 600), 'r-from-other-tab');
      expect(await service.refresh()).toBe('ok');
      http.expectNone(refreshUrl());
      expect(service.refreshToken()).toBe('r-from-other-tab');
    });

    it('keeps the pair another tab saved when the server rejects ours, instead of wiping it', async () => {
      store('a', 'r');
      const result = service.refresh();
      const request = http.expectOne(refreshUrl());
      // Інша вкладка встигла першою й записала нову пару.
      writeFromOtherTab('a-other', 'r-other');
      request.flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(await result).toBe('ok');
      expect(service.refreshToken()).toBe('r-other');
      expect(storedRecord().refreshToken).toBe('r-other');
    });

    it('marks the account outdated when the server rejects its tokens; as the only account it also clears the memory', async () => {
      store('a', 'r');
      const result = service.refresh();
      http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(await result).toBe('unauthorized');
      expect(service.hasTokens()).toBe(false);
      expect(accountsStore().accounts()).toHaveLength(0);
      expect(localStorage.length).toBe(0);
    });

    it('makes the other valid account the active one when the active one is rejected', async () => {
      const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
      store('a', 'r');
      const active = accountsStore().activeId()!;

      const result = service.refresh();
      http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(await result).toBe('unauthorized');

      // Застарілий більше не активний: активним став інший чинний акаунт.
      expect(accountsStore().activeId()).toBe(other.id);
      expect(service.accessToken()).toBe('b');
      expect(service.hasTokens()).toBe(true);
      expect(JSON.parse(localStorage.getItem(`yavir.account.${active}`)!).outdated).toBe(true);
      expect(JSON.parse(localStorage.getItem(`yavir.account.${other.id}`)!).outdated).toBe(false);
      expect(localStorage.getItem('yavir.accounts')).not.toBeNull();
    });

    describe('inactive accounts', () => {
      const DAY = 24 * 60 * 60 * 1000;
      const otherServer = 'http://other.example.org:5065';
      const otherRefreshUrl = `${otherServer}/api/v1/auth/refresh-token`;

      /** Активний акаунт на поточному сервері й неактивний на іншому, токени якого видано `ageMs` тому. */
      const withInactive = (ageMs: number) => {
        const inactive = accountsStore().add({ server: otherServer, accessToken: 'ia', refreshToken: 'ir', identity: 'user-2' });
        accountsStore().update(inactive.id, { tokensIssuedAt: Date.now() - ageMs });
        store('a', 'r');
        return { inactive: inactive.id, active: accountsStore().activeId()! };
      };

      it('exchanges the tokens of an inactive account once half of the refresh token term has passed', async () => {
        const { inactive, active } = withInactive(4 * DAY);
        const lastUsed = accountsStore().accounts().find((a) => a.id === inactive)!.lastUsedAt;

        const result = service.refreshStoredIfDue(inactive);
        const request = http.expectOne(otherRefreshUrl);
        // На сервер цього акаунта й з його власною парою токенів.
        expect(request.request.body).toEqual({ accessToken: 'ia', refreshToken: 'ir' });
        request.flush({ accessToken: 'ia2', refreshToken: 'ir2' });
        expect(await result).toBe('ok');

        const record = JSON.parse(localStorage.getItem(`yavir.account.${inactive}`)!);
        expect(record).toMatchObject({ accessToken: 'ia2', refreshToken: 'ir2', outdated: false });
        expect(Date.now() - record.tokensIssuedAt).toBeLessThan(5_000);
        // Активний акаунт не зачеплено, а «востаннє використано» неактивного не оновлюється.
        expect(accountsStore().activeId()).toBe(active);
        expect(service.refreshToken()).toBe('r');
        expect(record.lastUsedAt).toBe(lastUsed);
      });

      it('does nothing before half of the term has passed', async () => {
        const { inactive } = withInactive(3 * DAY);
        expect(await service.refreshStoredIfDue(inactive)).toBe('skipped');
        http.expectNone(otherRefreshUrl);
      });

      it('never exchanges the active account this way, nor an outdated one', async () => {
        const { inactive, active } = withInactive(6 * DAY);
        accountsStore().update(active, { tokensIssuedAt: Date.now() - 6 * DAY });
        expect(await service.refreshStoredIfDue(active)).toBe('skipped');

        accountsStore().update(inactive, { outdated: true });
        expect(await service.refreshStoredIfDue(inactive)).toBe('skipped');
        http.expectNone(otherRefreshUrl);
      });

      it('marks the inactive account outdated when its tokens are rejected, leaving the active one', async () => {
        const { inactive, active } = withInactive(5 * DAY);
        const result = service.refreshStoredIfDue(inactive);
        http.expectOne(otherRefreshUrl).flush({}, { status: 401, statusText: 'Unauthorized' });
        expect(await result).toBe('unauthorized');

        const record = JSON.parse(localStorage.getItem(`yavir.account.${inactive}`)!);
        expect(record).toMatchObject({ outdated: true, unavailable: false });
        expect(accountsStore().activeId()).toBe(active);
        expect(service.hasTokens()).toBe(true);
      });

      it('changes nothing when the server does not answer: it will be retried next time', async () => {
        const { inactive } = withInactive(5 * DAY);
        const result = service.refreshStoredIfDue(inactive);
        http.expectOne(otherRefreshUrl).error(new ProgressEvent('error'));
        expect(await result).toBe('unreachable');

        const record = JSON.parse(localStorage.getItem(`yavir.account.${inactive}`)!);
        expect(record).toMatchObject({ outdated: false, accessToken: 'ia', refreshToken: 'ir' });
      });

      it('takes the pair another tab already exchanged, without a request', async () => {
        const { inactive } = withInactive(5 * DAY);
        const record = JSON.parse(localStorage.getItem(`yavir.account.${inactive}`)!);
        localStorage.setItem(
          `yavir.account.${inactive}`,
          JSON.stringify({ ...record, accessToken: 'ia-tab', refreshToken: 'ir-tab', tokensIssuedAt: Date.now() }),
        );

        expect(await service.refreshStoredIfDue(inactive)).toBe('skipped');
        http.expectNone(otherRefreshUrl);
        expect(accountsStore().accounts().find((a) => a.id === inactive)?.refreshToken).toBe('ir-tab');
      });
    });

    it('keeps the tokens when the server does not answer', async () => {
      store('a', 'r');
      const result = service.refresh();
      http.expectOne(refreshUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
      expect(service.hasTokens()).toBe(true);
    });

    it('follows another tab through the storage event, including its sign-out', () => {
      store('a', 'r');
      const id = accountsStore().activeId()!;

      writeFromOtherTab('a2', 'r2');
      window.dispatchEvent(new StorageEvent('storage', { key: `yavir.account.${id}` }));
      expect(service.refreshToken()).toBe('r2');

      // Інша вкладка вийшла: запис, індекс і активний акаунт зникли.
      localStorage.removeItem(`yavir.account.${id}`);
      localStorage.setItem('yavir.accounts', '[]');
      localStorage.removeItem('yavir.activeAccount');
      window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.accounts' }));
      expect(service.hasTokens()).toBe(false);
    });

    describe('logout', () => {
      const logoutUrl = () => `${server()}/api/v1/auth/logout`;
      const validAccess = () => jwtWith(Math.floor(Date.now() / 1000) + 600);
      let navigate: ReturnType<typeof vi.spyOn>;

      beforeEach(() => {
        navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      });

      it('wipes the whole localStorage, forgets the tokens, goes to /login and revokes the refresh token', async () => {
        const access = validAccess();
        store(access, 'refresh-1');
        localStorage.setItem('yavir.language', 'uk');
        localStorage.setItem('yavir.serverAddress', 'somewhere');

        const result = service.logout();
        // Очищення й перехід відбуваються до відповіді сервера.
        expect(localStorage.length).toBe(0);
        expect(service.hasTokens()).toBe(false);
        expect(navigate).toHaveBeenCalledWith('/login');

        const request = http.expectOne(logoutUrl());
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ refreshToken: 'refresh-1' });
        expect(request.request.headers.get('Authorization')).toBe(`Bearer ${access}`);
        request.flush(null, { status: 204, statusText: 'No Content' });
        expect(await result).toBe('ok');
      });

      it.each([
        [400, 'invalid'],
        [401, 'unauthorized'],
        [500, 'error'],
      ])('still ends locally when the server answers HTTP %i', async (status, expected) => {
        store(validAccess(), 'refresh-1');
        const result = service.logout();
        http.expectOne(logoutUrl()).flush({}, { status, statusText: 'x' });
        expect(await result).toBe(expected);
        expect(localStorage.length).toBe(0);
        expect(service.hasTokens()).toBe(false);
      });

      it('still ends locally when the server does not answer', async () => {
        store(validAccess(), 'refresh-1');
        const result = service.logout();
        http.expectOne(logoutUrl()).error(new ProgressEvent('error'));
        expect(await result).toBe('unreachable');
        expect(localStorage.length).toBe(0);
      });

      it('sends no request when there were no tokens, but still wipes the storage and goes to /login', async () => {
        localStorage.setItem('yavir.language', 'uk');
        expect(await service.logout()).toBe('skipped');
        http.expectNone(logoutUrl());
        expect(localStorage.length).toBe(0);
        expect(navigate).toHaveBeenCalledWith('/login');
      });

      it('refreshes an expired access token first, so that the server accepts the logout and revokes the new refresh token', async () => {
        store(jwtWith(Math.floor(Date.now() / 1000) - 60), 'refresh-1');

        const result = service.logout();
        const refresh = http.expectOne(refreshUrl());
        const fresh = validAccess();
        refresh.flush({ accessToken: fresh, refreshToken: 'refresh-2' });

        const request = await vi.waitFor(() => http.expectOne(logoutUrl()));
        expect(request.request.body).toEqual({ refreshToken: 'refresh-2' });
        expect(request.request.headers.get('Authorization')).toBe(`Bearer ${fresh}`);
        request.flush(null, { status: 204, statusText: 'No Content' });
        expect(await result).toBe('ok');
      });

      it('goes on with the local logout when the refresh of an expired token fails', async () => {
        store(jwtWith(Math.floor(Date.now() / 1000) - 60), 'refresh-1');
        const result = service.logout();
        http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
        expect(await result).toBe('skipped');
        expect(localStorage.length).toBe(0);
        expect(navigate).toHaveBeenCalledWith('/login');
      });
    });

    it('signs out: forgets the tokens and goes to the login page', () => {
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      store('a', 'r');
      service.signOut();
      expect(service.hasTokens()).toBe(false);
      expect(accountsStore().accounts()).toHaveLength(0);
      expect(navigate).toHaveBeenCalledWith('/login');
    });
  });
});
