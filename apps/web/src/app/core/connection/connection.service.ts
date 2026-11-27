import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Observable, catchError, firstValueFrom, map, of, timeout } from 'rxjs';

import { MaintenanceNoticeDto, SystemStatusDto } from '../../api/generated/models';
import { AccountsService } from '../accounts/accounts.service';
import { Untrusted } from '../http/api-contract';

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
export type SystemStatus = SystemStatusDto;

/** Чим закінчилася перевірка `/health`: стан і, для невдачі, пояснення в консоль. */
interface HealthResult {
  readonly status: 'healthy' | 'unhealthy' | 'unreachable';
  readonly message: string;
}

/**
 * Скасовує запит, що ще триває. `next` — запит, який його замінив: той, хто чекав на
 * скасований, отримає результат нового. Без `next` — `null` (результату не буде).
 */
type Supersede<T> = (next: Promise<T> | null) => void;

/**
 * Розбирає адресу сервера, введену користувачем (домен, IP або повне
 * посилання), у URL. Правила: без протоколу береться протокол поточної
 * сторінки; без порту — порт API за замовчуванням (5065); шлях, query й
 * hash відкидаються — береться лише origin. Повертає `null`, якщо це не
 * схоже на адресу http(s)-сервера.
 */
export function parseServerAddress(raw: string | null): URL | null {
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
 * Дані підключення до інсталяції: до якого сервера звертатися і чи працює там API. Сервіс
 * існує в одному екземплярі на весь застосунок (`providedIn: 'root'`), тож усі сторінки
 * бачать ту саму адресу й той самий стан: це треба знати кожній, що звертається до API, а не
 * лише сторінці входу.
 *
 * Ціль (`serverUrl`) визначається за пріоритетом: сервер активного акаунта → адреса, надана
 * на кроці 1 сторінки входу → `<протокол>://<домен сторінки>:5065`. Перевірка (`checkApi`)
 * питає в цілі `GET /health`; результат — в `apiStatus` (невдача ще й у консолі). Змінилася
 * ціль (надано нову адресу, перемкнувся акаунт) — перевірку запускає той, хто її змінив.
 *
 * Сервіс не відповідає за вхід (сесію, токени) і сам не стежить за сервером: стан — це
 * результат останньої перевірки.
 */
@Injectable({ providedIn: 'root' })
export class ConnectionService {
  private readonly http = inject(HttpClient);
  private readonly accounts = inject(AccountsService);

  /** Домен (hostname, без протоколу й порту), з якого відкрито сторінку. */
  readonly domain: string = location.hostname;

  /**
   * Адреса сервера, надана користувачем на кроці 1 (як введена), або `null`, якщо
   * її не надавали. Діє, поки нема активного акаунта (у акаунта своя адреса
   * підключення) і лише в памʼяті: у сховище адреса потрапляє разом із
   * акаунтом після входу.
   */
  private readonly serverAddressSignal = signal<string | null>(null);
  readonly serverAddress: Signal<string | null> = this.serverAddressSignal.asReadonly();

  /**
   * Ціль перевірки (origin сервера): адреса підключення активного акаунта; без
   * нього — надана адреса, а без неї — домен сторінки. Протокол сторінки
   * використовується за замовчуванням: з https-сторінки запит на http браузер
   * заблокує як mixed content.
   */
  readonly serverUrl: Signal<string> = computed(
    () =>
      this.accounts.active()?.server ??
      parseServerAddress(this.serverAddressSignal())?.origin ??
      this.pageServerUrl,
  );

  /**
   * Сервер за доменом сторінки (`<протокол>://<домен>:5065`) — незалежно від акаунтів і
   * наданої адреси. Це сервер, на який веде посилання-запрошення (`/join`): колега може
   * вже мати акаунт на іншому сервері, але запрошення стосується саме цього.
   */
  readonly pageServerUrl: string = `${location.protocol}//${this.domain}:${API_PORT}`;
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

  /** Скасування перевірки `/health`, що ще триває (`null` — жодна не триває). */
  private supersedeCheck: Supersede<ApiStatus> | null = null;
  /** Скасування запиту статусу системи, що ще триває. */
  private supersedeSystemStatus: Supersede<SystemStatus | null> | null = null;

  constructor() {
    void this.checkApi();
  }

  /**
   * Перевіряє API за поточною ціллю; результат — в `apiStatus` (невдача ще й у консолі). Повертає
   * остаточний стан. Нова перевірка скасовує попередню, що ще триває (відповідь зі старої
   * адреси не має перезаписати результат нової), і той, хто чекав на скасовану, отримує
   * результат нової.
   */
  checkApi(): Promise<ApiStatus> {
    const supersede = this.supersedeCheck;
    // Статус системи належить попередній цілі — після нової перевірки він застарілий і має
    // бути запитаний заново.
    this.supersedeSystemStatus?.(null);
    this.supersedeSystemStatus = null;
    this.systemStatusSignal.set(null);
    this.systemStatusStateSignal.set('idle');

    const url = this.apiHealthUrl();
    this.apiStatusSignal.set('checking');

    const check = new Promise<ApiStatus>((resolve) => {
      const subscription = this.requestHealth(url).subscribe(({ status, message }) => {
        this.supersedeCheck = null;
        this.apiStatusSignal.set(status);
        if (status !== 'healthy') {
          console.warn(`[ConnectionService] ${message} — ${url}`);
        }
        resolve(status);
      });
      this.supersedeCheck = (next) => {
        subscription.unsubscribe();
        resolve(next ?? this.apiStatusSignal());
      };
    });
    supersede?.(check);
    return check;
  }

  /**
   * Одноразова перевірка сервера (`GET /health`, відповідь «Healthy»), що не чіпає стан
   * сервісу (`apiStatus`, статус системи): сторінка лишається такою, як була, поки триває
   * запит. `server` — origin, який перевіряти (за замовчуванням поточний сервер).
   */
  async probeApi(server?: string): Promise<boolean> {
    const url = server ? `${server}${API_HEALTH_PATH}` : this.apiHealthUrl();
    const { status, message } = await firstValueFrom(this.requestHealth(url));
    if (status !== 'healthy') {
      console.warn(`[ConnectionService] Одноразова перевірка: ${message} — ${url}`);
    }
    return status === 'healthy';
  }

  /**
   * `GET <сервер>/health` з таймаутом; не кидає помилок — невдача теж результат:
   * - `healthy` — API відповіло зі статусом `Healthy`;
   * - `unhealthy` — щось відповіло, але не так (помилковий HTTP-статус, інший `status` у тілі);
   * - `unreachable` — відповіді нема (сервера не існує, порт закритий, таймаут, CORS).
   */
  private requestHealth(url: string): Observable<HealthResult> {
    return this.http.get<Untrusted<{ status: string }>>(url).pipe(
      timeout(API_CHECK_TIMEOUT_MS),
      map((body): HealthResult => {
        const reported = typeof body?.status === 'string' ? body.status : 'немає';
        return reported === 'Healthy'
          ? { status: 'healthy', message: '' }
          : { status: 'unhealthy', message: `API відповіло, але не «Healthy» (status: ${reported})` };
      }),
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status > 0) {
          const reported = (error.error as Untrusted<{ status: string }>)?.status;
          const details = typeof reported === 'string' ? `, status: ${reported}` : '';
          return of<HealthResult>({ status: 'unhealthy', message: `Щось відповіло, але не здорове API: HTTP ${error.status}${details}` });
        }
        return of<HealthResult>({
          status: 'unreachable',
          message: 'API недоступне: відповіді немає (не існує, закрите порт/мережа або CORS)',
        });
      }),
    );
  }

  /**
   * Запитує стан первинного налаштування системи (`GET <сервер>/api/v1/system/status`);
   * результат — в `systemStatus` (невдача ще й у консолі). Викликається, коли підключення вже є.
   * Повертає статус або `null`, якщо запит не вдався чи відповідь не відповідає контракту
   * `SystemStatusDto`: за такою відповіддю не можна вирішувати, що показувати.
   *
   * Новий запит скасовує попередній, і той, хто чекав на скасований, отримує результат
   * нового. Запит, скасований новою перевіркою підключення (`checkApi`), дає `null`: ціль
   * змінилася, і його відповідь уже нічия.
   */
  loadSystemStatus(): Promise<SystemStatus | null> {
    const supersede = this.supersedeSystemStatus;

    const url = `${this.serverUrl()}${API_SYSTEM_STATUS_PATH}`;
    this.systemStatusSignal.set(null);
    this.systemStatusStateSignal.set('loading');

    const request = new Promise<SystemStatus | null>((resolve) => {
      const finish = (status: SystemStatus | null): void => {
        this.supersedeSystemStatus = null;
        this.systemStatusSignal.set(status);
        this.systemStatusStateSignal.set(status ? 'loaded' : 'failed');
        resolve(status);
      };
      const subscription = this.http
        .get<Untrusted<SystemStatus>>(url)
        .pipe(timeout(API_CHECK_TIMEOUT_MS))
        .subscribe({
          next: (body) => {
            const status = toSystemStatus(body);
            if (!status) {
              console.warn(`[ConnectionService] Статус системи не відповідає контракту — ${url}`, body);
            }
            finish(status);
          },
          error: (error: unknown) => {
            const reason =
              error instanceof HttpErrorResponse && error.status > 0 ? `HTTP ${error.status}` : 'відповіді немає';
            console.warn(`[ConnectionService] Не вдалося отримати статус системи (${reason}) — ${url}`);
            finish(null);
          },
        });
      this.supersedeSystemStatus = (next) => {
        subscription.unsubscribe();
        resolve(next);
      };
    });
    supersede?.(request);
    return request;
  }

  /**
   * Тихо перепитує статус системи (щоб побачити нове оголошення про технічні роботи): поточний
   * статус лишається, поки не прийде новий, а невдача нічого не міняє (на відміну від
   * `loadSystemStatus`, який на час запиту статус скидає). Лише коли статус уже завантажено й інший
   * запит не йде; відповідь для сервера, який тим часом змінився, відкидається.
   */
  async refreshSystemStatus(): Promise<void> {
    if (this.systemStatusStateSignal() !== 'loaded' || this.supersedeSystemStatus) {
      return;
    }
    const server = this.serverUrl();
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<SystemStatus>>(`${server}${API_SYSTEM_STATUS_PATH}`).pipe(timeout(API_CHECK_TIMEOUT_MS)));
      const status = toSystemStatus(body);
      if (status && server === this.serverUrl() && this.systemStatusStateSignal() === 'loaded' && !this.supersedeSystemStatus) {
        this.systemStatusSignal.set(status);
      }
    } catch {
      // Невдала фонова перевірка нічого не міняє: наступна спробує знову.
    }
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
    void this.checkApi();
    return true;
  }

  /** Забуває надану адресу й повертається до цілі за доменом сторінки. */
  clearServerAddress(): void {
    this.serverAddressSignal.set(null);
    void this.checkApi();
  }
}

