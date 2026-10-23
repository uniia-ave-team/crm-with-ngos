import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Injector, Signal, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { LanguageService } from '../i18n/language.service';
import { NgoService } from '../ngo/ngo.service';
import { UserProfile } from './user-profile';
import { UsersService } from './users.service';

const API_CURRENT_USER_PATH = '/api/v1/users/me';
const REQUEST_TIMEOUT_MS = 10000;

/** Профіль поточного користувача (`GET /api/v1/users/me`). */
export type CurrentUser = UserProfile;

/**
 * Результат перевірки входу:
 * - `ok` — сервер підтвердив токен, профіль завантажено;
 * - `unauthorized` — сервер не прийняв токени (навіть після їх оновлення) або їх нема;
 * - `unavailable` — перевірити не вдалося (мережа, 5xx, 404): це не доказ, що
 *   користувач не увійшов.
 */
export type CurrentUserResult = 'ok' | 'unauthorized' | 'unavailable';

/**
 * Поточний користувач. Перший запит `GET /api/v1/users/me` одночасно
 * перевіряє, що вхід актуальний (токен чинний, користувач активний), і дає
 * профіль, на основі якого далі буде будуватися меню. Профіль живе в памʼяті
 * до кінця сесії (вихід або новий вхід скидає його).
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);
  private readonly ngo = inject(NgoService);
  private readonly users = inject(UsersService);
  // Сервіс мови беремо, лише коли в профілі є мова: так профіль можна
  // використовувати й там, де перекладів немає (наприклад, у тестах без
  // TranslateService), і сервіс мови не створюється зайвий раз.
  private readonly injector = inject(Injector);

  private readonly userSignal = signal<CurrentUser | null>(null);
  readonly user: Signal<CurrentUser | null> = this.userSignal.asReadonly();

  /** Права активного акаунта (з його знімка в `localStorage`); порожньо, доки не отримано. */
  readonly permissions: Signal<readonly string[]> = computed(() => this.accounts.active()?.permissions ?? []);

  private inFlight: Promise<CurrentUserResult> | null = null;
  /** Останнє оновлення прав у знімку акаунта (завершене, якщо його не було). */
  private permissionsPromise: Promise<void> = Promise.resolve();
  /** Акаунт (`AuthService.sessionId`), для якого завантажено `user`. */
  private loadedForSession: string | null = null;

  constructor() {
    // Інший акаунт (вихід, вхід, перемикання) — профіль попереднього забуваємо.
    effect(() => {
      const session = this.auth.sessionId();
      untracked(() => {
        // Профіль, завантажений уже для цього акаунта, не чіпаємо (effect може
        // спрацювати вже після відповіді сервера).
        if (this.loadedForSession !== session) {
          this.userSignal.set(null);
        }
      });
    });
  }

  /** Перевіряє вхід: профіль уже завантажено — `ok` без запиту, інакше `GET /users/me`. */
  ensure(): Promise<CurrentUserResult> {
    if (this.userSignal() && this.auth.hasTokens()) {
      return Promise.resolve('ok');
    }
    return this.request();
  }

  /**
   * Перепитує профіль (`GET /users/me`) попри те, що він уже в памʼяті: так
   * зміни в БД (дані профілю, назва й логотип ГО) потрапляють в інтерфейс без
   * перезавантаження. Викликається при кожній зміні сторінки (`authGuard`).
   * Невдалий запит нічого не скидає: профіль лишається попереднім, а
   * недійсні токени обробляє `authInterceptor` (оновлення або вихід). Запити,
   * що збіглися в часі, діляться одним.
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

  private request(): Promise<CurrentUserResult> {
    if (!this.inFlight) {
      this.inFlight = this.load().finally(() => {
        this.inFlight = null;
      });
    }
    return this.inFlight;
  }

  private async load(): Promise<CurrentUserResult> {
    const url = `${this.connection.serverUrl()}${API_CURRENT_USER_PATH}`;
    console.log(`[CurrentUserService] Запитую поточного користувача: ${url}`);
    const session = this.auth.sessionId();

    try {
      // Токен додає, а за потреби оновлює authInterceptor.
      const user = await firstValueFrom(this.http.get<CurrentUser>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      // Поки йшов запит, акаунт змінився (вихід) — результат не береться.
      if (session === null || session !== this.auth.sessionId()) {
        return 'unauthorized';
      }
      this.loadedForSession = session;
      this.applyProfile(session, user);
      void this.syncNgo(session);
      this.permissionsPromise = this.syncPermissions(session);
      console.log('[CurrentUserService] Вхід підтверджено', user);
      return 'ok';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        console.warn('[CurrentUserService] Сервер не прийняв токени');
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
    if (!session || !current) {
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
      avatarUrl: user.avatarUrl ?? null,
      userId: user.id,
      ...(user.email ? { email: user.email } : {}),
    });
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
    }
  }
}
