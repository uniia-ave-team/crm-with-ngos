import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { AccountRestoreService } from './account-restore.service';
import { AccountsService } from './accounts.service';

describe('AccountRestoreService.activate', () => {
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

  it('makes an account that was outdated because its server did not answer valid again, when it answers now', async () => {
    accounts.markOutdated(idOf(second), true);
    const result = restore.activate(idOf(second));

    (await waitFor(`${second}/health`)).flush({ status: 'Healthy' });
    (await waitFor(`${second}/api/v1/users/me`)).flush({ id: 'u2', roles: [], customFields: [] });
    http.match(() => true);

    expect(await result).toBe('ok');
    expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: false, unavailable: false });
    expect(accounts.activeId()).toBe(idOf(second));
  });

  it('keeps the previous account active and marks the other as unavailable when its server does not answer', async () => {
    const previous = accounts.activeId();
    const result = restore.activate(idOf(second));

    (await waitFor(`${second}/health`)).error(new ProgressEvent('error'));
    // Підключення повертається до сервера попереднього акаунта.
    (await waitFor(`${first}/health`)).flush({ status: 'Healthy' });

    expect(await result).toBe('unavailable');
    // Профіль попереднього акаунта, що скинувся, завантажується знову.
    http.expectOne(`${first}/api/v1/users/me`);
    expect(accounts.activeId()).toBe(previous);
    expect(accounts.accounts().find((a) => a.id === idOf(second))).toMatchObject({ outdated: true, unavailable: true });
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
