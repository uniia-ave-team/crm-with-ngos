import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { AccountRestoreService } from './account-restore.service';
import { AccountsService } from './accounts.service';

describe('AccountRestoreService', () => {
  const first = 'https://first.example:5065';
  const second = 'https://second.example:5065';
  let http: HttpTestingController;
  let accounts: AccountsService;
  let restore: AccountRestoreService;
  let connection: ConnectionService;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    accounts = TestBed.inject(AccountsService);
    // Акаунти додаються до створення підключення, тож його перший запит іде на сервер активного.
    accounts.add({ server: second, accessToken: 'a2', refreshToken: 'r2', identity: 'u2', email: 'b@b.org' });
    accounts.add({ server: first, accessToken: 'a1', refreshToken: 'r1', identity: 'u1', email: 'a@b.org' });
    connection = TestBed.inject(ConnectionService);
    http.expectOne(`${first}/health`).flush({ status: 'Healthy' });
    restore = TestBed.inject(AccountRestoreService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const idOf = (server: string) => accounts.accounts().find((account) => account.server === server)!.id;
  const waitFor = (url: string) => vi.waitFor(() => http.expectOne(url));

  describe('activate', () => {
    it('makes a valid account active after the server and the profile confirm it', async () => {
      const result = restore.activate(idOf(second));

      const health = await waitFor(`${second}/health`);
      // Поки йде перевірка, акаунт уже активний, а тимчасове перемикання — не вихід.
      expect(restore.searching()).toBe(true);
      expect(accounts.activeId()).toBe(idOf(second));
      health.flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', roles: [], customFields: [] });
      // Права підтягуються після профілю й нічого не вирішують.
      http.match(() => true);

      expect(await result).toBe('ok');
      expect(restore.searching()).toBe(false);
      expect(accounts.activeId()).toBe(idOf(second));
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: false });
      expect(connection.serverUrl()).toBe(second);
    });

    it('makes an account that was unavailable because its server did not answer valid again, when it answers now', async () => {
      accounts.markUnavailable(idOf(second));
      const result = restore.activate(idOf(second));

      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', roles: [], customFields: [] });
      http.match(() => true);

      expect(await result).toBe('ok');
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: false, unavailable: false });
      expect(accounts.activeId()).toBe(idOf(second));
    });

    it('keeps the previous account active and marks the other as unavailable (not outdated) when its server does not answer', async () => {
      const previous = accounts.activeId();
      const result = restore.activate(idOf(second));

      (await waitFor(`${second}/health`)).error(new ProgressEvent('error'));
      // Підключення повертається до сервера попереднього акаунта.
      (await waitFor(`${first}/health`)).flush({ status: 'Healthy' });

      expect(await result).toBe('unavailable');
      // Профіль попереднього акаунта, що скинувся, завантажується знову.
      http.expectOne(`${first}/api/v1/users/me`);
      expect(accounts.activeId()).toBe(previous);
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: false, unavailable: true });
      expect(accounts.accounts().find((a) => a.id === previous)).toMatchObject({ outdated: false });
      expect(restore.searching()).toBe(false);
    });

    it('keeps the previous account active and marks the other as outdated when the server rejects its tokens', async () => {
      const previous = accounts.activeId();
      const result = restore.activate(idOf(second));

      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({}, { status: 401, statusText: 'x' });
      (await waitFor(`${first}/health`)).flush({ status: 'Healthy' });

      expect(await result).toBe('stale');
      http.expectOne(`${first}/api/v1/users/me`);
      expect(accounts.activeId()).toBe(previous);
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: true, unavailable: false });
    });

    it('does nothing for an account that is not stored', async () => {
      const previous = accounts.activeId();
      expect(await restore.activate('unknown-key')).toBe('unavailable');
      expect(accounts.activeId()).toBe(previous);
      expect(accounts.accounts()).toHaveLength(2);
    });
  });

  describe('restore (the search for a valid account)', () => {
    const fail = () => new ProgressEvent('error');

    /** Відповідає помилкою на всі запити, що ще висять, поки пошук не завершиться; повертає їхні адреси. */
    async function failTheRest<T>(promise: Promise<T>): Promise<string[]> {
      const urls: string[] = [];
      let done = false;
      void promise.then(() => (done = true));
      await vi.waitFor(() => {
        for (const request of http.match(() => true)) {
          urls.push(request.request.url);
          request.error(fail());
        }
        expect(done).toBe(true);
      });
      return urls;
    }

    it('skips an unavailable account at the first pass when a valid one exists, and leaves its flag as it was', async () => {
      accounts.markUnavailable(idOf(second));
      const result = restore.restore();

      (await waitFor(`${first}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${first}/api/v1/users/me`)).flush({ id: 'u1', roles: [], customFields: [] });
      http.match(() => true);

      expect(await result).toBe('restored');
      expect(accounts.activeId()).toBe(idOf(first));
      http.expectNone(`${second}/health`);
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ unavailable: true });
    });

    it('when no account is available, checks the unavailable ones again, each starting with the health of its own server', async () => {
      accounts.markUnavailable(idOf(first));
      accounts.markUnavailable(idOf(second));
      expect(accounts.activeId()).toBeNull();
      const result = restore.restore();

      // Перший недоступний (за порядком додавання) відповів — він і стає активним, решту не чіпаємо.
      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', roles: [], customFields: [] });
      http.match(() => true);

      expect(await result).toBe('restored');
      expect(accounts.activeId()).toBe(idOf(second));
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: false, unavailable: false });
      expect(accounts.accounts().find((a) => a.id === idOf(first))).toMatchObject({ unavailable: true });
      http.expectNone(`${first}/health`);
    });

    it('goes on to the next unavailable account when the first one still does not answer', async () => {
      accounts.markUnavailable(idOf(first));
      accounts.markUnavailable(idOf(second));
      const result = restore.restore();

      (await waitFor(`${second}/health`)).error(fail());
      (await waitFor(`${first}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${first}/api/v1/users/me`)).flush({ id: 'u1', roles: [], customFields: [] });
      http.match(() => true);

      expect(await result).toBe('restored');
      expect(accounts.activeId()).toBe(idOf(first));
      expect(accounts.accounts().find((a) => a.id === idOf(first))).toMatchObject({ unavailable: false });
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ unavailable: true });
    });

    it('finds no available account when none of them answers: nothing is wiped, no account stays active', async () => {
      localStorage.setItem('yavir.language', 'uk');
      accounts.markUnavailable(idOf(first));
      accounts.markUnavailable(idOf(second));
      const result = restore.restore();

      await failTheRest(result);

      expect(await result).toBe('unavailable');
      expect(accounts.accounts()).toHaveLength(2);
      expect(accounts.accounts().every((a) => a.unavailable && !a.outdated)).toBe(true);
      expect(accounts.activeId()).toBeNull();
      expect(localStorage.getItem('yavir.language')).toBe('uk');
      expect(accounts.accounts().map((a) => a.refreshToken).sort()).toEqual(['r1', 'r2']);
      expect(restore.searching()).toBe(false);
    });

    it('does not check again, in the second pass, the accounts that failed to answer at the first one', async () => {
      const result = restore.restore();

      const urls = await failTheRest(result);

      expect(await result).toBe('unavailable');
      const health = (server: string) => urls.filter((url) => url === `${server}/health`).length;
      expect(health(first)).toBe(1);
      expect(health(second)).toBe(1);
      expect(accounts.accounts().every((a) => a.unavailable && !a.outdated)).toBe(true);
    });

    it('marks an account whose tokens the server rejected as outdated, never checks the outdated ones, and returns none', async () => {
      accounts.markOutdated(idOf(second));
      const result = restore.restore();

      (await waitFor(`${first}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${first}/api/v1/users/me`)).flush({}, { status: 401, statusText: 'x' });
      await failTheRest(result);

      expect(await result).toBe('none');
      expect(accounts.accounts().every((a) => a.outdated && !a.unavailable)).toBe(true);
      expect(accounts.accounts()).toHaveLength(2);
      expect(accounts.activeId()).toBeNull();
      http.expectNone(`${second}/health`);
    });

    it('returns none without a single request when every account is outdated', async () => {
      accounts.markOutdated(idOf(first));
      accounts.markOutdated(idOf(second));

      expect(await restore.restore()).toBe('none');
      http.expectNone(() => true);
      expect(accounts.accounts()).toHaveLength(2);
    });
  });
});
