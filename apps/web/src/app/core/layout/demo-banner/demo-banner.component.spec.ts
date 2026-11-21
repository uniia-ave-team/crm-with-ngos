import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { AccountsService } from '../../accounts/accounts.service';
import { AuthService } from '../../auth/auth.service';
import { SessionService } from '../../auth/session.service';
import { ConnectionService } from '../../connection/connection.service';
import { DemoBannerComponent } from './demo-banner.component';

describe('DemoBannerComponent', () => {
  const server = 'https://demo.example.org';
  const status = { hasAdmin: true, hasNgo: true, isSetupComplete: true };
  const demo = { accounts: [], sessionMinutes: 60 };
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  const nowSeconds = () => Math.floor(Date.now() / 1000);

  /** JWT без підпису: дані демо-сеансу зникнуть через `secondsLeft` секунд (`null` — токен цього не каже). */
  function jwt(secondsLeft: number | null): string {
    const claims = { sub: 'user-1', exp: nowSeconds() + 3600, ...(secondsLeft === null ? {} : { demo_exp: nowSeconds() + secondsLeft }) };
    return `h.${btoa(JSON.stringify(claims)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.s`;
  }

  beforeEach(async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-20T12:00:00Z'));
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      imports: [DemoBannerComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    }).compileComponents();
    // Тексти живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  /**
   * Користувач уже ввійшов (сторінку перезавантажено) з таким access-токеном, а сервер
   * відповідає таким статусом системи. `answer()` — відповіді сервера на перевірку підключення.
   */
  function render(accessToken: string, systemStatus: object = { ...status, demo }) {
    TestBed.inject(AccountsService).add({ server, accessToken, refreshToken: 'r', identity: 'user-1' });
    const connection = TestBed.inject(ConnectionService);
    const fixture = TestBed.createComponent(DemoBannerComponent);
    const element = fixture.nativeElement as HTMLElement;
    const refresh = () => {
      TestBed.tick();
      fixture.detectChanges();
    };
    const answer = () => {
      http.expectOne(`${server}/health`).flush({ status: 'Healthy' });
      http.expectOne(`${server}/api/v1/system/status`).flush(systemStatus);
      refresh();
    };
    /** Минає `ms` мілісекунд, і шаблон оновлюється. */
    const wait = async (ms: number) => {
      await vi.advanceTimersByTimeAsync(ms);
      refresh();
    };
    refresh();
    return { fixture, element, connection, answer, wait };
  }

  /** Текст банера одним рядком (у шаблоні його частини стоять на окремих рядках). */
  const text = (element: HTMLElement) => element.querySelector('.demo-banner')?.textContent?.replace(/\s+/g, ' ').trim() ?? null;
  const time = (element: HTMLElement) => element.querySelector('.demo-banner__time')?.textContent?.trim() ?? null;
  const withCountdown = (value: string) => `${uk.demo.banner} ${uk.demo.bannerCountdown} ${value}`;

  it('says it is a demo, that only the visitor sees the changes, and counts down to the reset of the data', () => {
    const { element, answer } = render(jwt(3552));
    // Статус системи ще не отримано — банера нема.
    expect(element.querySelector('.demo-banner')).toBeNull();

    answer();
    expect(text(element)).toBe(withCountdown('59:12'));
    expect(time(element)).toBe('59:12');
    // Лічильник — role="timer": екранний диктор не зачитує його щосекунди.
    expect(element.querySelector('.demo-banner__time')?.getAttribute('role')).toBe('timer');
    expect(element.querySelector('[role="status"], [aria-live]')).toBeNull();
  });

  it('updates the countdown every second', async () => {
    const { element, answer, wait } = render(jwt(3552));
    answer();

    await wait(1000);
    expect(time(element)).toBe('59:11');
    await wait(1000);
    expect(time(element)).toBe('59:10');
    await wait(60_000);
    expect(time(element)).toBe('58:10');
  });

  it('shows hours once an hour or more is left', async () => {
    const { element, answer, wait } = render(jwt(3601));
    answer();
    expect(time(element)).toBe('1:00:01');

    await wait(1000);
    expect(time(element)).toBe('1:00:00');
    await wait(1000);
    expect(time(element)).toBe('59:59');
  });

  it('is emphasised when five minutes or less are left, and not before', async () => {
    const { element, answer, wait } = render(jwt(301));
    answer();
    const banner = element.querySelector('.demo-banner')!;
    expect(time(element)).toBe('05:01');
    expect(banner.classList.contains('demo-banner--ending')).toBe(false);

    await wait(1000);
    expect(time(element)).toBe('05:00');
    expect(banner.classList.contains('demo-banner--ending')).toBe(true);

    await wait(240_000);
    expect(time(element)).toBe('01:00');
    expect(banner.classList.contains('demo-banner--ending')).toBe(true);
  });

  it('ends the demo session when the countdown reaches zero', async () => {
    const { element, answer, wait } = render(jwt(2));
    // На сторінку входу веде `SessionService`, що стартує разом із застосунком.
    TestBed.inject(SessionService);
    answer();
    expect(time(element)).toBe('00:02');

    await wait(2000);
    const auth = TestBed.inject(AuthService);
    expect(auth.hasTokens()).toBe(false);
    expect(auth.demoSessionEnded()).toBe(true);
    expect(navigate).toHaveBeenCalledWith('/login');
    expect(TestBed.inject(AccountsService).accounts()).toEqual([]);
    expect(time(element)).toBeNull();
  });

  it('falls back to the static text with the length of the session when the token has no demo_exp', async () => {
    const { element, answer, wait } = render(jwt(null));
    answer();
    const expected = `${uk.demo.banner} ${uk.demo.bannerNoTimer.replace('{{minutes}}', '60')}`;

    expect(text(element)).toBe(expected);
    expect(time(element)).toBeNull();
    expect(element.querySelector('.demo-banner')!.classList.contains('demo-banner--ending')).toBe(false);

    await wait(5000);
    expect(text(element)).toBe(expected);
  });

  it('follows the token of the active account when it is replaced', async () => {
    const { element, answer, wait } = render(jwt(600));
    answer();
    await wait(100_000);
    expect(time(element)).toBe('08:20');

    const accounts = TestBed.inject(AccountsService);
    accounts.update(accounts.activeId()!, { accessToken: jwt(1200) });
    await wait(0);
    expect(time(element)).toBe('20:00');
  });

  it('cannot be dismissed: there is nothing to press in it', () => {
    const { element, answer } = render(jwt(3552));
    answer();
    expect(element.querySelector('.demo-banner button, .demo-banner ion-button, .demo-banner a')).toBeNull();
  });

  it('stays while the connection is checked again', () => {
    const { fixture, element, connection, answer } = render(jwt(3552));
    answer();

    void connection.checkApi();
    fixture.detectChanges();
    expect(element.querySelector('.demo-banner')).not.toBeNull();
  });

  it.each([
    ['has no demo field', status],
    ['says demo is null', { ...status, demo: null }],
  ])('shows nothing when the status %s', (_name, systemStatus) => {
    const { element, answer } = render(jwt(null), systemStatus);
    answer();
    expect(element.querySelector('.demo-banner')).toBeNull();
    expect(element.textContent?.trim()).toBe('');
  });
});
