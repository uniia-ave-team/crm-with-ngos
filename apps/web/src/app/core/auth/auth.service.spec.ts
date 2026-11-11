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

  it('adopts a pair of tokens for the given server as the active account, whichever server is current', () => {
    const accounts = TestBed.inject(AccountsService);
    accounts.add({ server: 'https://other.example:5065', accessToken: 'a0', refreshToken: 'r0', identity: 'someone' });

    service.adoptTokens('https://invited.example:5065', { accessToken: 'access', refreshToken: 'refresh' }, ' New@B.org ');

    expect(accounts.accounts()).toHaveLength(2);
    expect(accounts.active()).toMatchObject({
      server: 'https://invited.example:5065',
      accessToken: 'access',
      refreshToken: 'refresh',
      email: 'New@B.org',
      identity: 'new@b.org',
    });
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

    it('marks the account outdated when the server rejects its tokens; as the only account it stays in the memory, but is no longer active', async () => {
      store('a', 'r');
      localStorage.setItem('yavir.language', 'uk');
      const result = service.refresh();
      http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(await result).toBe('unauthorized');
      expect(service.hasTokens()).toBe(false);
      expect(accountsStore().accounts()).toHaveLength(1);
      expect(accountsStore().accounts()[0]).toMatchObject({ outdated: true, unavailable: false });
      expect(accountsStore().activeId()).toBeNull();
      expect(localStorage.getItem('yavir.language')).toBe('uk');
    });

    it('leaves no active account when the active one is rejected, and does not pick another one', async () => {
      const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
      store('a', 'r');
      const active = accountsStore().activeId()!;

      const result = service.refresh();
      http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
      expect(await result).toBe('unauthorized');

      // Застарілий більше не активний, а інший чинний сам не підхоплюється: активного нема.
      expect(accountsStore().activeId()).toBeNull();
      expect(service.hasTokens()).toBe(false);
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

      describe('refreshStored (at once, whatever the age of the tokens)', () => {
        it('exchanges the tokens of a fresh unavailable account and makes it available again', async () => {
          const { inactive, active } = withInactive(0);
          accountsStore().update(inactive, { unavailable: true });

          const result = service.refreshStored(inactive);
          const request = http.expectOne(otherRefreshUrl);
          expect(request.request.body).toEqual({ accessToken: 'ia', refreshToken: 'ir' });
          request.flush({ accessToken: 'ia2', refreshToken: 'ir2' });
          expect(await result).toBe('ok');

          const record = JSON.parse(localStorage.getItem(`yavir.account.${inactive}`)!);
          expect(record).toMatchObject({ accessToken: 'ia2', refreshToken: 'ir2', outdated: false, unavailable: false });
          expect(accountsStore().activeId()).toBe(active);
        });

        it('leaves the account unavailable, with its tokens, when the server still does not answer', async () => {
          const { inactive } = withInactive(0);
          accountsStore().update(inactive, { unavailable: true });
          const result = service.refreshStored(inactive);
          http.expectOne(otherRefreshUrl).error(new ProgressEvent('error'));
          expect(await result).toBe('unreachable');
          expect(accountsStore().accounts().find((a) => a.id === inactive)).toMatchObject({
            unavailable: true,
            outdated: false,
            refreshToken: 'ir',
          });
        });

        it('marks the account outdated when the server rejects its tokens', async () => {
          const { inactive } = withInactive(0);
          accountsStore().update(inactive, { unavailable: true });
          const result = service.refreshStored(inactive);
          http.expectOne(otherRefreshUrl).flush({}, { status: 401, statusText: 'Unauthorized' });
          expect(await result).toBe('unauthorized');
          expect(accountsStore().accounts().find((a) => a.id === inactive)).toMatchObject({ outdated: true, unavailable: false });
        });

        it('skips the active account, an outdated one and an unknown one, without a request', async () => {
          const { inactive, active } = withInactive(0);
          expect(await service.refreshStored(active)).toBe('skipped');
          expect(await service.refreshStored('unknown')).toBe('skipped');
          accountsStore().update(inactive, { outdated: true });
          expect(await service.refreshStored(inactive)).toBe('skipped');
          http.expectNone(otherRefreshUrl);
        });
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
      const otherServer = 'http://other.example.org:5065';
      let navigate: ReturnType<typeof vi.spyOn>;

      beforeEach(() => {
        navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      });

      /** Акаунт на іншому сервері, доданий до того, як `store` зробить активним основний. */
      const addOther = (extra: Partial<{ outdated: boolean; unavailable: boolean }> = {}) => {
        const other = accountsStore().add({ server: otherServer, accessToken: 'other-access', refreshToken: 'other-refresh', identity: 'user-2' });
        if (extra.outdated || extra.unavailable) {
          accountsStore().update(other.id, extra);
        }
        return other;
      };

      it('removes only the active account, leaves the rest of the memory, goes to /login when nothing is left, and revokes the refresh token', async () => {
        const access = validAccess();
        store(access, 'refresh-1');
        const id = accountsStore().activeId()!;
        localStorage.setItem('yavir.language', 'uk');
        localStorage.setItem('yavir.serverAddress', 'somewhere');

        const result = service.logout();
        // Видалення відбувається до відповіді сервера.
        expect(localStorage.getItem(`yavir.account.${id}`)).toBeNull();
        expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([]);
        expect(localStorage.getItem('yavir.activeAccount')).toBeNull();
        expect(localStorage.getItem('yavir.language')).toBe('uk');
        expect(localStorage.getItem('yavir.serverAddress')).toBe('somewhere');
        expect(service.hasTokens()).toBe(false);

        const request = http.expectOne(logoutUrl());
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ refreshToken: 'refresh-1' });
        expect(request.request.headers.get('Authorization')).toBe(`Bearer ${access}`);
        request.flush(null, { status: 204, statusText: 'No Content' });
        expect(await result).toBe('ok');
        await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/login'));
      });

      it('keeps the other accounts untouched and, as none of them is valid, goes to /relogin with no active account', async () => {
        const other = addOther({ unavailable: true });
        store(validAccess(), 'refresh-1');
        const id = accountsStore().activeId()!;

        const result = service.logout();
        // Другий акаунт недоступний: його сервер перевіряється, але мовчить.
        (await vi.waitFor(() => http.expectOne(`${otherServer}/health`))).error(new ProgressEvent('error'));
        http.expectOne(logoutUrl()).flush(null, { status: 204, statusText: 'No Content' });
        await result;
        // Далі підключення вертається до звичайної цілі, і лише тоді пошук завершено.
        await vi.waitFor(() => {
          http.match(() => true).forEach((request) => request.error(new ProgressEvent('error')));
          expect(navigate).toHaveBeenCalledWith('/relogin');
        });

        expect(accountsStore().accounts().map((a) => a.id)).toEqual([other.id]);
        expect(accountsStore().accounts()[0]).toMatchObject({ unavailable: true, refreshToken: 'other-refresh' });
        expect(accountsStore().activeId()).toBeNull();
        expect(localStorage.getItem(`yavir.account.${id}`)).toBeNull();
      });

      it('makes another valid account the active one and opens the page again, when one is found', async () => {
        const other = addOther();
        store(validAccess(), 'refresh-1');
        const id = accountsStore().activeId()!;

        const result = service.logout();
        http.expectOne(logoutUrl()).flush(null, { status: 204, statusText: 'No Content' });
        (await vi.waitFor(() => http.expectOne(`${otherServer}/health`))).flush({ status: 'Healthy' });
        (await vi.waitFor(() => http.expectOne(`${otherServer}/api/v1/users/me`))).flush({ id: 'u2', roles: [], customFields: [] });
        http.match(() => true);
        await result;

        await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/planner', { onSameUrlNavigation: 'reload' }));
        expect(accountsStore().activeId()).toBe(other.id);
        expect(accountsStore().accounts().map((a) => a.id)).toEqual([other.id]);
        expect(localStorage.getItem(`yavir.account.${id}`)).toBeNull();
        expect(navigate).not.toHaveBeenCalledWith('/login');
        expect(navigate).not.toHaveBeenCalledWith('/relogin');
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
        expect(accountsStore().accounts()).toHaveLength(0);
        expect(service.hasTokens()).toBe(false);
      });

      it('still ends locally when the server does not answer', async () => {
        store(validAccess(), 'refresh-1');
        const result = service.logout();
        http.expectOne(logoutUrl()).error(new ProgressEvent('error'));
        expect(await result).toBe('unreachable');
        expect(accountsStore().accounts()).toHaveLength(0);
      });

      it('sends no request when there is no account, and goes to /login', async () => {
        localStorage.setItem('yavir.language', 'uk');
        expect(await service.logout()).toBe('skipped');
        http.expectNone(logoutUrl());
        expect(localStorage.getItem('yavir.language')).toBe('uk');
        await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/login'));
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

      it('goes on with the local logout, removing that very account, when the refresh of an expired token fails', async () => {
        store(jwtWith(Math.floor(Date.now() / 1000) - 60), 'refresh-1');
        const result = service.logout();
        http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });
        expect(await result).toBe('skipped');
        // Невдале оновлення зняло з акаунта активність, але вийшли саме з нього.
        expect(accountsStore().accounts()).toHaveLength(0);
        http.expectNone(logoutUrl());
        await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/login'));
      });
    });

    describe('expireSession (the refresh of the active account failed)', () => {
      let navigate: ReturnType<typeof vi.spyOn>;
      beforeEach(() => {
        navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      });

      it('keeps the account, marks it outdated for rejected tokens, leaves no active account and goes to /relogin', () => {
        localStorage.setItem('yavir.language', 'uk');
        store('a', 'r');
        const id = accountsStore().activeId()!;
        service.expireSession(id, 'unauthorized');

        expect(service.hasTokens()).toBe(false);
        expect(accountsStore().accounts()).toHaveLength(1);
        expect(accountsStore().accounts()[0]).toMatchObject({ id, outdated: true, unavailable: false, refreshToken: 'r' });
        expect(accountsStore().activeId()).toBeNull();
        expect(localStorage.getItem('yavir.language')).toBe('uk');
        expect(navigate).toHaveBeenCalledWith('/relogin');
      });

      it('marks the account unavailable, not outdated, when the server did not answer', () => {
        store('a', 'r');
        const id = accountsStore().activeId()!;
        service.expireSession(id, 'unreachable');
        expect(accountsStore().accounts()[0]).toMatchObject({ outdated: false, unavailable: true });
        expect(accountsStore().activeId()).toBeNull();
        expect(navigate).toHaveBeenCalledWith('/relogin');
      });

      it('does not hand the activity to another valid account, which stays as it was', () => {
        const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
        store('a', 'r');
        const id = accountsStore().activeId()!;
        service.expireSession(id, 'invalid');

        expect(accountsStore().activeId()).toBeNull();
        expect(accountsStore().accounts().find((a) => a.id === other.id)).toMatchObject({ outdated: false, unavailable: false });
        expect(navigate).toHaveBeenCalledWith('/relogin');
      });

      it('goes nowhere when another account became active while the refresh of this one was running', () => {
        store('a', 'r');
        const failed = accountsStore().activeId()!;
        const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
        expect(accountsStore().activeId()).toBe(other.id);

        service.expireSession(failed, 'unauthorized');
        expect(accountsStore().activeId()).toBe(other.id);
        expect(accountsStore().accounts().find((a) => a.id === failed)?.outdated).toBe(true);
        expect(navigate).not.toHaveBeenCalled();
      });
    });
  });
});
