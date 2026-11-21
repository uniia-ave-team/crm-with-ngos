import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { AuthService } from './auth.service';

/** Сховище браузера переповнене: записи акаунтів не пишуться (решта, як-от мова, — пишеться). */
function fillStorageForAccounts(): void {
  const setItem = Storage.prototype.setItem;
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(function (this: Storage, key: string, value: string) {
    if (key.startsWith('yavir.account.')) {
      throw new DOMException('The quota has been exceeded.', 'QuotaExceededError');
    }
    setItem.call(this, key, value);
  });
}

describe('AuthService', () => {
  let http: HttpTestingController;
  let service: AuthService;
  let url: string;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    // Пошук API на старті (origin сторінки й порт 5065): обидва запити забираємо без відповіді.
    http.match((request) => request.url.endsWith('/health'));
    service = TestBed.inject(AuthService);
    url = `${connection.serverUrl()}/api/v1/auth/login`;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('when the browser cannot store the account (its storage is full)', () => {
    it('does not sign in: no account is added, the active one stays, and the new tokens are revoked on the server', async () => {
      const accounts = TestBed.inject(AccountsService);
      const previous = accounts.add({ server: TestBed.inject(ConnectionService).serverUrl(), accessToken: 'old', refreshToken: 'old-r', identity: 'other' });
      fillStorageForAccounts();

      const result = service.login('a@b.org', 'Abcde1');
      http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });

      expect(await result).toBe('storageFull');
      expect(accounts.accounts().map((a) => a.id)).toEqual([previous.id]);
      expect(accounts.activeId()).toBe(previous.id);
      // Про це каже форма входу, тож спливаючого попередження нема.
      expect(accounts.storageFull()).toBe(false);
      const logout = http.expectOne(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/auth/logout`);
      expect(logout.request.body).toEqual({ refreshToken: 'refresh' });
      expect(logout.request.headers.get('Authorization')).toBe('Bearer access');
    });

    it('keeps the hint for a new tab (the account seen last) when there was no active account in this one', async () => {
      const accounts = TestBed.inject(AccountsService);
      const seen = accounts.add({ server: TestBed.inject(ConnectionService).serverUrl(), accessToken: 'old', refreshToken: 'old-r', identity: 'other' });
      // Вкладка без активного акаунта (як на /login чи /relogin); підказка — акаунт, який бачили останнім.
      accounts.setActive(null);
      fillStorageForAccounts();

      const result = service.login('a@b.org', 'Abcde1');
      http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });

      expect(await result).toBe('storageFull');
      expect(accounts.activeId()).toBeNull();
      expect(localStorage.getItem('yavir.lastActiveAccount')).toBe(seen.id);
      http.match(() => true);
    });

    it('leaves a stored account as it is when the sign-in would overwrite it', async () => {
      const accounts = TestBed.inject(AccountsService);
      const saved = accounts.add({ server: TestBed.inject(ConnectionService).serverUrl(), accessToken: 'old', refreshToken: 'old-r', identity: 'a@b.org', email: 'a@b.org' });
      fillStorageForAccounts();

      const result = service.login('a@b.org', 'Abcde1', undefined, saved.id);
      http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });

      expect(await result).toBe('storageFull');
      expect(accounts.accounts()).toHaveLength(1);
      expect(accounts.active()).toMatchObject({ id: saved.id, accessToken: 'old', refreshToken: 'old-r' });
      http.match(() => true);
    });
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
    expect(sessionStorage.getItem('yavir.activeAccount')).toBe(id);
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

  it('still logs in, keeping the account in memory, when the browser gives no storage at all (the data of sites is blocked)', async () => {
    const denied = () => {
      throw new Error('denied');
    };
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(denied);
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(denied);
    // Сервіси створюються вже без сховища — так, як у такому браузері.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    http.match(() => true);
    service = TestBed.inject(AuthService);

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

  it.each([
    ['has no tokens', {}],
    ['misses the refresh token', { accessToken: 'access' }],
    ['has an empty token', { accessToken: '', refreshToken: 'refresh' }],
    ['has a token of another type', { accessToken: 'access', refreshToken: 7 }],
    ['is not an object', null],
  ])('fails, adding no account, when the answer %s: it is not the pair of the contract', async (_name, body) => {
    const result = service.login('a@b.org', 'Abcde1');
    http.expectOne(url).flush(body);

    expect(await result).toBe('error');
    expect(service.hasTokens()).toBe(false);
    expect(TestBed.inject(AccountsService).accounts()).toEqual([]);
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

    it('fails and keeps the tokens it had when the answer to the refresh has no pair of tokens', async () => {
      store('a', 'r');
      const result = service.refresh();
      http.expectOne(refreshUrl()).flush({ accessToken: 'a2' });

      expect(await result).toBe('error');
      expect(storedRecord()).toMatchObject({ accessToken: 'a', refreshToken: 'r', outdated: false });
    });

    it('refreshes the account it was asked for, even when another one became active meanwhile', async () => {
      store('a', 'r');
      const asked = accountsStore().activeId()!;
      const result = service.refresh();
      const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });

      const request = http.expectOne(refreshUrl());
      expect(request.request.body).toEqual({ accessToken: 'a', refreshToken: 'r' });
      request.flush({ accessToken: 'a2', refreshToken: 'r2' });

      expect(await result).toBe('ok');
      expect(accountsStore().activeId()).toBe(other.id);
      expect(accountsStore().accounts().map((a) => [a.id, a.refreshToken])).toEqual([
        [asked, 'r2'],
        [other.id, 'rb'],
      ]);
    });

    it('marks the account it was asked for when its tokens are rejected, not the one that became active meanwhile', async () => {
      store('a', 'r');
      const asked = accountsStore().activeId()!;
      const result = service.refresh();
      const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
      http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(await result).toBe('unauthorized');
      expect(accountsStore().accounts().map((a) => [a.id, a.outdated])).toEqual([
        [asked, true],
        [other.id, false],
      ]);
      expect(accountsStore().activeId()).toBe(other.id);
    });

    it('keeps a refresh of its own for every account', async () => {
      store('a', 'r');
      const first = accountsStore().activeId()!;
      const second = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' }).id;

      const results = [service.refresh(first), service.refresh(second)];
      const requests = http.match(refreshUrl());
      expect(requests.map((r) => r.request.body.refreshToken)).toEqual(['r', 'rb']);
      requests[0].flush({ accessToken: 'a2', refreshToken: 'r2' });
      requests[1].flush({ accessToken: 'b2', refreshToken: 'rb2' });

      expect(await Promise.all(results)).toEqual(['ok', 'ok']);
    });

    it('answers "unauthorized" without a request for an account that is not there', async () => {
      expect(await service.refresh('ghost')).toBe('unauthorized');
      expect(await service.refresh()).toBe('unauthorized');
      http.expectNone(refreshUrl());
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

        expect(await service.refreshStoredIfDue(inactive)).toBe('ok');
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

      // Інша вкладка вийшла: запис та індекс зникли.
      localStorage.removeItem(`yavir.account.${id}`);
      localStorage.setItem('yavir.accounts', '[]');
      window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.accounts' }));
      expect(service.hasTokens()).toBe(false);
    });

    // Сам сценарій виходу (пошук іншого акаунта, перехід) — в AccountRestoreService.signOut.
    describe('the two halves of the sign-out: forgetActive and revoke', () => {
      const logoutUrl = () => `${server()}/api/v1/auth/logout`;
      const validAccess = () => jwtWith(Math.floor(Date.now() / 1000) + 600);

      describe('forgetActive', () => {
        it('removes only the active account, leaves the rest of the memory and gives its record with the tokens', async () => {
          const access = validAccess();
          const home = server();
          store(access, 'refresh-1');
          const id = accountsStore().activeId()!;
          const other = accountsStore().add({ server: 'http://other.example.org:5065', accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
          accountsStore().setActive(id);
          localStorage.setItem('yavir.language', 'uk');

          const forgotten = await service.forgetActive();

          expect(forgotten).toMatchObject({ id, server: home, accessToken: access, refreshToken: 'refresh-1' });
          expect(localStorage.getItem(`yavir.account.${id}`)).toBeNull();
          expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([other.id]);
          expect(accountsStore().activeId()).toBeNull();
          expect(localStorage.getItem('yavir.language')).toBe('uk');
          expect(service.hasTokens()).toBe(false);
          // Саме забування нічого не відкликає: це друга половина.
          http.expectNone(logoutUrl());
        });

        it('gives null and changes nothing when there is no active account', async () => {
          const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
          accountsStore().setActive(null);
          expect(await service.forgetActive()).toBeNull();
          expect(accountsStore().accounts().map((a) => a.id)).toEqual([other.id]);
        });

        it('refreshes an expired access token first and gives the new pair, so that the server accepts the revoking', async () => {
          store(jwtWith(Math.floor(Date.now() / 1000) - 60), 'refresh-1');
          const forgetting = service.forgetActive();
          const fresh = validAccess();
          http.expectOne(refreshUrl()).flush({ accessToken: fresh, refreshToken: 'refresh-2' });

          expect(await forgetting).toMatchObject({ accessToken: fresh, refreshToken: 'refresh-2' });
          expect(accountsStore().accounts()).toHaveLength(0);
        });

        it('still forgets that very account when the refresh of its expired token fails, and gives it marked as outdated', async () => {
          store(jwtWith(Math.floor(Date.now() / 1000) - 60), 'refresh-1');
          const forgetting = service.forgetActive();
          http.expectOne(refreshUrl()).flush({}, { status: 401, statusText: 'Unauthorized' });

          // Невдале оновлення зняло з акаунта активність, але забуто саме його.
          expect(await forgetting).toMatchObject({ outdated: true, refreshToken: 'refresh-1' });
          expect(accountsStore().accounts()).toHaveLength(0);
        });
      });

      describe('revoke', () => {
        const forgotten = async (access = validAccess(), refresh = 'refresh-1') => {
          store(access, refresh);
          return (await service.forgetActive())!;
        };

        it('revokes the refresh token on the server of the account, with a header made of its own tokens', async () => {
          const access = validAccess();
          const account = await forgotten(access);
          const result = service.revoke(account);

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
        ])('tells how the server answered HTTP %i', async (status, expected) => {
          const result = service.revoke(await forgotten());
          http.expectOne(logoutUrl()).flush({}, { status, statusText: 'x' });
          expect(await result).toBe(expected);
        });

        it('tells that the server did not answer', async () => {
          const result = service.revoke(await forgotten());
          http.expectOne(logoutUrl()).error(new ProgressEvent('error'));
          expect(await result).toBe('unreachable');
        });

        it('sends nothing when there is no account, no tokens, or the server has already rejected them', async () => {
          const account = await forgotten();
          expect(await service.revoke(null)).toBe('skipped');
          expect(await service.revoke({ ...account, accessToken: '' })).toBe('skipped');
          expect(await service.revoke({ ...account, refreshToken: '' })).toBe('skipped');
          expect(await service.revoke({ ...account, outdated: true })).toBe('skipped');
          http.expectNone(logoutUrl());
        });
      });
    });

    describe('expireSession (the refresh of an account failed; where to go is not its business)', () => {
      let navigate: ReturnType<typeof vi.spyOn>;
      beforeEach(() => {
        navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      });

      it('keeps the account, marks it outdated for rejected tokens and leaves no active account', () => {
        localStorage.setItem('yavir.language', 'uk');
        store('a', 'r');
        const id = accountsStore().activeId()!;
        service.expireSession(id, 'unauthorized');

        expect(service.hasTokens()).toBe(false);
        expect(accountsStore().accounts()).toHaveLength(1);
        expect(accountsStore().accounts()[0]).toMatchObject({ id, outdated: true, unavailable: false, refreshToken: 'r' });
        expect(accountsStore().activeId()).toBeNull();
        expect(localStorage.getItem('yavir.language')).toBe('uk');
        expect(navigate).not.toHaveBeenCalled();
      });

      it('marks the account unavailable, not outdated, when the server did not answer', () => {
        store('a', 'r');
        const id = accountsStore().activeId()!;
        service.expireSession(id, 'unreachable');
        expect(accountsStore().accounts()[0]).toMatchObject({ outdated: false, unavailable: true });
        expect(accountsStore().activeId()).toBeNull();
        expect(navigate).not.toHaveBeenCalled();
      });

      it('does not hand the activity to another valid account, which stays as it was', () => {
        const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
        store('a', 'r');
        const id = accountsStore().activeId()!;
        service.expireSession(id, 'invalid');

        expect(accountsStore().activeId()).toBeNull();
        expect(accountsStore().accounts().find((a) => a.id === other.id)).toMatchObject({ outdated: false, unavailable: false });
      });

      it('marks the account whose session failed, not the one that became active meanwhile', () => {
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

    describe('demo session (DEMO_SESSION_EXPIRED)', () => {
      const expired = { title: 'Demo session expired', status: 401, errorCode: 'DEMO_SESSION_EXPIRED' };
      let navigate: ReturnType<typeof vi.spyOn>;
      beforeEach(() => {
        navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      });

      it('forgets the active account and raises the flag for the login page; where to go is not its business', () => {
        localStorage.setItem('yavir.language', 'uk');
        store('a', 'r');
        const id = accountsStore().activeId()!;
        const check = vi.spyOn(TestBed.inject(ConnectionService), 'checkApiIfServerChanged').mockResolvedValue('healthy');

        service.endDemoSession(id);

        expect(accountsStore().accounts()).toEqual([]);
        expect(localStorage.getItem(`yavir.account.${id}`)).toBeNull();
        expect(service.hasTokens()).toBe(false);
        expect(service.demoSessionEnded()).toBe(true);
        // Підключення перевіряється заново, лише якщо без акаунта сервер уже інший.
        expect(check).toHaveBeenCalledTimes(1);
        expect(navigate).not.toHaveBeenCalled();
        // Решта памʼяті (мова тощо) лишається.
        expect(localStorage.getItem('yavir.language')).toBe('uk');
        // Сервер про вихід не питаємо: сеансу там уже нема.
        http.expectNone(`${server()}/api/v1/auth/logout`);
      });

      it('forgets an inactive account quietly: the active one stays, and no flag is raised', () => {
        const inactive = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
        store('a', 'r');
        const active = accountsStore().activeId();

        service.endDemoSession(inactive.id);

        expect(accountsStore().accounts().map((a) => a.id)).toEqual([active]);
        expect(accountsStore().activeId()).toBe(active);
        expect(service.demoSessionEnded()).toBe(false);
      });

      it('does nothing for an account that is already gone', () => {
        store('a', 'r');
        service.endDemoSession('unknown');
        service.endDemoSession(null);
        expect(service.demoSessionEnded()).toBe(false);
        expect(accountsStore().accounts()).toHaveLength(1);
      });

      it('ends the demo session when the refresh answers DEMO_SESSION_EXPIRED, even if another tab stored a new pair', async () => {
        store('a', 'r');
        const result = service.refresh();
        const request = http.expectOne(refreshUrl());
        writeFromOtherTab('a-from-other-tab', 'r-from-other-tab');
        request.flush(expired, { status: 401, statusText: 'Unauthorized' });

        expect(await result).toBe('unauthorized');
        expect(accountsStore().accounts()).toEqual([]);
        expect(service.demoSessionEnded()).toBe(true);
      });

      it('ends the demo session of an inactive account whose background refresh answers DEMO_SESSION_EXPIRED, quietly', async () => {
        const inactive = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
        store('a', 'r');
        const active = accountsStore().activeId();

        const result = service.refreshStored(inactive.id);
        http.expectOne(refreshUrl()).flush(expired, { status: 401, statusText: 'Unauthorized' });

        expect(await result).toBe('unauthorized');
        expect(accountsStore().accounts().map((a) => a.id)).toEqual([active]);
        expect(service.demoSessionEnded()).toBe(false);
      });

      it('lowers the flag at the next login', async () => {
        store('a', 'r');
        service.endDemoSession(accountsStore().activeId());
        expect(service.demoSessionEnded()).toBe(true);

        const result = service.login('a@b.org', 'Abcde1');
        http.expectOne(url).flush({ accessToken: 'access', refreshToken: 'refresh' });
        expect(await result).toBe('ok');
        expect(service.demoSessionEnded()).toBe(false);
      });

      it('lowers the flag as soon as another account becomes active (the search for a valid account, a switch)', () => {
        const other = accountsStore().add({ server: server(), accessToken: 'b', refreshToken: 'rb', identity: 'user-2' });
        store('a', 'r');
        service.endDemoSession(accountsStore().activeId());
        expect(service.demoSessionEnded()).toBe(true);

        accountsStore().setActive(other.id);
        expect(service.demoSessionEnded()).toBe(false);
        // Знову без активного — це вже не кінець демо-сеансу.
        accountsStore().setActive(null);
        expect(service.demoSessionEnded()).toBe(false);
      });
    });
  });

  describe('a busy demo at login', () => {
    const demo = { accounts: [], sessionMinutes: 60 };

    /** Підключення до сервера в демо-режимі (чи звичайного): нова перевірка й статус системи. */
    function connect(demoInfo: typeof demo | null): void {
      const connection = TestBed.inject(ConnectionService);
      void connection.checkApi();
      http.expectOne(connection.apiHealthUrl()).flush({ status: 'Healthy' });
      http
        .expectOne(`${connection.serverUrl()}/api/v1/system/status`)
        .flush({ hasAdmin: true, hasNgo: true, isSetupComplete: true, demo: demoInfo });
    }

    it('reports "busy" for 503 DEMO_CAPACITY_REACHED and adds no account', async () => {
      const result = service.login('a@b.org', 'Abcde1');
      http
        .expectOne(url)
        .flush({ status: 503, errorCode: 'DEMO_CAPACITY_REACHED' }, { status: 503, statusText: 'Service Unavailable', headers: { 'Retry-After': '60' } });

      expect(await result).toBe('busy');
      expect(TestBed.inject(AccountsService).accounts()).toEqual([]);
    });

    it('reports "busy" for 429 on a demo server', async () => {
      connect(demo);
      const result = service.login('a@b.org', 'Abcde1');
      http.expectOne(url).flush({ status: 429 }, { status: 429, statusText: 'Too Many Requests' });
      expect(await result).toBe('busy');
    });

    it('keeps 429 an ordinary error outside the demo, and 503 without the code as well', async () => {
      connect(null);
      const limited = service.login('a@b.org', 'Abcde1');
      http.expectOne(url).flush({ status: 429 }, { status: 429, statusText: 'Too Many Requests' });
      expect(await limited).toBe('error');

      const down = service.login('a@b.org', 'Abcde1');
      http.expectOne(url).flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });
      expect(await down).toBe('error');
    });
  });
});
