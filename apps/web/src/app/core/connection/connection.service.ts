import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Observable, Subscription, catchError, finalize, firstValueFrom, map, of, timeout } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';

const API_PORT = 5065;
const API_HEALTH_PATH = '/health';
const API_SYSTEM_STATUS_PATH = '/api/v1/system/status';
const API_CHECK_TIMEOUT_MS = 5000;

/**
 * Стан API за поточною адресою сервера:
 * - `unknown` — перевірку ще не запускали;
 * - `checking` — запит до /health у процесі;
 * - `healthy` — API відповіло зі статусом `Healthy`;
 * - `unhealthy` — щось відповіло, але не «Healthy» (наше API з `Degraded`/`Unhealthy`
 *   або там взагалі не наше API: HTML, 404, не JSON);
 * - `unreachable` — відповіді немає (порт закритий, домен не резолвиться,
 *   таймаут, запит заблоковано CORS/mixed content): API там не існує.
 */
export type ApiStatus = 'unknown' | 'checking' | 'healthy' | 'unhealthy' | 'unreachable';

/** Стан запиту статусу системи (див. `ConnectionService.systemStatusState`). */
export type SystemStatusState = 'idle' | 'loading' | 'loaded' | 'failed';

/** Демо-акаунт, під яким можна ввійти однією кнопкою на сторінці входу. */
export interface DemoAccount {
  name: string;
  email: string;
  password: string;
}

/**
 * Демо-режим сервера: кожен відвідувач після входу отримує власну копію демо-даних, яка живе
 * рівно `sessionMinutes` хвилин від створення, хоч би що він робив. Точний момент видалення —
 * у claim `demo_exp` access-токена (див. `DemoSessionService`).
 */
export interface DemoInfo {
  accounts: DemoAccount[];
  sessionMinutes: number;
}

/**
 * Стан первинного налаштування системи — відповідь
 * `GET /api/v1/system/status` (авторизація не потрібна). За ним фронтенд
 * вирішує, чи показувати майстер початкового налаштування. `demo` є лише
 * тоді, коли на сервері ввімкнено демо-режим.
 */
export interface SystemStatus {
  hasAdmin: boolean;
  hasNgo: boolean;
  isSetupComplete: boolean;
  demo?: DemoInfo | null;
}

