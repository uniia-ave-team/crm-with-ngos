import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { CurrentUserService } from '../users/current-user.service';
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  let http: HttpTestingController;
  let accounts: AccountsService;
  let server: string;
  const profile = { id: 'user-1', email: 'a@b.org', firstName: 'Олена', lastName: 'Іваненко', avatarUrl: null, roles: [], customFields: [] };

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    accounts = TestBed.inject(AccountsService);
    server = TestBed.inject(ConnectionService).serverUrl();
    // Перша перевірка підключення (з конструктора ConnectionService).
    http.match(`${server}/health`);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const run = () =>
    TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as Promise<
      boolean | UrlTree
    >;

  const jwtAt = (seconds: number, sub = 'user-1') =>
    `h.${btoa(JSON.stringify({ sub, exp: Math.floor(Date.now() / 1000) + seconds })).replace(/=+$/, '')}.s`;

  /** Додає акаунт (останній доданий стає активним). */
  const addAccount = (identity: string, access: string, refresh: string, accountServer = server) =>
    accounts.add({ server: accountServer, accessToken: access, refreshToken: refresh, identity });

  /** Відповідає на першу перевірку підключення, що її запускає пошук. */
  const answerHealth = async (answer: 'healthy' | 'down', accountServer = server) => {
    const request = await vi.waitFor(() => {
      const open = http.match({ url: `${accountServer}/health` }).filter((r) => !r.cancelled);
      if (open.length === 0) {
        throw new Error('no health request');
      }
      return open[0];
    });
    if (answer === 'healthy') {
      request.flush({ status: 'Healthy' });
    } else {
      request.error(new ProgressEvent('error'));
    }
  };

  it('redirects to /login, with no requests, when there are no accounts', async () => {
    const result = (await run()) as UrlTree;
    expect(result.toString()).toBe('/login');
  });

  describe('when the current account is already confirmed in this page session', () => {
    async function confirm() {
      addAccount('user-1', jwtAt(600), 'r1');
      const user = TestBed.inject(CurrentUserService);
      const confirmed = user.ensure();
      http.expectOne(`${server}/api/v1/users/me`).flush(profile);
      await confirmed;
      http.match(`${server}/api/v1/ngo`);
      return user;
    }

    it('lets the user in at once, without the search of the accounts', async () => {
      await confirm();

      expect(await run()).toBe(true);
      http.expectNone(`${server}/health`);
      http.expectOne(`${server}/api/v1/users/me`);
    });

    it('asks for the profile again on every page change and takes the changes made in the database', async () => {
      const user = await confirm();

      expect(await run()).toBe(true);
      http.expectOne(`${server}/api/v1/users/me`).flush({ ...profile, firstName: ' ', lastName: ' ' });
      await vi.waitFor(() => expect(user.user()).toMatchObject({ firstName: ' ', lastName: ' ' }));
      http.match(`${server}/api/v1/ngo`);

      // Ще одна зміна сторінки — ще один запит.
      expect(await run()).toBe(true);
      http.expectOne(`${server}/api/v1/users/me`).flush({ ...profile, firstName: '  ' });
      await vi.waitFor(() => expect(user.user()).toMatchObject({ firstName: '  ' }));
    });

    it('does not hold the page back while the profile is being requested, nor lose it when the request fails', async () => {
      const user = await confirm();

      // Запит ще не відповів, а сторінку вже пущено.
      expect(await run()).toBe(true);
      const request = http.expectOne(`${server}/api/v1/users/me`);
      request.error(new ProgressEvent('error'));
      await vi.waitFor(() => expect(console.warn).toHaveBeenCalled());

      expect(user.user()).toMatchObject({ id: 'user-1' });
      expect(accounts.active()).not.toBeNull();
    });
  });

  it('searches the saved accounts: the first valid one becomes the current one and its data is loaded', async () => {
    addAccount('user-1', jwtAt(600), 'r1');
    const result = run();

    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/users/me`))).flush(profile);
    expect(await result).toBe(true);

    expect(accounts.activeId()).toBe(AccountsService.accountId(server, 'user-1'));
    expect(TestBed.inject(CurrentUserService).user()).toMatchObject({ firstName: 'Олена' });
    expect(accounts.active()).toMatchObject({ firstName: 'Олена', lastName: 'Іваненко' });
  });

  it('refreshes an expired token during the search', async () => {
    addAccount('user-1', jwtAt(-60), 'r1');
    const result = run();
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`))).flush({
      accessToken: jwtAt(600),
      refreshToken: 'r2',
    });
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/users/me`))).flush(profile);
    expect(await result).toBe(true);
    expect(accounts.active()?.refreshToken).toBe('r2');
  });

  it('redirects to /login when the only account is outdated (and the memory is cleared)', async () => {
    addAccount('user-1', jwtAt(-60), 'r1');
    const result = run();
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    await answerHealth('healthy');

    expect(((await result) as UrlTree).toString()).toBe('/login');
    expect(localStorage.length).toBe(0);
    expect(accounts.accounts()).toHaveLength(0);
  });

  it('takes the next account when the first one is outdated, leaving the first one marked', async () => {
    addAccount('stale', jwtAt(-60, 'stale'), 'r-stale');
    addAccount('good', jwtAt(600, 'good'), 'r-good');
    accounts.setActive(AccountsService.accountId(server, 'stale'));

    const result = run();
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/users/me`))).flush({ ...profile, id: 'good' });

    expect(await result).toBe(true);
    expect(accounts.activeId()).toBe(AccountsService.accountId(server, 'good'));
    // У знімку чинного акаунта після /users/me з'явився `userId`.
    expect(accounts.accounts().map((a) => [a.userId, a.outdated])).toEqual([
      ['', true],
      ['good', false],
    ]);
  });

  it('redirects to /login when every account is outdated', async () => {
    const first = addAccount('a', jwtAt(-60, 'a'), 'ra');
    addAccount('b', jwtAt(600, 'b'), 'rb');
    accounts.markOutdated(first.id);
    accounts.markOutdated(AccountsService.accountId(server, 'b'));
    expect(((await run()) as UrlTree).toString()).toBe('/login');
  });

  it('treats an account whose server does not answer as outdated: the memory is wiped and the user goes to /login with the no-connection error', async () => {
    addAccount('user-1', jwtAt(600), 'r1', 'http://down.example.org:5065');
    localStorage.setItem('yavir.language', 'uk');
    const result = run();
    await answerHealth('down', 'http://down.example.org:5065');
    await answerHealth('healthy');

    expect(((await result) as UrlTree).toString()).toBe('/login?error=no-connection');
    expect(accounts.accounts()).toHaveLength(0);
    expect(localStorage.length).toBe(0);
  });

  it('goes to /login without the error when the servers answered and rejected every account', async () => {
    addAccount('user-1', jwtAt(-60), 'r1');
    const result = run();
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    await answerHealth('healthy');
    expect(((await result) as UrlTree).toString()).toBe('/login');
  });

  it('shows the no-connection error when one account was rejected and another one did not answer', async () => {
    addAccount('stale', jwtAt(-60, 'stale'), 'r-stale');
    addAccount('down', jwtAt(600, 'down'), 'r-down', 'http://down.example.org:5065');
    accounts.setActive(AccountsService.accountId(server, 'stale'));
    const result = run();
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/auth/refresh-token`))).flush({}, { status: 401, statusText: 'Unauthorized' });
    await answerHealth('down', 'http://down.example.org:5065');
    await answerHealth('healthy');

    expect(((await result) as UrlTree).toString()).toBe('/login?error=no-connection');
    expect(accounts.accounts()).toHaveLength(0);
  });

  it('wipes the memory when no account is valid and none was merely unreachable', async () => {
    // Усі позначені застарілими ще до пошуку (напр., запис, залишений іншою вкладкою).
    const first = addAccount('a', jwtAt(600, 'a'), 'ra');
    const second = addAccount('b', jwtAt(600, 'b'), 'rb');
    for (const account of [first, second]) {
      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...JSON.parse(localStorage.getItem(`yavir.account.${account.id}`)!), outdated: true }));
    }
    localStorage.setItem('yavir.language', 'uk');
    accounts.reload();
    expect(accounts.accounts().every((a) => a.outdated)).toBe(true);

    const result = run();
    expect(((await result) as UrlTree).toString()).toBe('/login');
    expect(localStorage.length).toBe(0);
    expect(accounts.accounts()).toHaveLength(0);
  });

  it('moves on to the next account when the first one is unreachable, and keeps the first one marked', async () => {
    addAccount('good', jwtAt(600, 'good'), 'r-good');
    addAccount('down', jwtAt(600, 'down'), 'r-down', 'http://down.example.org:5065');
    // Пошук починає з активного — недоступного.
    accounts.setActive(AccountsService.accountId('http://down.example.org:5065', 'down'));
    const result = run();
    await answerHealth('down', 'http://down.example.org:5065');
    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/users/me`))).flush({ ...profile, id: 'good' });

    expect(await result).toBe(true);
    expect(accounts.activeId()).toBe(AccountsService.accountId(server, 'good'));
    expect(accounts.accounts().map((a) => a.outdated)).toEqual([false, true]);
    // Недоступний сервер лишає причину в записі (для меню акаунтів).
    expect(accounts.accounts().map((a) => a.unavailable)).toEqual([false, true]);
  });

  it('shares one search between simultaneous navigations', async () => {
    addAccount('user-1', jwtAt(600), 'r1');
    const first = run();
    const second = run();

    await answerHealth('healthy');
    (await vi.waitFor(() => http.expectOne(`${server}/api/v1/users/me`))).flush(profile);
    expect(await first).toBe(true);
    expect(await second).toBe(true);
  });
});
