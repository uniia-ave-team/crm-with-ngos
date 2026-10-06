import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Subscription, finalize, firstValueFrom, timeout } from 'rxjs';

const SERVER_ADDRESS_STORAGE_KEY = 'yavir.serverAddress';

const API_PORT = 5065;
const API_HEALTH_PATH = '/health';
const API_SYSTEM_STATUS_PATH = '/api/v1/system/status';
const API_CHECK_TIMEOUT_MS = 5000;

/**
 * Стан API за поточною адресою сервера:
 * - `unknown` — перевірку ще не запускали;
 * - `checking` — запит до /health у процесі;
 * - `healthy` — API відповіло зі статусом `Healthy`;
 * - `unhealthy` — щось відповіло, але не «Healthy» (помилковий HTTP-статус,
 *   `Degraded`/`Unhealthy` у тілі або там взагалі не наше API);
 * - `unreachable` — відповіді немає (порт закритий, домен не резолвиться,
 *   таймаут, запит заблоковано CORS/mixed content): API там не існує.
 */
export type ApiStatus = 'unknown' | 'checking' | 'healthy' | 'unhealthy' | 'unreachable';

/** Стан запиту статусу системи (див. `ConnectionService.systemStatusState`). */
export type SystemStatusState = 'idle' | 'loading' | 'loaded' | 'failed';

/**
 * Стан первинного налаштування системи — відповідь
 * `GET /api/v1/system/status` (авторизація не потрібна). За ним фронтенд
 * вирішує, чи показувати майстер початкового налаштування.
 */
export interface SystemStatus {
  hasAdmin: boolean;
  hasNgo: boolean;
  isSetupComplete: boolean;
}

/**
 * Розбирає адресу сервера, введену користувачем (домен, IP або повне
 * посилання), у URL. Правила: без протоколу береться протокол поточної
 * сторінки; без порту — порт API за замовчуванням (5065); шлях, query й
 * hash відкидаються — береться лише origin. Повертає `null`, якщо це не
 * схоже на адресу http(s)-сервера.
 */
function parseServerAddress(raw: string | null): URL | null {
  const trimmed = raw?.trim();
  if (!trimmed) {
    return null;
  }
  const withScheme = /^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed) ? trimmed : `${location.protocol}//${trimmed}`;
  try {
    const url = new URL(withScheme);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      return null;
    }
    if (!url.port) {
      url.port = String(API_PORT);
    }
    return url;
  } catch {
    return null;
  }
}

/**
 * Дані підключення до інсталяції: домен, з якого відкрито сторінку, адреса
 * сервера (надана на кроці 1 сторінки входу) і стан API. Синглтон на весь
 * застосунок: це треба знати будь-якій сторінці, що звертається до API, а
 * не лише самій сторінці входу.
 *
 * Алгоритм: визначити ціль — надану адресу сервера, а якщо її нема, то
 * `<протокол>://<домен сторінки>:5065` — і перевірити, чи там існує API та
 * чи воно працює (`GET /health`). Результат — в `apiStatus` і в консолі.
 * Надання нової адреси (`setServerAddress`) запускає алгоритм заново вже
 * з нею. Не відповідає за сам вхід (сесію/токен) і не шукає сервер у
 * мережі — це прийде пізніше.
 */
@Injectable({ providedIn: 'root' })
export class ConnectionService {
  private readonly http = inject(HttpClient);

  /** Домен (hostname, без протоколу й порту), з якого відкрито сторінку. */
  readonly domain: string = location.hostname;

  /**
   * Адреса сервера, надана користувачем (як введена), або `null`, якщо її
   * не надавали — тоді ціль визначається за доменом сторінки.
   */
  private readonly serverAddressSignal = signal<string | null>(this.readServerAddress());
  readonly serverAddress: Signal<string | null> = this.serverAddressSignal.asReadonly();

  /**
   * Ціль перевірки (origin сервера). Протокол сторінки використовується за
   * замовчуванням: з https-сторінки запит на http браузер заблокує як mixed
   * content.
   */
  readonly serverUrl: Signal<string> = computed(
    () => parseServerAddress(this.serverAddressSignal())?.origin ?? `${location.protocol}//${this.domain}:${API_PORT}`,
  );
  readonly apiHealthUrl: Signal<string> = computed(() => `${this.serverUrl()}${API_HEALTH_PATH}`);

  private readonly apiStatusSignal = signal<ApiStatus>('unknown');
  readonly apiStatus: Signal<ApiStatus> = this.apiStatusSignal.asReadonly();

  /** Підключення є: API за поточною адресою існує й працює. */
  readonly connected: Signal<boolean> = computed(() => this.apiStatusSignal() === 'healthy');

  private readonly systemStatusSignal = signal<SystemStatus | null>(null);
  /** Остання відповідь /api/v1/system/status або `null` (не запитували/помилка). */
  readonly systemStatus: Signal<SystemStatus | null> = this.systemStatusSignal.asReadonly();

  private readonly systemStatusStateSignal = signal<SystemStatusState>('idle');
  /** Стан запиту статусу системи: ще не питали, триває, отримано або не вдалося. */
  readonly systemStatusState: Signal<SystemStatusState> = this.systemStatusStateSignal.asReadonly();

  private checkSubscription: Subscription | null = null;
  private systemStatusSubscription: Subscription | null = null;

  constructor() {
    console.log(`[ConnectionService] Домен, з якого відкрито сторінку: ${this.domain}`);
    this.checkApi();
  }

