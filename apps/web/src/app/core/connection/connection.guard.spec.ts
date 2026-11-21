import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { connectionGuard } from './connection.guard';
import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from './connection.service';

describe('connectionGuard', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  type GuardResult = boolean | UrlTree;

  /** Викликає guard; відповідь прийде, коли він дочекається перевірки підключення. */
  function runGuard(): Promise<GuardResult> {
    return TestBed.runInInjectionContext(() =>
      connectionGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<GuardResult>;
  }

  /** Чекає на відповідь guard-а, проганяючи ефекти (стан перевірки він читає через сигнал). */
  async function answer(result: Promise<GuardResult>): Promise<string | boolean> {
    TestBed.tick();
    const value = await result;
    return value instanceof UrlTree ? value.toString() : value;
  }

  const fail = (request: { error(event: ProgressEvent): void }) => request.error(new ProgressEvent('error'));

  describe('without stored accounts', () => {
    function start() {
      const connection = TestBed.inject(ConnectionService);
      const request = http.expectOne(connection.apiHealthUrl());
      return { connection, request, result: runGuard() };
    }

    it('waits for the connection check instead of deciding early', async () => {
      const { request, result } = start();
      let decided = false;
      void result.then(() => (decided = true));
      TestBed.tick();
      await Promise.resolve();
      expect(decided).toBe(false);

      request.flush({ status: 'Healthy' });
      expect(await answer(result)).toBe(true);
    });

    it('lets the navigation through when the API is up', async () => {
      const { request, result } = start();
      request.flush({ status: 'Healthy' });
      expect(await answer(result)).toBe(true);
    });

    it.each([
      ['no response at all', fail],
      [
        'an unhealthy API',
        (request: { flush(b: unknown, o: { status: number; statusText: string }): void }) =>
          request.flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' }),
      ],
    ])('redirects to /login on %s, as there is no account to restore', async (_name, break_) => {
      const { request, result } = start();
      (break_ as (r: unknown) => void)(request);
      expect(await answer(result)).toBe('/login');
    });
  });

  describe('with an active account (the guard judges by its server)', () => {
    const server = `${location.protocol}//192.168.0.7:5065`;
    const other = `${location.protocol}//192.168.0.8:5065`;

    /** Два акаунти на різних серверах; активний — на `server`, і його перевірка ще триває. */
    function start() {
      const accounts = TestBed.inject(AccountsService);
      const spare = accounts.add({ server: other, accessToken: 'a2', refreshToken: 'r2', identity: 'user-2' });
      const active = accounts.add({ server, accessToken: 'a', refreshToken: 'r', identity: 'user-1' });
      const connection = TestBed.inject(ConnectionService);
      expect(connection.serverUrl()).toBe(server);
      return { accounts, connection, active: active.id, spare: spare.id, request: http.expectOne(`${server}/health`) };
    }

    const flags = (accounts: AccountsService) => accounts.accounts().map((account) => [account.id, account.unavailable]);

    it('lets the navigation through when the server of the account is up', async () => {
      const { accounts, active, request } = start();
      const result = runGuard();
      request.flush({ status: 'Healthy' });

      expect(await answer(result)).toBe(true);
      expect(accounts.activeId()).toBe(active);
    });

    it('sends to /relogin, not to /login, when the server of the account does not answer: the account is marked unavailable and no other one is picked', async () => {
      const { accounts, active, spare, request } = start();
      const result = runGuard();
      fail(request);

      expect(await answer(result)).toBe('/relogin');
      expect(accounts.activeId()).toBeNull();
      expect(flags(accounts)).toEqual([
        [spare, false],
        [active, true],
      ]);
    });

    it('checks the server once more before giving up on a failure that was known already: it may be back', async () => {
      const { accounts, active, request } = start();
      fail(request);
      TestBed.tick();

      const result = runGuard();
      TestBed.tick();
      await Promise.resolve();
      http.expectOne(`${server}/health`).flush({ status: 'Healthy' });

      expect(await answer(result)).toBe(true);
      expect(accounts.activeId()).toBe(active);
      expect(accounts.active()?.unavailable).toBe(false);
    });

    it('sends to /relogin when the repeated check fails too', async () => {
      const { accounts, request } = start();
      fail(request);
      TestBed.tick();

      const result = runGuard();
      TestBed.tick();
      await Promise.resolve();
      fail(http.expectOne(`${server}/health`));

      expect(await answer(result)).toBe('/relogin');
      expect(accounts.activeId()).toBeNull();
    });

    it('does not repeat a check that has only just finished', async () => {
      const { request } = start();
      const result = runGuard();
      fail(request);

      await answer(result);
      http.expectNone(`${server}/health`);
    });
  });
});