/** Повідомлення про планові роботи з відповіді сервера; `null` — не обʼєкт контракту. */
function toMaintenanceNotice(raw: Untrusted<MaintenanceNoticeDto>): MaintenanceNoticeDto | null {
  if (!raw || typeof raw !== 'object') {
    return null;
  }
  const { message, startsAt, endsAt } = raw;
  return typeof message === 'string' && typeof startsAt === 'string' && typeof endsAt === 'string'
    ? { message, startsAt, endsAt }
    : null;
}

/** Статус системи з відповіді сервера; `null` — відповідь не відповідає контракту `SystemStatusDto`. */
function toSystemStatus(body: Untrusted<SystemStatus>): SystemStatus | null {
  if (!body || typeof body !== 'object') {
    return null;
  }
  const { hasAdmin, hasNgo, isSetupComplete, version, notice } = body;
  if (
    typeof hasAdmin !== 'boolean' ||
    typeof hasNgo !== 'boolean' ||
    typeof isSetupComplete !== 'boolean' ||
    typeof version !== 'string'
  ) {
    return null;
  }
  if (notice === null) {
    return { hasAdmin, hasNgo, isSetupComplete, version, notice: null };
  }
  const parsedNotice = toMaintenanceNotice(notice);
  return parsedNotice ? { hasAdmin, hasNgo, isSetupComplete, version, notice: parsedNotice } : null;
}
