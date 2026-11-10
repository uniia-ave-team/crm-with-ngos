import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, Injector, Signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';

import { AccountRestoreService } from '../accounts/account-restore.service';
import { AccountsService, StoredAccount } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { readJwtExpiry, readJwtSubject } from './jwt';

const API_LOGIN_PATH = '/api/v1/auth/login';
const API_REFRESH_PATH = '/api/v1/auth/refresh-token';
const API_LOGOUT_PATH = '/api/v1/auth/logout';
const REQUEST_TIMEOUT_MS = 10000;

/** Куди переходить користувач, коли сесію завершено (вихід). */
export const LOGIN_URL = '/login';
/** Сторінка повторного входу: куди веде відсутність доступних акаунтів, коли збережені ще є. */
export const RELOGIN_URL = '/relogin';

/**
 * Куди переходить користувач, коли вхід у систему завершено (після входу, коли
 * ГО вже є, після створення ГО й після успішного оновлення сесії) і куди його
 * повертає сторінка, на яку йому бракує прав (`requireAnyPermission`). Щоб
 * змінити цю адресу — міняти лише тут.
 */
export const REDIRECT_URL = '/planner';

/** Імʼя блокування (Web Locks), що не дає двом вкладкам оновлювати токени одночасно. */
const REFRESH_LOCK_NAME = 'yavir-refresh-tokens';

/** Access-токен вважається простроченим за стільки секунд до справжнього кінця. */
const EXPIRY_SKEW_SECONDS = 30;

/**
 * Скільки живе refresh-токен. Це **припущення клієнта**: сервер не віддає термін
 * (токен непрозорий, відповідь має лише пару токенів), а в налаштуваннях API
 * (`JwtOptions.RefreshTokenExpiryDays`) він 1–365 днів, за замовчуванням 7.
 * Якщо в інсталяції термін коротший, неактивні акаунти можуть застаріти раніше,
 * ніж їх оновлять.
 */
const REFRESH_TOKEN_LIFETIME_MS = 7 * 24 * 60 * 60 * 1000;
/** Токени неактивного акаунта оновлюються, коли минула половина терміну refresh-токена. */
const REFRESH_DUE_AFTER_MS = REFRESH_TOKEN_LIFETIME_MS / 2;

/** Пара токенів, яку віддають вхід і завершення реєстрації за запрошенням. */
export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

type LoginResponse = AuthTokens;

export type LoginResult = 'ok' | RequestFailure;
export type RefreshResult = 'ok' | RequestFailure;
/** `skipped` — токенів уже не було, на сервер запит не йшов. */
export type LogoutResult = 'ok' | 'skipped' | RequestFailure;

