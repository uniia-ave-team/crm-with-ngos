import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { CurrentUserService } from '../users/current-user.service';
import { requireAnyPermission } from './permission.guard';
import { ADMIN_PANEL_PERMISSIONS } from './permissions';

describe('requireAnyPermission', () => {
  let http: HttpTestingController;
  let accounts: AccountsService;
  let server: string;
  const profile = { id: 'user-1', email: 'a@b.org', firstName: ' ', lastName: ' ', avatarUrl: null, roles: [], customFields: [] };

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
    http.match(`${server}/health`);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const run = () =>
    TestBed.runInInjectionContext(() =>
      requireAnyPermission(ADMIN_PANEL_PERMISSIONS)({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;

  const jwt = () => `h.${btoa(JSON.stringify({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 600 })).replace(/=+$/, '')}.s`;
  const meUrl = () => `${server}/api/v1/users/me`;
  const permissionsUrl = () => `${server}/api/v1/users/me/permissions`;

  /** Профіль уже в памʼяті, а в знімку акаунта лежать `stored` права. */
  async function signedIn(stored: string[]): Promise<void> {
    accounts.add({ server, accessToken: jwt(), refreshToken: 'r', identity: 'user-1', permissions: stored });
    const confirmed = TestBed.inject(CurrentUserService).ensure();
    http.expectOne(meUrl()).flush(profile);
    await confirmed;
    http.match(`${server}/api/v1/ngo`);
    http.expectOne(permissionsUrl()).flush(stored);
    await TestBed.inject(CurrentUserService).permissionsSettled();
  }

  /** Відповідає на запит оновлення профілю, який робить страж. */
  async function answer(permissions: string[] | 'fail') {
    (await vi.waitFor(() => http.expectOne(meUrl()))).flush(profile);
    http.match(`${server}/api/v1/ngo`);
    const request = await vi.waitFor(() => http.expectOne(permissionsUrl()));
    if (permissions === 'fail') {
      request.flush({}, { status: 500, statusText: 'x' });
    } else {
      request.flush(permissions);
    }
  }

  it('lets the user in when the fresh rights include all of the listed ones', async () => {
    await signedIn(ADMIN_PANEL_PERMISSIONS as string[]);
    const result = run();
    await answer([...ADMIN_PANEL_PERMISSIONS, 'Permissions.ViewUser']);
    expect(await result).toBe(true);
  });

  it.each(ADMIN_PANEL_PERMISSIONS as string[])('lets the user in with the single right %s', async (right) => {
    await signedIn([right]);
    const result = run();
    await answer([right]);
    expect(await result).toBe(true);
  });

  it('redirects to /planner when none of the listed rights is there, whatever others the user has', async () => {
    await signedIn(['Permissions.UpdateNgo']);
    const result = run();
    await answer(['Permissions.ViewUser', 'Permissions.UpdateUser']);
    expect(((await result) as UrlTree).toString()).toBe('/planner');
  });

  it('takes the rights away at once when they were revoked in the database, although they were saved before', async () => {
    await signedIn(ADMIN_PANEL_PERMISSIONS as string[]);
    const result = run();
    await answer(['Permissions.ViewUser']);
    expect(((await result) as UrlTree).toString()).toBe('/planner');
    expect(accounts.active()?.permissions).toEqual(['Permissions.ViewUser']);
  });

  it('keeps the saved rights when they could not be refreshed', async () => {
    await signedIn(ADMIN_PANEL_PERMISSIONS as string[]);
    const result = run();
    await answer('fail');
    expect(await result).toBe(true);
  });

  it('waits for the sign-in search and for the rights of a page that was just opened', async () => {
    accounts.add({ server, accessToken: jwt(), refreshToken: 'r', identity: 'user-1' });
    const result = run();

    // Пошук акаунта: перевірка підключення, тоді профіль.
    (await vi.waitFor(() => http.expectOne(`${server}/health`))).flush({ status: 'Healthy' });
    (await vi.waitFor(() => http.expectOne(meUrl()))).flush(profile);
    http.match(`${server}/api/v1/ngo`);
    (await vi.waitFor(() => http.expectOne(permissionsUrl()))).flush(ADMIN_PANEL_PERMISSIONS as string[]);

    expect(await result).toBe(true);
    // Профіль завантажено один раз: страж не питає його вдруге після пошуку.
    http.expectNone(meUrl());
  });

  it('does not let the user in (and does not redirect) when nobody is signed in: that is for authGuard', async () => {
    expect(await run()).toBe(false);
  });
});
