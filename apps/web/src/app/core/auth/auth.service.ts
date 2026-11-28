import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, Signal, computed, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AuthTokensDto, AuthenticateUserCommand, LogoutCommand, RefreshTokensCommand } from '../../api/generated/models';
import { AccountsService, StoredAccount } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { readJwtExpiry, readJwtSubject } from './jwt';

const API_LOGIN_PATH = '/api/v1/auth/login';
const API_REFRESH_PATH = '/api/v1/auth/refresh-token';
const API_LOGOUT_PATH = '/api/v1/auth/logout';

/** Куди переходить користувач, коли сесію завершено (вихід). */
export const LOGIN_URL = '/login';
/** Сторінка повторного входу: куди веде відсутність доступних акаунтів, коли збережені ще є. */
export const RELOGIN_URL = '/relogin';

/**
 * Куди вести користувача, коли активного акаунта нема: збережені акаунти ще є (застарілі чи
 * недоступні, їх можна відновити) — на повторний вхід, акаунтів нема взагалі — на вхід.
 * Єдине місце, де записано це правило; ним користуються стражі, `SessionService`, вихід і
 * сторінка входу.
 */
export function signedOutUrl(accounts: AccountsService): string {
  return accounts.accounts().length > 0 ? RELOGIN_URL : LOGIN_URL;
}

/**
 * Куди переходить користувач, коли вхід у систему завершено (після входу, коли
 * ГО вже є, після створення ГО й після успішного оновлення сесії) і куди його
 * повертає сторінка, на яку йому бракує прав (`requireAnyPermission`): стартова
 * сторінка (`start/start.page.ts`), яка сама веде на першу доступну за правами (Планер, База
 * знань, Колеги) або, якщо такої нема, показує порожній стан.
 */
export const REDIRECT_URL = '/start';

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

/** Пара токенів, яку віддають вхід, оновлення токенів і завершення реєстрації за запрошенням. */
export type AuthTokens = AuthTokensDto;

/**
 * Пара токенів з відповіді сервера; `null` — відповідь не за контрактом (хоч один токен не
 * рядок або порожній). З такою відповіддю акаунт лишився б без токенів, тому це помилка, а не
 * успіх.
 */
export function toAuthTokens(body: Untrusted<AuthTokens>): AuthTokens | null {
  const { accessToken, refreshToken } = body ?? {};
  return typeof accessToken === 'string' && accessToken && typeof refreshToken === 'string' && refreshToken
    ? { accessToken, refreshToken }
    : null;
}

/**
 * Чим закінчився вхід: `ok`, причина невдачі запиту або `storageFull` — сервер вхід прийняв, але
 * браузер не дав записати акаунт (сховище переповнене), тож входу нема.
 */
export type LoginResult = 'ok' | 'storageFull' | RequestFailure;
export type RefreshResult = 'ok' | RequestFailure;
/** `skipped` — токенів уже не було, на сервер запит не йшов. */
export type LogoutResult = 'ok' | 'skipped' | RequestFailure;

