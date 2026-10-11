import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, Signal, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { NgoService } from '../ngo/ngo.service';

const API_CURRENT_USER_PATH = '/api/v1/users/me';
const REQUEST_TIMEOUT_MS = 10000;

/** Профіль поточного користувача (`GET /api/v1/users/me`). Поля, які ще не потрібні, не описано. */
export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  patronymic?: string | null;
  internalPosition?: string | null;
  phoneNumber?: string | null;
  country?: string | null;
  preferredLanguage?: string | null;
  pronouns?: string | null;
  avatarUrl?: string | null;
  roles: unknown[];
  customFields: unknown[];
}

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

  private readonly userSignal = signal<CurrentUser | null>(null);
  readonly user: Signal<CurrentUser | null> = this.userSignal.asReadonly();

  private inFlight: Promise<CurrentUserResult> | null = null;
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
      this.userSignal.set(user);
      // Знімок у записі акаунта: плитка у вікні акаунта малюється без запитів.
      this.accounts.update(session, {
        firstName: user.firstName ?? '',
        lastName: user.lastName ?? '',
        avatarUrl: user.avatarUrl ?? null,
        userId: user.id,
      });
      void this.syncNgo(session);
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

  /** Підтягує назву й логотип ГО в знімок акаунта (не блокує перевірку входу). */
  private async syncNgo(session: string): Promise<void> {
    const ngo = await this.ngo.fetch();
    if (ngo !== null) {
      this.accounts.update(session, { ngoName: ngo.name, ngoLogoUrl: ngo.logoUrl });
    }
  }
}