/**
 * Вхід користувача й токени сесії. Токени належать **активному акаунту**
 * (`AccountsService`): вони зберігаються в його записі в `localStorage`, щоб
 * пережити перезавантаження сторінки. Обмін токенів
 * (`/auth/refresh-token`) вимагає і refresh-токен, і попередній access-токен.
 *
 * Refresh-токен одноразовий (після обміну сервер видаляє старий), тому обмін
 * іде в одному екземплярі: у межах вкладки — один спільний запит, між
 * вкладками — Web Lock, а запис, оновлений іншою вкладкою, підхоплюється зі
 * сховища, а не обмінюється вдруге.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);
  private readonly router = inject(Router);
  // Сервіс пошуку акаунта залежить від цього, тож береться лише в момент виходу.
  private readonly injector = inject(Injector);

  readonly accessToken: Signal<string | null> = computed(() => this.accounts.active()?.accessToken ?? null);
  readonly refreshToken: Signal<string | null> = computed(() => this.accounts.active()?.refreshToken ?? null);

  /** Є обидва токени (в активного акаунта). */
  readonly hasTokens: Signal<boolean> = computed(() => !!this.accessToken() && !!this.refreshToken());

  /**
   * `id` активного акаунта (або `null`): змінюється при вході, виході й
   * перемиканні. На нього зав'язано все, що належить конкретному акаунту
   * (наприклад, кеш профілю).
   */
  readonly sessionId: Signal<string | null> = this.accounts.activeId;

  private refreshInFlight: Promise<RefreshResult> | null = null;

  /** Access-токен прострочений або майже (за `exp` у самому JWT; без токена чи при нечитному — `false`). */
  accessTokenExpired(): boolean {
    const exp = readJwtExpiry(this.accessToken());
    return exp !== null && exp * 1000 - EXPIRY_SKEW_SECONDS * 1000 <= Date.now();
  }

  /** Скільки мілісекунд лишилось до кінця терміну access-токена; `null` — токена нема чи його `exp` нечитний. */
  accessTokenMsLeft(): number | null {
    const exp = readJwtExpiry(this.accessToken());
    return exp === null ? null : exp * 1000 - Date.now();
  }

  /**
   * `POST <сервер>/api/v1/auth/refresh-token` з обома токенами активного
   * акаунта; при успіху зберігає нову пару. Виклики, що збіглися в часі,
   * діляться одним запитом, а між вкладками обмін іде під Web Lock. Якщо сервер
   * відхилив токени (400/401), а в сховищі вже лежить нова пара від іншої
   * вкладки — беремо її; інакше акаунт позначається застарілим (`outdated`) і
   * перестає бути активним. При мережевій помилці акаунт лишається.
   */
  refresh(): Promise<RefreshResult> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.runExclusive(() => this.performRefresh()).finally(() => {
        this.refreshInFlight = null;
      });
    }
    return this.refreshInFlight;
  }

  /** Забуває акаунт (за замовчуванням активний): його запис і токени видаляються зі сховища. */
  clearTokens(accountId: string | null = this.accounts.activeId()): void {
    if (accountId) {
      this.accounts.remove(accountId);
    }
  }

  /**
   * Сесія акаунта не вдалася (оновити токени не вийшло): акаунт **лишається** в памʼяті, але
   * позначений — токени відхилено (`failure` = `invalid`/`unauthorized`) → `outdated`, сервер
   * мовчить чи помилився → `unavailable` — і, якщо він був активним, активного акаунта більше нема.
   * Тоді користувача перекинуто на сторінку повторного входу (акаунти лишились). Акаунт не
   * видаляється: його користувач зможе відновити. `accountId` — акаунт, чия сесія не вдалася
   * (його треба передавати: поки йшло оновлення, активним міг стати інший, і тоді
   * нікуди не переходимо).
   */
  expireSession(accountId: string | null, failure: RefreshResult): void {
    if (accountId) {
      if (failure === 'invalid' || failure === 'unauthorized') {
        this.accounts.markOutdated(accountId);
      } else if (failure !== 'ok') {
        this.accounts.markUnavailable(accountId);
      }
    }
    if (this.accounts.activeId() === null) {
      void this.router.navigateByUrl(this.accounts.accounts().length > 0 ? RELOGIN_URL : LOGIN_URL);
    }
  }

  /**
   * Вихід з активного акаунта: видаляє лише його (запис, місце в індексі, вказівник активного;
   * мова, інші акаунти й решта памʼяті лишаються) і відкликає його refresh-токен на сервері
   * (`POST <сервер>/api/v1/auth/logout`). Після цього йде та сама перевірка, що й при вході на
   * сторінку (`AccountRestoreService`): знайшовся інший чинний акаунт — він стає активним і
   * відкривається `REDIRECT_URL`; нема — `/relogin`, якщо збережені акаунти ще є, інакше `/login`.
   *
   * Спершу беремо токени (прострочений access-токен тихо оновлюємо — сервер вимагає
   * авторизований запит), далі видаляємо акаунт, і лише потім шлемо запит із заголовком,
   * зібраним уже з цих токенів: результат запиту ні на що не впливає, локально користувач
   * вийшов у будь-якому разі.
   */
  async logout(): Promise<LogoutResult> {
    // Виходимо саме з цього акаунта, навіть якщо невдале оновлення токенів зніме з нього активність.
    const id = this.accounts.activeId();
    if (this.hasTokens() && this.accessTokenExpired()) {
      await this.refresh();
    }
    const account = this.accounts.accounts().find((candidate) => candidate.id === id);
    const url = `${account?.server ?? this.connection.serverUrl()}${API_LOGOUT_PATH}`;

    if (id) {
      this.accounts.remove(id);
    }
    void this.moveOnAfterLogout();

    // Токени, які сервер уже відхилив (оновлення не вдалося), відкликати нічого.
    if (!account?.accessToken || !account.refreshToken || account.outdated) {
      return 'skipped';
    }
    console.log(`[AuthService] Виходжу: ${url}`);
    try {
      await firstValueFrom(
        this.http
          .post(
            url,
            { refreshToken: account.refreshToken },
            { headers: new HttpHeaders({ Authorization: `Bearer ${account.accessToken}` }) },
          )
          .pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      console.log('[AuthService] Refresh-токен відкликано');
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося відкликати refresh-токен на сервері (${failure})`, error);
      return failure;
    }
  }

  /**
   * Після виходу: шукає інший чинний акаунт (як `authGuard`; на час пошуку `SessionService`
   * мовчить). Знайшовся — на `REDIRECT_URL` зі стражами заново; ні — на `/relogin`, якщо
   * акаунти ще є, інакше на `/login`.
   */
  private async moveOnAfterLogout(): Promise<void> {
    const result = await this.injector.get(AccountRestoreService).restore();
    if (result === 'restored') {
      void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
    } else {
      void this.router.navigateByUrl(this.accounts.accounts().length > 0 ? RELOGIN_URL : LOGIN_URL);
    }
  }

  /**
   * `POST <сервер>/api/v1/auth/login`; при успіху додає акаунт (сервер + користувач із
   * токена) зі збереженням токенів і робить його активним. `server` — origin сервера
   * (за замовчуванням поточний): інший потрібен, коли додають акаунт на іншому сервері.
   * `replaceAccountId` — ключ збереженого акаунта, який перезаписується цим входом (див.
   * `AccountsService.add`), а не додається новий.
   */
  async login(
    email: string,
    password: string,
    server: string = this.connection.serverUrl(),
    replaceAccountId?: string,
  ): Promise<LoginResult> {
    const url = `${server}${API_LOGIN_PATH}`;
    console.log(`[AuthService] Входжу: ${url}`);

    try {
      const tokens = await firstValueFrom(
        this.http.post<LoginResponse>(url, { email, password }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      this.adoptTokens(server, tokens, email, replaceAccountId);
      console.log('[AuthService] Вхід виконано, токени отримано');
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося увійти (${failure})`, error);
      return failure;
    }
  }

  /**
   * Додає акаунт (`server` + користувач із токена) зі збереженням токенів і робить його
   * активним. Це спільний кінець входу й завершення реєстрації за запрошенням: обидва
   * запити віддають ту саму пару токенів. `replaceAccountId` — акаунт, який перезаписується.
   */
  adoptTokens(server: string, tokens: AuthTokens, email: string, replaceAccountId?: string): void {
    const userId = readJwtSubject(tokens.accessToken);
    this.accounts.add({
      server,
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      userId: userId ?? '',
      // Без `sub` у токені акаунт впізнаємо за поштою.
      identity: userId ?? email.trim().toLowerCase(),
      // Пошту з /users/me не отримати — запамʼятовуємо ту, з якою ввійшли.
      email: email.trim(),
    }, replaceAccountId);
  }

  /**
   * Оновлює токени **неактивного** акаунта, якщо минула половина терміну
   * refresh-токена (див. `REFRESH_TOKEN_LIFETIME_MS`). Так акаунт, що давно не
   * був активним, не застаріває сам по собі. Той самий обмін, що й для активного
   * (під Web Lock, з підхопленням нової пари від іншої вкладки), але:
   * - відхилення токенів (400/401) позначає акаунт застарілим;
   * - мережева помилка нічого не змінює: це не доказ, що вхід недійсний, а
   *   фонове оновлення повториться при наступному запуску.
   * Повертає результат обміну або `'skipped'` (не активний/застарілий чи ще не час).
   */
  refreshStoredIfDue(accountId: string): Promise<RefreshResult | 'skipped'> {
    if (!this.isStoredRefreshDue(accountId)) {
      return Promise.resolve('skipped');
    }
    return this.runExclusive(async () => {
      // Поки чекали на блокування, інша вкладка могла вже оновити цей акаунт.
      this.accounts.syncFromStorage(accountId);
      if (!this.isStoredRefreshDue(accountId)) {
        return 'skipped' as const;
      }
      const account = this.accounts.accounts().find((candidate) => candidate.id === accountId);
      return account ? this.exchange(account) : ('skipped' as const);
    });
  }

  /**
   * Те саме, що `refreshStoredIfDue`, але одразу, не чекаючи половини терміну: обмін токенів
   * неактивного акаунта, що не застарів. Так повторно перевіряють акаунти, чий сервер не відповідав
   * (сторінка повторного входу): вдалося — токени нові, `unavailable` знято; відхилено — акаунт
   * `outdated`; мережева помилка — нічого не змінюється. `'skipped'` — акаунт активний,
   * застарілий чи невідомий.
   */
  refreshStored(accountId: string): Promise<RefreshResult | 'skipped'> {
    if (!this.isStoredRefreshable(accountId)) {
      return Promise.resolve('skipped');
    }
    return this.runExclusive(async () => {
      this.accounts.syncFromStorage(accountId);
      if (!this.isStoredRefreshable(accountId)) {
        return 'skipped' as const;
      }
      const account = this.accounts.accounts().find((candidate) => candidate.id === accountId);
      return account ? this.exchange(account) : ('skipped' as const);
    });
  }

  /** Неактивний акаунт, що не застарів. */
  private isStoredRefreshable(accountId: string): boolean {
    if (accountId === this.accounts.activeId()) {
      return false;
    }
    const account = this.accounts.accounts().find((candidate) => candidate.id === accountId);
    return !!account && !account.outdated;
  }

  /** Неактивний чинний акаунт, у якого минула половина терміну refresh-токена. */
  private isStoredRefreshDue(accountId: string): boolean {
    if (accountId === this.accounts.activeId()) {
      return false;
    }
    const account = this.accounts.accounts().find((candidate) => candidate.id === accountId);
    return !!account && !account.outdated && Date.now() - account.tokensIssuedAt >= REFRESH_DUE_AFTER_MS;
  }

  private async performRefresh(): Promise<RefreshResult> {
    // Поки чекали на блокування, інша вкладка могла вже оновити токени.
    if (this.adoptNewerStoredTokens() && !this.accessTokenExpired()) {
      console.log('[AuthService] Токени вже оновлено іншою вкладкою');
      return 'ok';
    }

    const account = this.accounts.active();
    if (!account?.accessToken || !account.refreshToken) {
      return 'unauthorized';
    }
    return this.exchange(account);
  }

  /**
   * `POST /auth/refresh-token` з парою токенів акаунта; при успіху зберігає нову
   * пару. Відхилення (400/401) — застарілий акаунт, якщо за цей час інша вкладка
   * не записала нову пару.
   */
  private async exchange(account: StoredAccount): Promise<RefreshResult> {
    const url = `${account.server}${API_REFRESH_PATH}`;
    console.log(`[AuthService] Оновлюю токени: ${url}`);

    try {
      const tokens = await firstValueFrom(
        this.http
          .post<LoginResponse>(url, { accessToken: account.accessToken, refreshToken: account.refreshToken })
          .pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      this.accounts.update(account.id, {
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        tokensIssuedAt: Date.now(),
        // Сервер відповів: якщо акаунт вважався недоступним, це знімається.
        unavailable: false,
        // «Востаннє використано» чіпаємо лише для активного: фонове оновлення
        // неактивного акаунта його не «використовує».
        ...(account.id === this.accounts.activeId() ? { lastUsedAt: Date.now() } : {}),
      });
      console.log('[AuthService] Токени оновлено');
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося оновити токени (${failure})`, error);
      if (failure === 'invalid' || failure === 'unauthorized') {
        // Можливо, наш refresh-токен «спалила» інша вкладка, що встигла першою.
        if (this.accounts.syncFromStorage(account.id) !== null) {
          return 'ok';
        }
        this.accounts.markOutdated(account.id);
      }
      return failure;
    }
  }

  /**
   * Якщо у сховищі запис активного акаунта має іншу пару токенів, ніж у памʼяті
   * (її записала інша вкладка), бере її в памʼять. Повертає, чи так було.
   */
  private adoptNewerStoredTokens(): boolean {
    const id = this.accounts.activeId();
    return id !== null && this.accounts.syncFromStorage(id) !== null;
  }

  private runExclusive<T>(task: () => Promise<T>): Promise<T> {
    // Web Locks є не скрізь (старі браузери) — тоді обходимось без блокування.
    return typeof navigator !== 'undefined' && navigator.locks
      ? navigator.locks.request(REFRESH_LOCK_NAME, task)
      : task();
  }
}
