import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { DEMO_ENDING_SECONDS, DemoSessionService, formatCountdown } from './demo-session.service';

describe('formatCountdown', () => {
  it.each([
    [3552, '59:12'],
    [3599, '59:59'],
    [300, '05:00'],
    [61, '01:01'],
    [9, '00:09'],
    [0, '00:00'],
  ])('shows %i seconds as mm:ss (%s)', (seconds, text) => {
    expect(formatCountdown(seconds)).toBe(text);
  });

  it.each([
    [3600, '1:00:00'],
    [3661, '1:01:01'],
    [7199, '1:59:59'],
    [36000, '10:00:00'],
  ])('shows %i seconds as h:mm:ss (%s) once an hour or more is left', (seconds, text) => {
    expect(formatCountdown(seconds)).toBe(text);
  });

  it.each([
    [-5, '00:00'],
    [Number.NaN, '00:00'],
    [59.9, '00:59'],
  ])('never shows a negative or a fractional time (%d → %s)', (seconds, text) => {
    expect(formatCountdown(seconds)).toBe(text);
  });
});

describe('DemoSessionService', () => {
  let http: HttpTestingController;
  let accounts: AccountsService;
  let auth: AuthService;
  let service: DemoSessionService;
  let server: string;
  let navigate: ReturnType<typeof vi.spyOn>;

  const nowSeconds = () => Math.floor(Date.now() / 1000);

  /** JWT без підпису з таким `demo_exp` (`undefined` — без нього). */
  function jwt(demoExp: unknown, subject = 'user-1'): string {
    const claims = { sub: subject, exp: nowSeconds() + 3600, ...(demoExp === undefined ? {} : { demo_exp: demoExp }) };
    return `h.${btoa(JSON.stringify(claims)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.s`;
  }

  /** Активний акаунт із таким access-токеном; повертає його ключ. */
  function signIn(accessToken: string, identity = 'user-1'): string {
    const account = accounts.add({ server, accessToken, refreshToken: 'refresh', identity });
    TestBed.tick();
    return account.id;
  }

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-20T12:00:00Z'));
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    // Пошук API на старті: відповідає origin сторінки, тож після кінця сеансу підключення не шукають знову.
    http.expectOne(connection.apiHealthUrl()).flush({ status: 'Healthy' });
    server = connection.serverUrl();
    accounts = TestBed.inject(AccountsService);
    auth = TestBed.inject(AuthService);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    service = TestBed.inject(DemoSessionService);
    TestBed.tick();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('has no countdown when nobody is signed in', () => {
    expect(service.expiresAt()).toBeNull();
    expect(service.secondsLeft()).toBeNull();
    expect(service.ending()).toBe(false);
  });

  it('has no countdown for a token without demo_exp, and never ends such a session', async () => {
    signIn(jwt(undefined));
    expect(service.secondsLeft()).toBeNull();
    expect(service.ending()).toBe(false);

    await vi.advanceTimersByTimeAsync(2 * 3600_000);
    expect(auth.hasTokens()).toBe(true);
    expect(auth.demoSessionEnded()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it.each([
    ['not a time', 'soon'],
    ['an empty string', ''],
    ['zero', 0],
    ['negative', -5],
    ['null', null],
    ['an object', { at: 1 }],
  ])('ignores a demo_exp that is %s', (_name, claim) => {
    signIn(jwt(claim));
    expect(service.secondsLeft()).toBeNull();
    expect(auth.hasTokens()).toBe(true);
  });

  it('counts down to demo_exp given as a number, once a second', async () => {
    signIn(jwt(nowSeconds() + 3552));
    expect(service.expiresAt()).toBe((nowSeconds() + 3552) * 1000);
    expect(service.secondsLeft()).toBe(3552);

    await vi.advanceTimersByTimeAsync(1000);
    expect(service.secondsLeft()).toBe(3551);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(service.secondsLeft()).toBe(3541);
  });

  it('reads demo_exp given as a numeric string just the same', async () => {
    signIn(jwt(String(nowSeconds() + 90)));
    expect(service.secondsLeft()).toBe(90);

    await vi.advanceTimersByTimeAsync(30_000);
    expect(service.secondsLeft()).toBe(60);
  });

  it('marks the countdown as ending when five minutes or less are left', async () => {
    expect(DEMO_ENDING_SECONDS).toBe(300);
    signIn(jwt(nowSeconds() + 301));
    expect(service.ending()).toBe(false);

    await vi.advanceTimersByTimeAsync(1000);
    expect(service.secondsLeft()).toBe(300);
    expect(service.ending()).toBe(true);
  });

  it('ends the demo session when the countdown reaches zero, without waiting for a failing request', async () => {
    const id = signIn(jwt(nowSeconds() + 3));

    await vi.advanceTimersByTimeAsync(2000);
    expect(service.secondsLeft()).toBe(1);
    expect(auth.hasTokens()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1000);
    // Те саме, що й після відповіді DEMO_SESSION_EXPIRED: акаунт забуто, сторінка входу з поясненням.
    expect(accounts.accounts().some((account) => account.id === id)).toBe(false);
    expect(auth.hasTokens()).toBe(false);
    expect(auth.demoSessionEnded()).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/login');
    expect(navigate).toHaveBeenCalledTimes(1);
    // Жодного запиту до API для цього не знадобилось.
    http.expectNone((request) => request.url.includes('/api/v1/auth/'));

    TestBed.tick();
    expect(service.secondsLeft()).toBeNull();
    await vi.advanceTimersByTimeAsync(10_000);
    expect(navigate).toHaveBeenCalledTimes(1);
  });

  it('ends the session at once when the token says the demo data is already gone', () => {
    signIn(jwt(nowSeconds() - 60));

    expect(auth.hasTokens()).toBe(false);
    expect(auth.demoSessionEnded()).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('reads the claim again when the tokens of the active account are replaced', async () => {
    const id = signIn(jwt(nowSeconds() + 600));
    await vi.advanceTimersByTimeAsync(100_000);
    expect(service.secondsLeft()).toBe(500);

    // Оновлення токенів чи повторний вхід: копія даних та сама, `demo_exp` теж; якби змінився — беремо новий.
    accounts.update(id, { accessToken: jwt(nowSeconds() + 2000) });
    TestBed.tick();
    expect(service.secondsLeft()).toBe(2000);

    await vi.advanceTimersByTimeAsync(1000);
    expect(service.secondsLeft()).toBe(1999);
    expect(auth.hasTokens()).toBe(true);
  });

  it('follows the active account: another account brings its own demo_exp, an ordinary one stops the countdown', async () => {
    signIn(jwt(nowSeconds() + 600));
    signIn(jwt(nowSeconds() + 45, 'user-2'), 'user-2');
    expect(service.secondsLeft()).toBe(45);

    signIn(jwt(undefined, 'user-3'), 'user-3');
    expect(service.secondsLeft()).toBeNull();
    // Відлік неактивного акаунта нікого не виганяє.
    await vi.advanceTimersByTimeAsync(700_000);
    expect(accounts.accounts()).toHaveLength(3);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('stops counting after a sign-out', async () => {
    const id = signIn(jwt(nowSeconds() + 5));
    accounts.remove(id);
    TestBed.tick();
    expect(service.secondsLeft()).toBeNull();

    await vi.advanceTimersByTimeAsync(10_000);
    expect(auth.demoSessionEnded()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('clears its timer when it is destroyed', async () => {
    signIn(jwt(nowSeconds() + 2));
    const end = vi.spyOn(auth, 'endDemoSession');

    TestBed.resetTestingModule();
    await vi.advanceTimersByTimeAsync(10_000);

    expect(end).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });
});
