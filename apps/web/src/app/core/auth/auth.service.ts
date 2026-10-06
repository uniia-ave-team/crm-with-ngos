import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

/** Префікси ключів localStorage; до них дописується адреса сервера. */
const ACCESS_TOKEN_STORAGE_PREFIX = 'yavir.accessToken.';
const REFRESH_TOKEN_STORAGE_PREFIX = 'yavir.refreshToken.';

const API_LOGIN_PATH = '/api/v1/auth/login';
const API_REFRESH_PATH = '/api/v1/auth/refresh-token';
const REQUEST_TIMEOUT_MS = 10000;

/** Куди переходить користувач, коли сесію завершено (вихід). */
const LOGIN_URL = '/login';

/** Імʼя блокування (Web Locks), що не дає двом вкладкам оновлювати токени одночасно. */
const REFRESH_LOCK_NAME = 'yavir-refresh-tokens';

/** Access-токен вважається простроченим за стільки секунд до справжнього кінця. */
const EXPIRY_SKEW_SECONDS = 30;

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}

export type LoginResult = 'ok' | RequestFailure;
export type RefreshResult = 'ok' | RequestFailure;

/**
 * Вхід користувача й токени сесії. Обидва токени, крім памʼяті, записуються в
 * `localStorage` (окремі ключі для кожної адреси сервера), щоб пережити
 * перезавантаження сторінки: обмін токенів (`/auth/refresh-token`) вимагає і
 * refresh-токен, і попередній access-токен.
 *
 * Refresh-токен одноразовий (після обміну сервер видаляє старий), тому обмін
 * іде в одному екземплярі: у межах вкладки — один спільний запит, між
 * вкладками — Web Lock, а токени, оновлені іншою вкладкою, підхоплюються зі
 * сховища, а не обмінюються вдруге.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly router = inject(Router);

  private readonly accessTokenSignal = signal<string | null>(null);
  readonly accessToken: Signal<string | null> = this.accessTokenSignal.asReadonly();

  private readonly refreshTokenSignal = signal<string | null>(null);
  readonly refreshToken: Signal<string | null> = this.refreshTokenSignal.asReadonly();

  /** Є обидва токени (з памʼяті або відновлені зі сховища). */
  readonly hasTokens: Signal<boolean> = computed(() => !!this.accessTokenSignal() && !!this.refreshTokenSignal());

  private refreshInFlight: Promise<RefreshResult> | null = null;

  constructor() {
    // Інша вкладка оновила токени або вийшла — підхоплюємо її стан.
    window.addEventListener('storage', (event) => {
      const server = this.connection.serverUrl();
      if (
        event.key === `${ACCESS_TOKEN_STORAGE_PREFIX}${server}` ||
        event.key === `${REFRESH_TOKEN_STORAGE_PREFIX}${server}`
      ) {
        const stored = this.readStoredTokens();
        this.accessTokenSignal.set(stored?.accessToken ?? null);
        this.refreshTokenSignal.set(stored?.refreshToken ?? null);
      }
    });
  }

  /**
   * Підхоплює токени, збережені в `localStorage` для поточної адреси сервера
   * (якщо в памʼяті їх ще нема). Викликається, коли адреса сервера вже відома.
   */
  restoreTokens(): void {
    if (this.hasTokens()) {
      return;
    }
    const stored = this.readStoredTokens();
    if (stored) {
      this.accessTokenSignal.set(stored.accessToken);
      this.refreshTokenSignal.set(stored.refreshToken);
    }
  }

  /** Access-токен прострочений або майже (за `exp` у самому JWT; без токена чи при нечитному — `false`). */
  accessTokenExpired(): boolean {
    const exp = readJwtExpiry(this.accessTokenSignal());
    return exp !== null && exp * 1000 - EXPIRY_SKEW_SECONDS * 1000 <= Date.now();
  }

  /**
   * `POST <сервер>/api/v1/auth/refresh-token` з обома токенами; при успіху
   * зберігає нову пару. Виклики, що збіглися в часі, діляться одним запитом,
   * а між вкладками обмін іде під Web Lock. Якщо сервер відхилив токени
   * (400/401), а в сховищі вже лежить нова пара від іншої вкладки — беремо її;
   * інакше токени видаляються. При мережевій помилці токени лишаються.
   */
  refresh(): Promise<RefreshResult> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.runExclusive(() => this.performRefresh()).finally(() => {
        this.refreshInFlight = null;
      });
    }
    return this.refreshInFlight;
  }

  /** Забуває токени (памʼять і `localStorage` для поточної адреси сервера). */
  clearTokens(): void {
    this.accessTokenSignal.set(null);
    this.refreshTokenSignal.set(null);
    const server = this.connection.serverUrl();
    try {
      localStorage.removeItem(`${ACCESS_TOKEN_STORAGE_PREFIX}${server}`);
      localStorage.removeItem(`${REFRESH_TOKEN_STORAGE_PREFIX}${server}`);
    } catch {
      // Сховище недоступне — нічого прибирати.
    }
  }

  /** Вихід із системи: токени забуто, користувача перекинуто на сторінку входу. */
  signOut(): void {
    this.clearTokens();
    void this.router.navigateByUrl(LOGIN_URL);
  }

  /** `POST <сервер>/api/v1/auth/login`; при успіху зберігає обидва токени (в памʼяті й у `localStorage`). */
  async login(email: string, password: string): Promise<LoginResult> {
    const url = `${this.connection.serverUrl()}${API_LOGIN_PATH}`;
    console.log(`[AuthService] Входжу: ${url}`);

    try {
      const tokens = await firstValueFrom(
        this.http.post<LoginResponse>(url, { email, password }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      this.applyTokens(tokens);
      console.log('[AuthService] Вхід виконано, токени отримано');
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося увійти (${failure})`, error);
      return failure;
    }
  }

  private async performRefresh(): Promise<RefreshResult> {
    // Поки чекали на блокування, інша вкладка могла вже оновити токени.
    if (this.adoptNewerStoredTokens() && !this.accessTokenExpired()) {
      console.log('[AuthService] Токени вже оновлено іншою вкладкою');
      return 'ok';
    }

    const accessToken = this.accessTokenSignal();
    const refreshToken = this.refreshTokenSignal();
    if (!accessToken || !refreshToken) {
      return 'unauthorized';
    }
    const url = `${this.connection.serverUrl()}${API_REFRESH_PATH}`;
    console.log(`[AuthService] Оновлюю токени: ${url}`);

    try {
      const tokens = await firstValueFrom(
        this.http.post<LoginResponse>(url, { accessToken, refreshToken }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      this.applyTokens(tokens);
      console.log('[AuthService] Токени оновлено');
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося оновити токени (${failure})`, error);
      if (failure === 'invalid' || failure === 'unauthorized') {
        // Можливо, наш refresh-токен «спалила» інша вкладка, що встигла першою.
        if (this.adoptNewerStoredTokens()) {
          return 'ok';
        }
        this.clearTokens();
      }
      return failure;
    }
  }

  /**
   * Якщо у сховищі лежить інша пара, ніж у памʼяті (її записала інша вкладка),
   * бере її в памʼять. Повертає, чи так було.
   */
  private adoptNewerStoredTokens(): boolean {
    const stored = this.readStoredTokens();
    if (!stored || stored.refreshToken === this.refreshTokenSignal()) {
      return false;
    }
    this.accessTokenSignal.set(stored.accessToken);
    this.refreshTokenSignal.set(stored.refreshToken);
    return true;
  }

  private runExclusive<T>(task: () => Promise<T>): Promise<T> {
    // Web Locks є не скрізь (старі браузери) — тоді обходимось без блокування.
    return typeof navigator !== 'undefined' && navigator.locks
      ? navigator.locks.request(REFRESH_LOCK_NAME, task)
      : task();
  }

  private readStoredTokens(): LoginResponse | null {
    const server = this.connection.serverUrl();
    try {
      const accessToken = localStorage.getItem(`${ACCESS_TOKEN_STORAGE_PREFIX}${server}`);
      const refreshToken = localStorage.getItem(`${REFRESH_TOKEN_STORAGE_PREFIX}${server}`);
      return accessToken && refreshToken ? { accessToken, refreshToken } : null;
    } catch {
      return null;
    }
  }

  private applyTokens(tokens: LoginResponse): void {
    this.accessTokenSignal.set(tokens.accessToken);
    this.refreshTokenSignal.set(tokens.refreshToken);
    this.persistTokens(tokens);
  }

  private persistTokens(tokens: LoginResponse): void {
    const server = this.connection.serverUrl();
    try {
      localStorage.setItem(`${ACCESS_TOKEN_STORAGE_PREFIX}${server}`, tokens.accessToken);
      localStorage.setItem(`${REFRESH_TOKEN_STORAGE_PREFIX}${server}`, tokens.refreshToken);
    } catch {
      // Сховище може бути недоступне (приватний режим тощо) — тоді токени
      // лишаються лише в памʼяті.
    }
  }
}

/** Поле `exp` (секунди) з payload JWT; `null`, якщо токена нема або він нечитний. */
function readJwtExpiry(token: string | null): number | null {
  const payload = token?.split('.')[1];
  if (!payload) {
    return null;
  }
  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const exp = (JSON.parse(json) as { exp?: unknown }).exp;
    return typeof exp === 'number' ? exp : null;
  } catch {
    return null;
  }
}
