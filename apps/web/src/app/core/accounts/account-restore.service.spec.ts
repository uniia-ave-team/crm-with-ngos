import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { ConnectionService } from '../connection/connection.service';
import { CurrentUserService } from '../users/current-user.service';
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
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    accounts = TestBed.inject(AccountsService);
    // Акаунти додаються до створення підключення, тож його перший запит іде на сервер активного.
    accounts.add({ server: second, accessToken: 'a2', refreshToken: 'r2', identity: 'u2', email: 'b@b.org' });
    accounts.add({ server: first, accessToken: 'a1', refreshToken: 'r1', identity: 'u1', email: 'a@b.org' });
    connection = TestBed.inject(ConnectionService);
    http.expectOne(`${first}/health`).flush({ status: 'Healthy' });
    // Підключення є — сервіс одразу запитує статус системи; тут він ні на що не впливає.
    http.expectOne(`${first}/api/v1/system/status`);
    restore = TestBed.inject(AccountRestoreService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const idOf = (server: string) => accounts.accounts().find((account) => account.server === server)!.id;
  /**
   * Запити, на які ще можна відповісти. Скасовані (новий пошук API скасовує попередній, а знайдене
   * здорове API — менш пріоритетних кандидатів) лишаються в списку, але відповісти на них не можна.
   */
  const openRequests = () => http.match(() => true).filter((request) => !request.cancelled);
  const waitFor = (url: string) => vi.waitFor(() => http.expectOne(url));

  describe('the search and the switch by a tile go one at a time', () => {
    it('starts a search asked for during a switch only after the switch, and keeps switching on all that time', async () => {
      const switched = restore.activate(idOf(second));
      const health = await waitFor(`${second}/health`);
      // Під час перемикання хтось просить пошук (наприклад, страж сторінки).
      const searched = restore.restore();
      http.expectNone(`${first}/health`);
      expect(accounts.activeId()).toBe(idOf(second));

      health.flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', email: 'u2@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
      http.match((request) => request.url.includes('/permissions') || request.url.endsWith('/ngo'));
      expect(await switched).toBe('ok');
      expect(accounts.switching()).toBe(true);

      // Тепер пошук: активний першим — щойно перевірений акаунт, і профіль у нього вже є.
      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      expect(await searched).toBe('restored');
      expect(accounts.activeId()).toBe(idOf(second));
      expect(accounts.switching()).toBe(false);
    });

    it('starts a switch asked for during a search only after the search', async () => {
      const searched = restore.restore();
      const health = await waitFor(`${first}/health`);
      const switched = restore.activate(idOf(second));
      http.expectNone(`${second}/health`);
      expect(accounts.activeId()).toBe(idOf(first));

      health.flush({ status: 'Healthy' });
      (await waitFor(`${first}/api/v1/users/me`)).flush({ id: 'u1', email: 'u1@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
      http.match((request) => request.url.includes('/permissions') || request.url.endsWith('/ngo'));
      expect(await searched).toBe('restored');

      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', email: 'u2@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
      http.match((request) => request.url.includes('/permissions') || request.url.endsWith('/ngo'));
      expect(await switched).toBe('ok');
      expect(accounts.activeId()).toBe(idOf(second));
      expect(accounts.switching()).toBe(false);
    });
  });

  describe('activate', () => {
    it('makes a valid account active after the server and the profile confirm it', async () => {
      const result = restore.activate(idOf(second));

      const health = await waitFor(`${second}/health`);
      // Поки йде перевірка, акаунт уже активний, а тимчасове перемикання — не вихід.
      expect(accounts.switching()).toBe(true);
      expect(accounts.activeId()).toBe(idOf(second));
      health.flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', email: 'u2@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
      // Права підтягуються після профілю й нічого не вирішують.
      http.match(() => true);

      expect(await result).toBe('ok');
      expect(accounts.switching()).toBe(false);
      expect(accounts.activeId()).toBe(idOf(second));
      expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: false });
      expect(connection.serverUrl()).toBe(second);
    });

    it('makes an account that was unavailable because its server did not answer valid again, when it answers now', async () => {
      accounts.markUnavailable(idOf(second));
      const result = restore.activate(idOf(second));

      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', email: 'u2@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
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
      expect(accounts.switching()).toBe(false);
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

    it('without a previous active account, does not ask for any profile after a failed switch', async () => {
    accounts.setActive(null);
    const ensure = vi.spyOn(TestBed.inject(CurrentUserService), 'ensure');
    const result = restore.activate(idOf(second));

    let done = false;
    void result.then(() => (done = true));
    await vi.waitFor(() => {
      openRequests().forEach((request) => request.error(new ProgressEvent('error')));
      expect(done).toBe(true);
    });

    expect(await result).toBe('unavailable');
    expect(accounts.activeId()).toBeNull();
    expect(ensure).not.toHaveBeenCalled();
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
        for (const request of openRequests()) {
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
      (await waitFor(`${first}/api/v1/users/me`)).flush({ id: 'u1', email: 'u1@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
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
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', email: 'u2@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
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
      (await waitFor(`${first}/api/v1/users/me`)).flush({ id: 'u1', email: 'u1@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
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
      expect(accounts.switching()).toBe(false);
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

  describe('signOut (the active account is forgotten, then the same search as on entering a page)', () => {
    let navigate: ReturnType<typeof vi.spyOn>;
    const logoutUrl = (server: string) => `${server}/api/v1/auth/logout`;

    beforeEach(() => {
      navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    });

    /** Відповідає помилкою на всі запити, що ще висять, поки не станеться перехід. */
    async function failTheRestUntilNavigation(): Promise<void> {
      await vi.waitFor(() => {
        openRequests().forEach((request) => request.error(new ProgressEvent('error')));
        expect(navigate).toHaveBeenCalled();
      });
    }

    it('forgets only the active account, revokes its refresh token and opens the app in another valid account', async () => {
      const signedOut = idOf(first);
      const result = restore.signOut();

      // Відкликання токена й пошук ідуть одночасно: пошук не чекає на відповідь сервера.
      const revoke = await waitFor(logoutUrl(first));
      expect(revoke.request.body).toEqual({ refreshToken: 'r1' });
      expect(revoke.request.headers.get('Authorization')).toBe('Bearer a1');
      (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
      (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', email: 'u2@organization.org', firstName: '', lastName: '', roles: [], customFields: [] });
      http.match(() => true);
      revoke.flush(null, { status: 204, statusText: 'No Content' });

      expect(await result).toBe('ok');
      await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/start', { onSameUrlNavigation: 'reload' }));
      expect(accounts.accounts().map((a) => a.id)).toEqual([idOf(second)]);
      expect(accounts.activeId()).toBe(idOf(second));
      expect(localStorage.getItem(`yavir.account.${signedOut}`)).toBeNull();
    });

    it('goes to /relogin, with no active account, when the accounts that are left are not available', async () => {
      accounts.markOutdated(idOf(second));
      const result = restore.signOut();
      (await waitFor(logoutUrl(first))).flush(null, { status: 204, statusText: 'No Content' });
      await result;
      await failTheRestUntilNavigation();

      expect(navigate).toHaveBeenCalledWith('/relogin');
      expect(accounts.accounts().map((a) => a.id)).toEqual([idOf(second)]);
      expect(accounts.activeId()).toBeNull();
    });

    it('goes to /login when no account is left', async () => {
      accounts.remove(idOf(second));
      const result = restore.signOut();
      (await waitFor(logoutUrl(first))).flush(null, { status: 204, statusText: 'No Content' });
      await result;
      await failTheRestUntilNavigation();

      expect(navigate).toHaveBeenCalledWith('/login');
      expect(accounts.accounts()).toHaveLength(0);
    });

    it('still signs out locally when the server does not answer the revoking', async () => {
      accounts.remove(idOf(second));
      const result = restore.signOut();
      (await waitFor(logoutUrl(first))).error(new ProgressEvent('error'));

      expect(await result).toBe('unreachable');
      await failTheRestUntilNavigation();
      expect(accounts.accounts()).toHaveLength(0);
      expect(navigate).toHaveBeenCalledWith('/login');
    });

    it('keeps the search flag on while it looks for another account, so the session service does not interfere', async () => {
      const result = restore.signOut();
      const health = await waitFor(`${second}/health`);
      expect(accounts.switching()).toBe(true);

      health.error(new ProgressEvent('error'));
      openRequests().forEach((request) => request.error(new ProgressEvent('error')));
      await result;
      await failTheRestUntilNavigation();
      expect(accounts.switching()).toBe(false);
    });
  });
});