  /** Перевіряє API за поточною ціллю; результат — в `apiStatus` і в консолі. */
  checkApi(): void {
    // Відповідь на попередню, ще не завершену перевірку (стару адресу) не
    // має перезаписати результат нової.
    this.checkSubscription?.unsubscribe();
    // Статус системи належить попередній цілі — після нової перевірки він
    // застарілий і має бути запитаний заново.
    this.systemStatusSubscription?.unsubscribe();
    this.systemStatusSignal.set(null);
    this.systemStatusStateSignal.set('idle');

    const url = this.apiHealthUrl();
    const source = this.serverAddressSignal() ? 'надана адреса сервера' : 'домен сторінки';
    this.apiStatusSignal.set('checking');
    console.log(`[ConnectionService] Перевіряю API (${source}): ${url}`);

    this.checkSubscription = this.http
      .get<{ status?: string }>(url)
      .pipe(timeout(API_CHECK_TIMEOUT_MS))
      .subscribe({
        next: (body) => {
          const reported = body?.status;
          if (reported === 'Healthy') {
            this.finishCheck('healthy', `API існує й працює (status: ${reported})`, url);
          } else {
            this.finishCheck('unhealthy', `API відповіло, але не «Healthy» (status: ${reported ?? 'немає'})`, url);
          }
        },
        error: (error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status > 0) {
            const reported = (error.error as { status?: string } | null)?.status;
            this.finishCheck(
              'unhealthy',
              `Щось відповіло, але не здорове API: HTTP ${error.status}` + (reported ? `, status: ${reported}` : ''),
              url,
            );
          } else {
            this.finishCheck('unreachable', 'API недоступне: відповіді немає (не існує, закрите порт/мережа або CORS)', url);
          }
        },
      });
  }

  /**
   * Одноразова перевірка поточного сервера (`GET /health`, відповідь
   * «Healthy»), що не чіпає стан сервісу (`apiStatus`, статус системи): сторінка
   * лишається такою, як була, поки триває запит.
   */
  async probeApi(): Promise<boolean> {
    const url = this.apiHealthUrl();
    console.log(`[ConnectionService] Одноразова перевірка API: ${url}`);
    try {
      const body = await firstValueFrom(this.http.get<{ status?: string }>(url).pipe(timeout(API_CHECK_TIMEOUT_MS)));
      const healthy = body?.status === 'Healthy';
      console.log(`[ConnectionService] Одноразова перевірка: ${healthy ? 'сервер здоровий' : 'сервер не здоровий'} — ${url}`);
      return healthy;
    } catch {
      console.warn(`[ConnectionService] Одноразова перевірка: відповіді немає або помилка — ${url}`);
      return false;
    }
  }

  /**
   * Запитує стан первинного налаштування системи
   * (`GET <сервер>/api/v1/system/status`); результат — в `systemStatus` і в
   * консолі. Викликається, коли підключення вже є. Повертає отриманий статус
   * (`null` — запит не вдався або його скасовано новим).
   */
  loadSystemStatus(): Promise<SystemStatus | null> {
    this.systemStatusSubscription?.unsubscribe();

    const url = `${this.serverUrl()}${API_SYSTEM_STATUS_PATH}`;
    this.systemStatusSignal.set(null);
    this.systemStatusStateSignal.set('loading');
    console.log(`[ConnectionService] Запитую статус системи: ${url}`);

    return new Promise((resolve) => {
      this.systemStatusSubscription = this.http
        .get<SystemStatus>(url)
        .pipe(
          timeout(API_CHECK_TIMEOUT_MS),
          // Запит скасовано новим (відписка) — не лишаємо виклик висіти.
          finalize(() => resolve(null)),
        )
        .subscribe({
          next: (status) => {
            this.systemStatusSignal.set(status);
            this.systemStatusStateSignal.set('loaded');
            console.log(`[ConnectionService] Статус системи отримано — ${url}`, status);
            resolve(status);
          },
          error: (error: unknown) => {
            this.systemStatusSignal.set(null);
            this.systemStatusStateSignal.set('failed');
            const reason =
              error instanceof HttpErrorResponse && error.status > 0 ? `HTTP ${error.status}` : 'відповіді немає';
            console.warn(`[ConnectionService] Не вдалося отримати статус системи (${reason}) — ${url}`);
            resolve(null);
          },
        });
    });
  }

  /**
   * Зберігає надану адресу сервера й проходить алгоритм заново вже з нею.
   * Повертає `false` (нічого не змінюючи), якщо це не схоже на адресу
   * http(s)-сервера.
   */
  setServerAddress(address: string): boolean {
    const trimmed = address.trim();
    if (!parseServerAddress(trimmed)) {
      return false;
    }
    this.serverAddressSignal.set(trimmed);
    this.persistServerAddress(trimmed);
    this.checkApi();
    return true;
  }

  /** Забуває надану адресу й повертається до цілі за доменом сторінки. */
  clearServerAddress(): void {
    this.serverAddressSignal.set(null);
    this.persistServerAddress('');
    this.checkApi();
  }

  private finishCheck(status: ApiStatus, message: string, url: string): void {
    this.apiStatusSignal.set(status);
    const line = `[ConnectionService] ${message} — ${url}`;
    if (status === 'healthy') {
      console.log(line);
    } else {
      console.warn(line);
    }
  }

  private persistServerAddress(value: string): void {
    try {
      if (value) {
        localStorage.setItem(SERVER_ADDRESS_STORAGE_KEY, value);
      } else {
        localStorage.removeItem(SERVER_ADDRESS_STORAGE_KEY);
      }
    } catch {
      // Сховище може бути недоступне (приватний режим тощо).
    }
  }

  private readServerAddress(): string | null {
    try {
      return localStorage.getItem(SERVER_ADDRESS_STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