/**
 * Вхід користувача й токени сесії. Токени належать акаунту (`AccountsService`): вони
 * зберігаються в його записі в `localStorage`, щоб пережити перезавантаження сторінки.
 * Сигнали `accessToken`, `refreshToken`, `hasTokens` — про **активний** акаунт. Обмін токенів
 * (`/auth/refresh-token`) вимагає і refresh-токен, і попередній access-токен.
 *
 * Refresh-токен одноразовий (після обміну сервер видаляє старий), тому обмін іде в одному
 * екземплярі: у межах вкладки — один спільний запит на акаунт, між вкладками — Web Lock, а
 * запис, оновлений іншою вкладкою, підхоплюється зі сховища, а не обмінюється вдруге.
 *
 * Сервіс нікуди не переходить: куди вести користувача, коли сесії не стало, вирішує
 * `SessionService` (і стражі), бо це залежить від того, чи не йде пошук акаунта.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);

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

  /** Оновлення токенів, що тривають: по одному на акаунт. */
  private readonly refreshesInFlight = new Map<string, Promise<RefreshResult>>();

  /** Access-токен акаунта (за замовчуванням активного); `null` — акаунта чи токена нема. */
  accessTokenOf(accountId: string | null = this.accounts.activeId()): string | null {
    return this.find(accountId)?.accessToken || null;
  }

  /**
   * Access-токен акаунта (за замовчуванням активного) прострочений або майже — за `exp` у
   * самому JWT; без токена чи при нечитному — `false`.
   */
  accessTokenExpired(accountId: string | null = this.accounts.activeId()): boolean {
    const exp = readJwtExpiry(this.accessTokenOf(accountId));
    return exp !== null && exp * 1000 - EXPIRY_SKEW_SECONDS * 1000 <= Date.now();
  }

  /** Скільки мілісекунд лишилось до кінця терміну access-токена; `null` — токена нема чи його `exp` нечитний. */
  accessTokenMsLeft(): number | null {
    const exp = readJwtExpiry(this.accessToken());
    return exp === null ? null : exp * 1000 - Date.now();
  }

  /**
   * Оновлює токени акаунта `accountId` (за замовчуванням активного):
   * `POST <сервер>/api/v1/auth/refresh-token` з обома його токенами; при успіху зберігає нову
   * пару. Акаунт визначається в мить виклику й далі не змінюється: якщо, поки триває оновлення,
   * активним стане інший, результат однаково стосується того, для кого оновлення просили.
   *
   * Виклики для одного акаунта, що збіглися в часі, діляться одним запитом, а між вкладками
   * обмін іде під Web Lock. Якщо, поки чекали на чергу, пару вже оновив хтось інший (інша
   * вкладка), обміну нема — `ok`. Якщо сервер відхилив токени (400/401), а в сховищі вже лежить
   * нова пара від іншої вкладки — беремо її; інакше акаунт позначається застарілим (`outdated`) і
   * перестає бути активним. При мережевій помилці акаунт лишається як був. Акаунта чи його
   * токенів нема — `unauthorized`.
   */
  refresh(accountId: string | null = this.accounts.activeId()): Promise<RefreshResult> {
    const account = this.find(accountId);
    if (!account) {
      return Promise.resolve('unauthorized');
    }
    const { id, refreshToken } = account;
    let inFlight = this.refreshesInFlight.get(id);
    if (!inFlight) {
      inFlight = this.runExclusive(() => this.performRefresh(id, refreshToken)).finally(() => {
        this.refreshesInFlight.delete(id);
      });
      this.refreshesInFlight.set(id, inFlight);
    }
    return inFlight;
  }

  /**
   * Сесія акаунта не вдалася (оновити токени не вийшло): акаунт **лишається** в памʼяті, але
   * позначений — токени відхилено (`failure` = `invalid`/`unauthorized`) → `outdated`, сервер
   * мовчить чи помилився → `unavailable` — і, якщо він був активним, активного акаунта більше
   * нема. Акаунт не видаляється: його користувач зможе відновити. `accountId` — акаунт, чия
   * сесія не вдалася.
   *
   * Сам метод нікуди не переходить: що активного акаунта не стало, помічає `SessionService` і
   * веде на повторний вхід, а під час пошуку акаунта нічого не робить — там це звичайний крок
   * (пошук перевіряє наступний акаунт).
   */
  expireSession(accountId: string | null, failure: RefreshResult): void {
    if (!accountId) {
      return;
    }
    if (failure === 'invalid' || failure === 'unauthorized') {
      this.accounts.markOutdated(accountId);
    } else if (failure !== 'ok') {
      this.accounts.markUnavailable(accountId);
    }
  }

  /**
   * Перша половина виходу: забуває активний акаунт (запис, місце в індексі, вказівник активного;
   * мова, інші акаунти й решта памʼяті лишаються) і повертає його запис із токенами, щоб їх
   * можна було відкликати (`revoke`). Прострочений access-токен спершу тихо оновлюється: сервер
   * вимагає авторизований запит. `null` — активного акаунта не було.
   *
   * Що буде після виходу (пошук іншого акаунта, перехід) вирішує `AccountRestoreService.signOut`.
   */
  async forgetActive(): Promise<StoredAccount | null> {
    // Виходимо саме з цього акаунта, навіть якщо невдале оновлення токенів зніме з нього активність.
    const id = this.accounts.activeId();
    if (this.hasTokens() && this.accessTokenExpired(id)) {
      await this.refresh(id);
    }
    const account = this.find(id);
    if (id) {
      this.accounts.remove(id);
    }
    return account;
  }

  /**
   * Друга половина виходу: відкликає refresh-токен забутого акаунта на його сервері
   * (`POST <сервер>/api/v1/auth/logout`) із заголовком, зібраним з його ж токенів. Результат ні
   * на що не впливає: локально користувач уже вийшов. `skipped` — відкликати нічого: акаунта чи
   * токенів нема або сервер їх уже відхилив (оновлення не вдалося).
   */
  async revoke(account: StoredAccount | null): Promise<LogoutResult> {
    if (!account?.accessToken || !account.refreshToken || account.outdated) {
      return 'skipped';
    }
    const url = `${account.server}${API_LOGOUT_PATH}`;
    try {
      await firstValueFrom(
        this.http
          .post(
            url,
            { refreshToken: account.refreshToken } satisfies LogoutCommand,
            { headers: new HttpHeaders({ Authorization: `Bearer ${account.accessToken}` }) },
          )
          .pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      return 'ok';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[AuthService] Не вдалося відкликати refresh-токен на сервері (${failure})`, error);
      return failure;
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

    try {
      const body = await firstValueFrom(
        this.http
          .post<Untrusted<AuthTokens>>(url, { email, password } satisfies AuthenticateUserCommand)
          .pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const tokens = toAuthTokens(body);
      if (!tokens) {
        console.warn('[AuthService] Відповідь на вхід без токенів', body);
        return 'error';
      }
      return this.adoptTokens(server, tokens, email, replaceAccountId) ? 'ok' : 'storageFull';
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
   *
   * Повертає, чи акаунт записано. Якщо браузер не дав його записати (сховище переповнене),
   * акаунта не додаємо зовсім: після перезавантаження сторінки його однаково не було б, а
   * користувач думав би, що ввійшов. Тоді памʼять повертається до того, що лежить у сховищі,
   * активним лишається попередній акаунт, а щойно видані токени відкликаються на сервері.
   */
  adoptTokens(server: string, tokens: AuthTokens, email: string, replaceAccountId?: string): boolean {
    const previous = this.accounts.activeId();
    const userId = readJwtSubject(tokens.accessToken);
    const account = this.accounts.add({
      server,
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      userId: userId ?? '',
      // Без `sub` у токені акаунт впізнаємо за поштою.
      identity: userId ?? email.trim().toLowerCase(),
      // Пошту з /users/me не отримати — запамʼятовуємо ту, з якою ввійшли.
      email: email.trim(),
    }, replaceAccountId);
    if (this.accounts.isStored(account.id)) {
      return true;
    }
    console.warn('[AuthService] Браузер не дав записати акаунт: сховище переповнене');
    // Про це скаже сама форма входу (`storageFull`), спливаюче попередження тут зайве.
    this.accounts.dismissStorageFull();
    this.accounts.reload();
    this.accounts.setActive(previous);
    void this.revoke(account);
    return false;
  }

  /**
   * Записує акаунту нову пару токенів, яку сервер видав сам, без обміну (`refresh`). Так буває
   * після зміни пароля: сервер відкликає всі refresh-токени користувача, щоб завершити інші
   * сесії, а сесії, що змінила пароль, віддає нову пару. Сервер щойно її видав, тож акаунт уже
   * не застарілий і не недоступний. Невідомий `accountId` ігнорується.
   */
  adoptRenewedTokens(accountId: string, tokens: AuthTokens): void {
    this.accounts.update(accountId, {
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      tokensIssuedAt: Date.now(),
      outdated: false,
      unavailable: false,
    });
  }

  /**
   * Оновлює токени **неактивного** акаунта, якщо минула половина терміну refresh-токена (див.
   * `REFRESH_TOKEN_LIFETIME_MS`). Так акаунт, що давно не був активним, не застаріває сам по
   * собі. Той самий обмін, що й для активного (`refresh`), тож:
   * - відхилення токенів (400/401) позначає акаунт застарілим;
   * - мережева помилка нічого не змінює: це не доказ, що вхід недійсний, а фонове оновлення
   *   повториться при наступному запуску.
   * Повертає результат обміну або `'skipped'` (акаунт активний, застарілий, невідомий чи ще
   * не час).
   */
  refreshStoredIfDue(accountId: string): Promise<RefreshResult | 'skipped'> {
    return this.refreshInactive(accountId, true);
  }

  /**
   * Те саме, що `refreshStoredIfDue`, але одразу, не чекаючи половини терміну. Так повторно
   * перевіряють акаунти, чий сервер не відповідав (сторінка повторного входу): вдалося — токени
   * нові, `unavailable` знято; відхилено — акаунт `outdated`; мережева помилка — нічого не
   * змінюється. `'skipped'` — акаунт активний, застарілий чи невідомий.
   */
  refreshStored(accountId: string): Promise<RefreshResult | 'skipped'> {
    return this.refreshInactive(accountId, false);
  }

  /** Оновлення токенів неактивного чинного акаунта; `onlyIfDue` — лише коли минула половина терміну. */
  private refreshInactive(accountId: string, onlyIfDue: boolean): Promise<RefreshResult | 'skipped'> {
    const account = accountId === this.accounts.activeId() ? null : this.find(accountId);
    const due = !!account && (!onlyIfDue || Date.now() - account.tokensIssuedAt >= REFRESH_DUE_AFTER_MS);
    return account && !account.outdated && due ? this.refresh(accountId) : Promise.resolve('skipped');
  }

  /**
   * Сам обмін, уже під блокуванням. `refreshTokenAtCall` — refresh-токен акаунта в мить, коли
   * оновлення попросили: якщо тепер він інший, пару вже оновив хтось інший (інша вкладка, поки
   * ми чекали на блокування), і обмінювати вдруге не треба.
   */
  private async performRefresh(accountId: string, refreshTokenAtCall: string): Promise<RefreshResult> {
    this.accounts.syncFromStorage(accountId);
    const account = this.find(accountId);
    if (!account?.accessToken || !account.refreshToken) {
      return 'unauthorized';
    }
    if (account.refreshToken !== refreshTokenAtCall) {
      return 'ok';
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

    try {
      const body = await firstValueFrom(
        this.http
          .post<Untrusted<AuthTokens>>(url, { accessToken: account.accessToken, refreshToken: account.refreshToken } satisfies RefreshTokensCommand)
          .pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const tokens = toAuthTokens(body);
      if (!tokens) {
        // Старі токени лишаємо: з такою відповіддю акаунт лишився б узагалі без токенів.
        console.warn('[AuthService] Відповідь на оновлення токенів без токенів', body);
        return 'error';
      }
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

  private find(accountId: string | null): StoredAccount | null {
    return this.accounts.accounts().find((account) => account.id === accountId) ?? null;
  }

  private runExclusive<T>(task: () => Promise<T>): Promise<T> {
    // Web Locks є не скрізь (старі браузери) — тоді обходимось без блокування.
    return typeof navigator !== 'undefined' && navigator.locks
      ? navigator.locks.request(REFRESH_LOCK_NAME, task)
      : task();
  }
}
