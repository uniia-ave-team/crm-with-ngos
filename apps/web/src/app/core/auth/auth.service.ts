import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, Signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';

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

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}

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
   * Вихід без запиту на сервер (сесія застаріла): акаунт забуто, користувача
   * перекинуто на сторінку входу. `accountId` — акаунт, чия сесія не вдалася
   * (за замовчуванням активний). Його треба передавати, коли між невдалим
   * оновленням і викликом активним міг стати інший акаунт: неуспішний обмін
   * токенів позначає акаунт застарілим і передає активність іншому чинному, а
   * забути треба саме невдалий, а не цей новий.
   */
  signOut(accountId: string | null = this.accounts.activeId()): void {
    this.clearTokens(accountId);
    void this.router.navigateByUrl(LOGIN_URL);
  }

  /**
   * Вихід: повністю очищає `localStorage` і акаунти, переносить на сторінку входу
   * й відкликає refresh-токен на сервері (`POST <сервер>/api/v1/auth/logout`).
   *
   * Спершу беремо токени (прострочений access-токен тихо оновлюємо — сервер
   * вимагає авторизований запит), далі очищаємо, і лише потім шлемо запит із
   * заголовком, зібраним уже з цих токенів: результат запиту ні на що не
   * впливає, локально користувач вийшов у будь-якому разі.
   */
  async logout(): Promise<LogoutResult> {
    if (this.hasTokens() && this.accessTokenExpired()) {
      await this.refresh();
    }
    const account = this.accounts.active();
    const url = `${account?.server ?? this.connection.serverUrl()}${API_LOGOUT_PATH}`;

    this.accounts.wipe();
    void this.router.navigateByUrl(LOGIN_URL);

    if (!account?.accessToken || !account.refreshToken) {
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
   * `POST <сервер>/api/v1/auth/login`; при успіху додає акаунт (поточний сервер +
   * користувач із токена) зі збереженням токенів і робить його активним.
   */
  async login(email: string, password: string): Promise<LoginResult> {
    const server = this.connection.serverUrl();
    const url = `${server}${API_LOGIN_PATH}`;
    console.log(`[AuthService] Входжу: ${url}`);

    try {
      const tokens = await firstValueFrom(
        this.http.post<LoginResponse>(url, { email, password }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
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
      });
      console.log('[AuthService] Вхід виконано, токени отримано');
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося увійти (${failure})`, error);
      return failure;
    }
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
