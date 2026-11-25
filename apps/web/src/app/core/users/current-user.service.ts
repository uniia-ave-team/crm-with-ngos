import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Injector, Signal, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { UserProfileDto } from '../../api/generated/models';
import { AccountThumbnail, AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { readJwtRoleIds } from '../auth/jwt';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { ProtectedImageService } from '../http/protected-image.service';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { LanguageService } from '../i18n/language.service';
import { ThumbnailFit, ThumbnailService } from '../images/thumbnail.service';
import { NgoService } from '../ngo/ngo.service';
import { UserProfile, sanitizeRoles, toUserProfile } from './user-profile';
import { UsersService } from './users.service';

const API_CURRENT_USER_PATH = '/api/v1/users/me';

/** Профіль поточного користувача (`GET /api/v1/users/me`). */
export type CurrentUser = UserProfile;

/**
 * Результат перевірки входу:
 * - `ok` — сервер підтвердив токен, профіль завантажено;
 * - `unauthorized` — сервер не прийняв токени (навіть після їх оновлення) або їх нема, чи
 *   користувача на сервері вже нема (404 від API: акаунт видалили);
 * - `unavailable` — перевірити не вдалося (мережа, 5xx, 404 не від API, відповідь не за
 *   контрактом, або поки йшов запит, активним став інший акаунт): це не доказ, що
 *   користувач не увійшов.
 */
export type CurrentUserResult = 'ok' | 'unauthorized' | 'unavailable';

/** Розмір мініатюри аватара (px): плитка 44px, з запасом для щільних екранів. */
const AVATAR_THUMBNAIL_SIZE = 96;
/** Розмір мініатюри логотипа ГО (px): значок на плитці малий. */
const LOGO_THUMBNAIL_SIZE = 64;

/**
 * Поточний користувач — профіль активного акаунта. Запит `GET /api/v1/users/me` одночасно
 * перевіряє, що вхід актуальний (токен чинний, користувач активний), і дає профіль для меню,
 * аватара й форм. Разом із ним підтягуються права (`/users/me/permissions`) і організація
 * (`/ngo`) — усе це йде в знімок акаунта.
 *
 * Профіль перепитується при кожній зміні сторінки (`authGuard` → `reload`), тож зміни в БД
 * потрапляють в інтерфейс без перезавантаження. Якщо ролі користувача змінили, токени
 * оновлюються перед запитом прав (`refreshIfRolesChanged`): інакше сервер давав би старі права. Коли активним стає інший акаунт, профіль
 * попереднього забувається; новий завантажує той, хто зробив акаунт активним (пошук чи
 * перемикання акаунта, страж сторінки після входу).
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);
  private readonly ngo = inject(NgoService);
  private readonly users = inject(UsersService);
  private readonly images = inject(ProtectedImageService);
  private readonly thumbnails = inject(ThumbnailService);
  // Сервіс мови беремо, лише коли в профілі є мова: так профіль можна
  // використовувати й там, де перекладів немає (наприклад, у тестах без
  // TranslateService), і сервіс мови не створюється зайвий раз.
  private readonly injector = inject(Injector);

  private readonly userSignal = signal<CurrentUser | null>(null);
  readonly user: Signal<CurrentUser | null> = this.userSignal.asReadonly();

  /** Права активного акаунта (з його знімка в `localStorage`); порожньо, доки не отримано. */
  readonly permissions: Signal<readonly string[]> = computed(() => this.accounts.active()?.permissions ?? []);

  /** Запити профілю, що тривають: по одному на акаунт. */
  private readonly requestsInFlight = new Map<string, Promise<CurrentUserResult>>();
  /** Останнє оновлення прав у знімку акаунта (завершене, якщо його не було). */
  private permissionsPromise: Promise<void> = Promise.resolve();
  /** Акаунт (`AuthService.sessionId`), для якого завантажено `user`. */
  private readonly profileSession = signal<string | null>(null);
  /**
   * Профіль завантажено саме для активного акаунта. Тимчасове перемикання (пошук акаунта,
   * `activate`) профіль попереднього акаунта не скидає: він лишається в `user`, поки перемикання
   * не вдалося, але для нового активного акаунта вважається чужим (`false`).
   */
  readonly profileLoaded: Signal<boolean> = computed(() => {
    const session = this.auth.sessionId();
    return session !== null && this.userSignal() !== null && this.profileSession() === session;
  });
  constructor() {
    // Інший акаунт (вихід, вхід, перемикання) — профіль попереднього забуваємо.
    effect(() => {
      const session = this.auth.sessionId();
      // Під час пошуку акаунта (`AccountRestoreService`) і перемикання по плитці активний
      // акаунт змінюється тимчасово й ці зміни не наші: профіль для них вантажить сам пошук.
      const searching = this.accounts.switching();
      untracked(() => {
        // Тимчасове перемикання профіль не скидає: аватар і меню лишаються попередніми, доки
        // перемикання не вдалося (тоді профіль новий, а коли ні — попередній лишається як був).
        if (searching) {
          return;
        }
        // Профіль, завантажений уже для цього акаунта, не чіпаємо (effect може
        // спрацювати вже після відповіді сервера).
        if (this.profileSession() !== session) {
          this.userSignal.set(null);
        }
      });
    });
  }

  /** Перевіряє вхід: профіль активного акаунта вже завантажено — `ok` без запиту, інакше `GET /users/me`. */
  ensure(): Promise<CurrentUserResult> {
    if (this.profileLoaded() && this.auth.hasTokens()) {
      return Promise.resolve('ok');
    }
    return this.request();
  }

  /**
   * Перепитує профіль (`GET /users/me`) попри те, що він уже в памʼяті: так
   * зміни в БД (дані профілю, назва й логотип ГО) потрапляють в інтерфейс без
   * перезавантаження. Викликається при кожній зміні сторінки (`authGuard`).
   * Невдалий запит нічого не скидає: профіль лишається попереднім, а
   * недійсні токени обробляє `authInterceptor` (оновлення або вихід). Запити
   * для одного акаунта, що збіглися в часі, діляться одним.
   */
  reload(): Promise<CurrentUserResult> {
    if (!this.auth.hasTokens()) {
      return Promise.resolve('unauthorized');
    }
    return this.request();
  }

  /**
   * Завершується, коли права з останнього запиту профілю вже у знімку акаунта
   * (або їх не вдалося отримати). Права підтягуються після відповіді `/users/me`
   * й не блокують його, тож той, кому потрібні саме актуальні права (страж
   * сторінки за правами), чекає тут.
   */
  permissionsSettled(): Promise<void> {
    return this.permissionsPromise;
  }

  /**
   * Запит профілю активного акаунта. Спільний лише для того самого акаунта: інакше перевірка
   * одного акаунта отримала б відповідь, що стосується іншого.
   */
  private request(): Promise<CurrentUserResult> {
    const session = this.auth.sessionId();
    if (session === null) {
      return Promise.resolve('unauthorized');
    }
    let inFlight = this.requestsInFlight.get(session);
    if (!inFlight) {
      inFlight = this.load(session).finally(() => {
        this.requestsInFlight.delete(session);
      });
      this.requestsInFlight.set(session, inFlight);
    }
    return inFlight;
  }

  private async load(session: string): Promise<CurrentUserResult> {
    const url = `${this.connection.serverUrl()}${API_CURRENT_USER_PATH}`;

    try {
      // Токен додає, а за потреби оновлює authInterceptor.
      const body = await firstValueFrom(this.http.get<Untrusted<UserProfileDto>>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      // Поки йшов запит, активним став інший акаунт — профіль уже не його. Це не відмова
      // сервера: токени цього акаунта тут ні до чого, тож не `unauthorized`.
      if (session !== this.auth.sessionId()) {
        return 'unavailable';
      }
      const user = toUserProfile(body);
      if (!user) {
        console.warn('[CurrentUserService] Профіль не відповідає контракту', body);
        return 'unavailable';
      }
      this.profileSession.set(session);
      this.applyProfile(session, user);
      void this.syncNgo(session);
      this.permissionsPromise = this.refreshIfRolesChanged(session, user).then(() => this.syncPermissions(session));
      return 'ok';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        console.warn('[CurrentUserService] Сервер не прийняв токени');
        return 'unauthorized';
      }
      if (isUserGone(error)) {
        // Токен ще чинний, але користувача за ним уже нема (акаунт видалили): сесія цього
        // акаунта закінчилась. Він лишається з позначкою «Сеанс закінчився» й перестає бути
        // активним, а `SessionService` веде на повторний вхід.
        console.warn('[CurrentUserService] Користувача на сервері вже нема', error);
        this.auth.expireSession(session, 'unauthorized');
        return 'unauthorized';
      }
      console.warn('[CurrentUserService] Перевірити вхід не вдалося', error);
      return 'unavailable';
    }
  }

  /**
   * Вносить у профіль у памʼяті (і в знімок акаунта) зміни, які користувач щойно
   * успішно зберіг на сервері, не перепитуючи його. Профіль не завантажено —
   * нічого не робить.
   */
  applyLocal(changes: Partial<UserProfile>): void {
    const session = this.auth.sessionId();
    const current = this.userSignal();
    if (!session || !current || this.profileSession() !== session) {
      return;
    }
    this.applyProfile(session, { ...current, ...changes });
  }

  /**
   * Кладе профіль у памʼять і оновлює знімок у записі акаунта, щоб плитка у
   * вікні акаунта малювалась без запитів. Пошту беремо з профілю, лише якщо вона
   * там є: інакше лишається та, з якою ввійшли. Мова інтерфейсу перемикається
   * на `preferredLanguage` профілю: це спільна точка першого запиту
   * (`/users/me`), оновлення при відкритті «Дані профілю» і збереження змін.
   */
  private applyProfile(session: string, user: UserProfile): void {
    this.userSignal.set(user);
    if (user.preferredLanguage) {
      this.injector.get(LanguageService).applyPreferred(user.preferredLanguage);
    }
    this.accounts.update(session, {
      firstName: user.firstName ?? '',
      lastName: user.lastName ?? '',
      patronymic: user.patronymic ?? '',
      avatarUrl: user.avatarUrl ?? null,
      userId: user.id,
      roles: sanitizeRoles(user.roles),
      ...(user.email ? { email: user.email } : {}),
    });
    void this.syncAvatarThumbnail(session, user.avatarUrl ?? null);
  }

  /**
   * Ролі користувача змінили (профіль `/users/me` каже одне, а токен виданий з іншими —
   * `role_id`): сервер дає права за ролями з токена, поки його не оновлено, тож і
   * `/users/me/permissions`, і доступ лишались би старими. Тоді токени оновлюються одразу
   * (`AuthService.refresh`), і права далі запитуються вже з новим токеном. Ролі ті самі чи токен
   * нечитний — нічого не робить. Невдале оновлення обробляє сам `refresh` (застарілий акаунт).
   */
  private async refreshIfRolesChanged(session: string, user: UserProfile): Promise<void> {
    const tokenRoles = readJwtRoleIds(this.auth.accessTokenOf(session));
    if (tokenRoles === null) {
      return;
    }
    const profileRoles = new Set(user.roles.map((role) => role.id));
    const same = tokenRoles.length === profileRoles.size && tokenRoles.every((id) => profileRoles.has(id));
    if (!same) {
      await this.auth.refresh(session);
    }
  }

  /**
   * Підтягує права користувача (`GET /users/me/permissions`) у знімок акаунта.
   * Іде разом із кожним запитом профілю, тож права оновлюються при кожній зміні
   * сторінки. Не блокує перевірку входу; невдача лишає попередній список.
   */
  private async syncPermissions(session: string): Promise<void> {
    const permissions = await this.users.getMyPermissions();
    if (typeof permissions === 'string') {
      return;
    }
    // Поки йшов запит, акаунт змінився — права чужого акаунта не записуємо.
    if (session !== this.auth.sessionId()) {
      return;
    }
    const current = this.accounts.active()?.permissions ?? [];
    const same = current.length === permissions.length && current.every((permission, index) => permission === permissions[index]);
    if (!same) {
      this.accounts.update(session, { permissions });
    }
  }

  /** Підтягує назву й логотип ГО в знімок акаунта (не блокує перевірку входу). */
  private async syncNgo(session: string): Promise<void> {
    const ngo = await this.ngo.fetch();
    if (ngo !== null) {
      this.accounts.update(session, { ngoName: ngo.name, ngoLogoUrl: ngo.logoUrl });
      await this.syncLogoThumbnail(session, ngo.logoUrl);
    }
  }

  private account(session: string) {
    return this.accounts.accounts().find((account) => account.id === session);
  }

  /**
   * Мініатюра аватара в знімку акаунта (для плитки у вікні акаунтів, див.
   * `AccountThumbnail`): робиться, коли аватар зʼявився чи змінився (інша адреса
   * `?v=…`), знімається, коли аватара нема. Невдача лишає попередню мініатюру.
   */
  private async syncAvatarThumbnail(session: string, avatarUrl: string | null): Promise<void> {
    const thumbnail = await this.thumbnailFor(session, avatarUrl, this.account(session)?.avatarThumb ?? null, AVATAR_THUMBNAIL_SIZE, 'cover');
    if (thumbnail !== undefined && session === this.auth.sessionId()) {
      this.accounts.update(session, { avatarThumb: thumbnail });
    }
  }

  /** Мініатюра логотипа ГО в знімку акаунта (для значка на плитці), як і в аватара. */
  private async syncLogoThumbnail(session: string, logoUrl: string | null): Promise<void> {
    const thumbnail = await this.thumbnailFor(session, logoUrl, this.account(session)?.ngoLogoThumb ?? null, LOGO_THUMBNAIL_SIZE, 'contain');
    if (thumbnail !== undefined && session === this.auth.sessionId()) {
      this.accounts.update(session, { ngoLogoThumb: thumbnail });
    }
  }

  /**
   * Нова мініатюра для `url`: `undefined` — міняти нічого не треба (вже зроблена з
   * цієї адреси чи зробити не вдалося), `null` — зображення нема, мініатюру
   * прибрати, інакше — нова.
   */
  private async thumbnailFor(
    session: string,
    url: string | null,
    current: AccountThumbnail | null,
    size: number,
    fit: ThumbnailFit,
  ): Promise<AccountThumbnail | null | undefined> {
    if (!url) {
      return current === null ? undefined : null;
    }
    if (current?.url === url) {
      return undefined;
    }
    const server = this.account(session)?.server ?? this.connection.serverUrl();
    let absolute: string;
    try {
      absolute = new URL(url, `${server}/`).toString();
    } catch {
      return undefined;
    }
    // Зображення беремо з кешу (його вже могли завантажити для рейла) і віддаємо одразу, як зробимо мініатюру.
    const lease = this.images.acquire(absolute);
    try {
      const source = await lease.src;
      if (!source || session !== this.auth.sessionId()) {
        return undefined;
      }
      const data = await this.thumbnails.create(source, size, fit);
      return data ? { url, data } : undefined;
    } finally {
      lease.release();
    }
  }
}

/**
 * 404 саме від API (тіло — ProblemDetails зі `status: 404`): користувача, чий токен надіслано,
 * на сервері нема. 404 без такого тіла (проксі, інший сервіс за адресою) нічого не доводить.
 */
function isUserGone(error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 404) {
    return false;
  }
  const body = error.error as Untrusted<{ status: number }> | null;
  return typeof body === 'object' && body !== null && body.status === 404;
}