const HAS_SCHEME = /^[a-z][a-z0-9+.-]*:\/\//i;
/** Після хоста явно стоїть `:порт` (також стандартний, який `URL` відкидає). */
const EXPLICIT_PORT = /^[a-z][a-z0-9+.-]*:\/\/[^/?#]*:\d+(?:[/?#]|$)/i;

function withScheme(address: string): string {
  return HAS_SCHEME.test(address) ? address : `${location.protocol}//${address}`;
}

/**
 * Розбирає адресу сервера, введену користувачем (домен, IP або повне
 * посилання), у URL. Правила: без протоколу береться протокол поточної
 * сторінки; порт лишається таким, як введено (без нього — стандартний для
 * протоколу); шлях, query й hash відкидаються — береться лише origin. Де саме
 * за цією адресою шукати API, каже `serverAddressCandidates`. Повертає `null`,
 * якщо це не схоже на адресу http(s)-сервера.
 */
export function parseServerAddress(raw: string | null): URL | null {
  const trimmed = raw?.trim();
  if (!trimmed) {
    return null;
  }
  try {
    const url = new URL(withScheme(trimmed));
    return url.protocol === 'http:' || url.protocol === 'https:' ? url : null;
  } catch {
    return null;
  }
}

/**
 * Де шукати API за введеною адресою (origin-и за пріоритетом): порт вказано —
 * лише він; не вказано — спершу адреса як є (стандартний порт 443/80: API за
 * зворотним проксі), далі з портом API за замовчуванням (5065: локальний
 * `docker compose`). Порожній список — це не адреса http(s)-сервера.
 */
export function serverAddressCandidates(raw: string | null): string[] {
  const url = parseServerAddress(raw);
  if (!url) {
    return [];
  }
  // `URL` ховає стандартний порт (`https://host:443` → порт ''), тож явний порт шукаємо у введеному.
  if (url.port || EXPLICIT_PORT.test(withScheme((raw ?? '').trim()))) {
    return [url.origin];
  }
  const withApiPort = new URL(url.origin);
  withApiPort.port = String(API_PORT);
  return [url.origin, withApiPort.origin];
}

/**
 * Що відповіло на `GET <origin>/health`:
 * - `healthy` / `unhealthy` — наше API (JSON із рядковим полем `status`; HTTP-статус будь-який,
 *   крім 404), здорове чи ні;
 * - `not-api` — щось відповіло, але це не наше API: HTML (веб-контейнер віддає `index.html`
 *   на будь-який шлях), 404, не JSON або JSON без `status`;
 * - `unreachable` — відповіді нема.
 */
type ProbeOutcome = 'healthy' | 'unhealthy' | 'not-api' | 'unreachable';

interface ProbeResult {
  origin: string;
  outcome: ProbeOutcome;
  /** Пояснення для консолі. */
  detail: string;
}

function isOurApi(result: ProbeResult): boolean {
  return result.outcome === 'healthy' || result.outcome === 'unhealthy';
}

function healthUrl(origin: string): string {
  return `${origin}${API_HEALTH_PATH}`;
}

/** Поле `status` відповіді /health, якщо тіло — JSON-обʼєкт із рядковим `status`. */
function reportedStatus(body: unknown): string | null {
  const status = body !== null && typeof body === 'object' ? (body as { status?: unknown }).status : undefined;
  return typeof status === 'string' ? status : null;
}

/**
 * Дані підключення до інсталяції: домен, з якого відкрито сторінку, адреса
 * сервера (надана на кроці 1 сторінки входу) і стан API. Синглтон на весь
 * застосунок: це треба знати будь-якій сторінці, що звертається до API, а
 * не лише самій сторінці входу.
 *
 * Алгоритм: визначити, де шукати API (кандидати за пріоритетом), і перевірити
 * кожного (`GET /health`); перший за пріоритетом, що відповів як наше API, і є
 * сервером:
 * - активний акаунт — лише його збережена адреса підключення;
 * - надана адреса сервера — див. `serverAddressCandidates`;
 * - нічого з цього — спершу origin самої сторінки (за зворотним проксі API й
 *   веб-клієнт на одному origin), далі `<протокол>://<домен сторінки>:5065`
 *   (локальний `docker compose`).
 * Результат — в `apiStatus`, `serverUrl` і в консолі. Надання нової адреси
 * (`setServerAddress`) запускає алгоритм заново вже з нею. Знайшлося здорове
 * API — одразу запитується статус системи (`loadSystemStatus`), також коли
 * користувач уже ввійшов. Не відповідає за сам вхід (сесію/токен) і не шукає
 * сервер у мережі.
 */
@Injectable({ providedIn: 'root' })
export class ConnectionService {
  private readonly http = inject(HttpClient);
  private readonly accounts = inject(AccountsService);

  /** Домен (hostname, без протоколу й порту), з якого відкрито сторінку. */
  readonly domain: string = location.hostname;

  /** Де шукати API, коли адреси не надано: origin сторінки, далі той самий домен із портом 5065. */
  private readonly pageCandidates: readonly string[] = [
    ...new Set([location.origin, `${location.protocol}//${this.domain}:${API_PORT}`]),
  ];

  /**
   * Адреса сервера, надана користувачем на кроці 1 (як введена), або `null`, якщо
   * її не надавали. Діє, поки нема активного акаунта (у акаунта своя адреса
   * підключення) і лише в памʼяті: у сховище адреса потрапляє разом із
   * акаунтом після входу.
   */
  private readonly serverAddressSignal = signal<string | null>(null);
  readonly serverAddress: Signal<string | null> = this.serverAddressSignal.asReadonly();

  /**
   * Що знайшов останній пошук без акаунта: для якої наданої адреси (`null` — домен
   * сторінки) і за яким origin відповіло наше API.
   */
  private readonly foundSignal = signal<{ address: string | null; origin: string } | null>(null);

  /** Origin, за яким знайшлося API домену сторінки (`null` — ще не знайдено). */
  private readonly pageServerSignal = signal<string | null>(null);

  /**
   * Сервер (origin), з яким працює застосунок: адреса підключення активного акаунта; без
   * нього — те, що знайшов пошук за наданою адресою чи доменом сторінки. Поки пошук ще нічого
   * не знаходив — перший кандидат (адреса як введена або origin сторінки). Протокол сторінки
   * використовується за замовчуванням: з https-сторінки запит на http браузер
   * заблокує як mixed content.
   */
  readonly serverUrl: Signal<string> = computed(() => {
    const account = this.accounts.active();
    if (account) {
      return account.server;
    }
    const address = this.serverAddressSignal();
    const found = this.foundSignal();
    if (found && found.address === address) {
      return found.origin;
    }
    return serverAddressCandidates(address)[0] ?? this.pageServerUrl;
  });

  /**
   * Сервер за доменом сторінки — незалежно від акаунтів і наданої адреси: origin сторінки
   * або `<протокол>://<домен>:5065`, залежно від того, де відповіло API (поки не знайдено —
   * origin сторінки). Це сервер, на який веде посилання-запрошення (`/join`): колега може
   * вже мати акаунт на іншому сервері, але запрошення стосується саме цього. Щоб
   * дочекатися пошуку — `findPageServer`.
   */
  get pageServerUrl(): string {
    return this.pageServerSignal() ?? this.pageCandidates[0];
  }

  readonly apiHealthUrl: Signal<string> = computed(() => healthUrl(this.serverUrl()));

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

  /** Демо-режим за останнім отриманим статусом системи і сервер, якого він стосується. */
  private readonly demoSignal = signal<{ server: string; info: DemoInfo | null } | null>(null);

  /**
   * Демо-режим поточного сервера (`demo` зі статусу системи) або `null`, якщо його вимкнено
   * чи статус ще не отримано. На відміну від `systemStatus`, не скидається на час повторної
   * перевірки підключення чи перезапиту статусу — банер демо-режиму не блимає.
   */
  readonly demo: Signal<DemoInfo | null> = computed(() => {
    const known = this.demoSignal();
    return known && known.server === this.serverUrl() ? known.info : null;
  });

  /** Сервер працює в демо-режимі. */
  readonly isDemo: Signal<boolean> = computed(() => this.demo() !== null);

  /** Origin, за яким остання завершена перевірка знайшла наше API (`null` — не знайшла чи ще триває). */
  private checkedServer: string | null = null;

  private checkSubscription: Subscription | null = null;
  private systemStatusSubscription: Subscription | null = null;
  /** Запит статусу системи, що ще триває: повторний виклик для того самого сервера ділить його. */
  private systemStatusInFlight: { id: number; url: string; result: Promise<SystemStatus | null> } | null = null;
  private systemStatusRequests = 0;

  constructor() {
    console.log(`[ConnectionService] Домен, з якого відкрито сторінку: ${this.domain}`);
    void this.checkApi();
  }

  /**
   * Шукає й перевіряє API за поточною ціллю (усі кандидати одночасно, перемагає
   * перший за пріоритетом); результат — в `apiStatus`, `serverUrl` і в консолі.
   * Жоден кандидат не є нашим API — стан за останнім із них. Повертає остаточний
   * стан перевірки (якщо її скасовано новою — поточний).
   */
  checkApi(): Promise<ApiStatus> {
    // Відповідь на попередню, ще не завершену перевірку (стару адресу) не
    // має перезаписати результат нової.
    this.checkSubscription?.unsubscribe();
    // Статус системи належить попередній цілі — після нової перевірки він
    // застарілий і має бути запитаний заново.
    this.cancelSystemStatus();
    this.systemStatusSignal.set(null);
    this.systemStatusStateSignal.set('idle');

    const account = this.accounts.active();
    const address = this.serverAddressSignal();
    const candidates = account ? [account.server] : address ? serverAddressCandidates(address) : this.pageCandidates;
    const source = account ? 'адреса активного акаунта' : address ? 'надана адреса сервера' : 'домен сторінки';
    this.apiStatusSignal.set('checking');
    this.checkedServer = null;
    console.log(`[ConnectionService] Перевіряю API (${source}): ${candidates.map(healthUrl).join(', ')}`);

    return new Promise((resolve) => {
      this.checkSubscription = this.discover(candidates)
        .pipe(
          // Перевірку скасовано новою (відписка) — не лишаємо виклик висіти.
          finalize(() => resolve(this.apiStatusSignal())),
        )
        .subscribe((result) => {
          if (!account) {
            this.rememberFound(address, result);
          } else if (isOurApi(result) && this.pageServerSignal() === null && this.pageCandidates.includes(result.origin)) {
            // Акаунт живе на сервері домену сторінки — отже, відомо, де він: туди й повернеться
            // застосунок, коли акаунта не стане (вихід), без нового пошуку.
            this.pageServerSignal.set(result.origin);
          }
          this.checkedServer = isOurApi(result) ? result.origin : null;
          this.finishCheck(result);
        });
    });
  }

  /**
   * Активний акаунт зник чи змінився без перевірки підключення (наприклад, завершився
   * демо-сеанс): якщо поточний сервер (`serverUrl`) уже не той, який перевіряли востаннє, —
   * перевіряє заново; інакше стан підключення й статус системи лишаються чинними.
   */
  checkApiIfServerChanged(): Promise<ApiStatus> {
    return this.checkedServer === this.serverUrl() ? Promise.resolve(this.apiStatusSignal()) : this.checkApi();
  }

  /**
   * Одноразова перевірка одного сервера (`GET /health`, відповідь
   * «Healthy»), що не чіпає стан сервісу (`apiStatus`, статус системи): сторінка
   * лишається такою, як була, поки триває запит. `server` — origin, який перевіряти
   * (за замовчуванням поточний сервер).
   */
  async probeApi(server: string = this.serverUrl()): Promise<boolean> {
    return (await this.discoverOnce([server])) !== null;
  }

  /**
   * Одноразовий пошук API за введеною адресою (`serverAddressCandidates`), що не чіпає стан
   * сервісу. Повертає origin, за яким відповіло здорове API, — саме його зберігає акаунт як
   * адресу підключення, — або `null`: адреса не схожа на адресу сервера чи API там нема.
   */
  resolveServer(address: string): Promise<string | null> {
    return this.discoverOnce(serverAddressCandidates(address));
  }

  /**
   * Одноразовий пошук API за доменом сторінки (ті самі кандидати, що й у `checkApi` без
   * акаунта й наданої адреси), що не чіпає `apiStatus`: знайдений origin стає
   * `pageServerUrl`. Повертає його, якщо API там здорове, інакше `null`.
   */
  findPageServer(): Promise<string | null> {
    return this.discoverOnce(this.pageCandidates, (origin) => this.pageServerSignal.set(origin));
  }

  /**
   * Запитує стан первинного налаштування системи
   * (`GET <сервер>/api/v1/system/status`); результат — в `systemStatus`, `demo` і в
   * консолі. Викликається, коли підключення вже є (після вдалої перевірки API сервіс
   * робить це сам). Запит до того самого сервера, що ще триває, не дублюється.
   * Повертає отриманий статус (`null` — запит не вдався або його скасовано новим).
   */
  loadSystemStatus(): Promise<SystemStatus | null> {
    const server = this.serverUrl();
    const url = `${server}${API_SYSTEM_STATUS_PATH}`;
    if (this.systemStatusInFlight?.url === url) {
      return this.systemStatusInFlight.result;
    }
    this.cancelSystemStatus();

    this.systemStatusSignal.set(null);
    this.systemStatusStateSignal.set('loading');
    console.log(`[ConnectionService] Запитую статус системи: ${url}`);

    const id = ++this.systemStatusRequests;
    const result = new Promise<SystemStatus | null>((resolve) => {
      this.systemStatusSubscription = this.http
        .get<SystemStatus>(url)
        .pipe(
          timeout(API_CHECK_TIMEOUT_MS),
          // Запит скасовано новим (відписка) — не лишаємо виклик висіти.
          finalize(() => {
            if (this.systemStatusInFlight?.id === id) {
              this.systemStatusInFlight = null;
            }
            resolve(null);
          }),
        )
        .subscribe({
          next: (status) => {
            this.systemStatusSignal.set(status);
            this.systemStatusStateSignal.set('loaded');
            this.demoSignal.set({ server, info: status?.demo ?? null });
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
    // Відповідь могла прийти ще під час підписки (тоді `finalize` уже відпрацював).
    if (this.systemStatusStateSignal() === 'loading') {
      this.systemStatusInFlight = { id, url, result };
    }
    return result;
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

  private cancelSystemStatus(): void {
    this.systemStatusSubscription?.unsubscribe();
    this.systemStatusSubscription = null;
    this.systemStatusInFlight = null;
  }

  /**
   * Пошук без акаунта завершено: де API цієї адреси (`address`; `null` — домен сторінки).
   * Нічого не знайдено — лишається знайдене раніше: сервер, що тимчасово мовчить, не переїхав.
   */
  private rememberFound(address: string | null, result: ProbeResult): void {
    if (!isOurApi(result)) {
      return;
    }
    this.foundSignal.set({ address, origin: result.origin });
    if (address === null) {
      this.pageServerSignal.set(result.origin);
    }
  }

  /** Origin першого за пріоритетом кандидата, що є нашим API, якщо воно здорове; інакше `null`. */
  private async discoverOnce(
    candidates: readonly string[],
    onFound?: (origin: string) => void,
  ): Promise<string | null> {
    if (candidates.length === 0) {
      return null;
    }
    console.log(`[ConnectionService] Одноразова перевірка API: ${candidates.map(healthUrl).join(', ')}`);
    const result = await firstValueFrom(this.discover(candidates));
    if (isOurApi(result)) {
      onFound?.(result.origin);
    }
    if (result.outcome === 'healthy') {
      console.log(`[ConnectionService] Одноразова перевірка: сервер здоровий — ${healthUrl(result.origin)}`);
      return result.origin;
    }
    console.warn(`[ConnectionService] Одноразова перевірка: ${result.detail} — ${healthUrl(result.origin)}`);
    return null;
  }

  /**
   * Перевіряє всіх кандидатів одночасно й видає одного: першого за пріоритетом, що
   * відповів як наше API (здорове чи ні), — решту запитів тоді скасовано; жоден — останнього
   * (його відповідь і пояснює, чому API не знайдено). Швидша відповідь менш пріоритетного
   * кандидата чекає, поки відповість чи відпаде пріоритетніший.
   */
  private discover(candidates: readonly string[]): Observable<ProbeResult> {
    return new Observable<ProbeResult>((subscriber) => {
      const results: (ProbeResult | undefined)[] = candidates.map(() => undefined);
      const probes = new Subscription();
      let done = false;

      const finish = (index: number, result: ProbeResult) => {
        done = true;
        probes.unsubscribe();
        // Кандидати, пріоритетніші за обраного, відпали — лишаємо слід, чому.
        for (const skipped of results.slice(0, index)) {
          if (skipped) {
            console.warn(`[ConnectionService] Тут API нема, шукаю далі: ${skipped.detail} — ${healthUrl(skipped.origin)}`);
          }
        }
        subscriber.next(result);
        subscriber.complete();
      };
      const settle = () => {
        for (const [index, result] of results.entries()) {
          if (!result) {
            return;
          }
          if (isOurApi(result) || index === results.length - 1) {
            finish(index, result);
            return;
          }
        }
      };

      candidates.forEach((origin, index) => {
        if (done) {
          return;
        }
        probes.add(
          this.probe(origin).subscribe((result) => {
            results[index] = result;
            if (!done) {
              settle();
            }
          }),
        );
      });
      return () => probes.unsubscribe();
    });
  }

  /** Один запит `GET <origin>/health` і що він означає (`ProbeOutcome`); помилкою не завершується. */
  private probe(origin: string): Observable<ProbeResult> {
    return this.http.get<unknown>(healthUrl(origin)).pipe(
      timeout(API_CHECK_TIMEOUT_MS),
      map((body): ProbeResult => {
        const reported = reportedStatus(body);
        if (reported === null) {
          return { origin, outcome: 'not-api', detail: 'Щось відповіло, але це не наше API (JSON без status)' };
        }
        return reported === 'Healthy'
          ? { origin, outcome: 'healthy', detail: `API існує й працює (status: ${reported})` }
          : { origin, outcome: 'unhealthy', detail: `API відповіло, але не «Healthy» (status: ${reported})` };
      }),
      catchError((error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status === 0) {
          return of<ProbeResult>({
            origin,
            outcome: 'unreachable',
            detail: 'API недоступне: відповіді немає (не існує, закрите порт/мережа або CORS)',
          });
        }
        // Тіло помилки HttpClient розбирає як JSON, коли може; нерозібране (HTML, текст) — не обʼєкт.
        // 404 — це завжди «тут API нема», навіть із JSON (problem details теж має поле `status`).
        const reported = error.status === 404 ? null : reportedStatus(error.error);
        return of<ProbeResult>(
          reported === null
            ? { origin, outcome: 'not-api', detail: `Щось відповіло, але це не наше API: HTTP ${error.status}` }
            : {
                origin,
                outcome: 'unhealthy',
                detail: `Щось відповіло, але не здорове API: HTTP ${error.status}, status: ${reported}`,
              },
        );
      }),
    );
  }

  private finishCheck(result: ProbeResult): void {
    const status: ApiStatus =
      result.outcome === 'healthy' ? 'healthy' : result.outcome === 'unreachable' ? 'unreachable' : 'unhealthy';
    this.apiStatusSignal.set(status);
    const line = `[ConnectionService] ${result.detail} — ${healthUrl(result.origin)}`;
    if (status === 'healthy') {
      console.log(line);
      // Статус системи (первинне налаштування, демо-режим) потрібен і сторінці входу, і
      // сторінкам після входу — запитуємо його одразу, щойно підключення є.
      void this.loadSystemStatus();
    } else {
      console.warn(line);
    }
  }
}
