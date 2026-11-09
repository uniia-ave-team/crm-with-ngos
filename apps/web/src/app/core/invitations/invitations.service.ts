import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AuthTokens } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

const API_INVITATIONS_PATH = '/api/v1/invitations';
const REQUEST_TIMEOUT_MS = 10000;

/** Що сервер знає про запрошення (відповідь `GET /api/v1/invitations/validate`). */
export interface InvitationDetails {
  /** Пошта, на яку надіслано запрошення: колега реєструється саме з нею. */
  readonly email: string;
}

/** Дані, які колега вводить, завершуючи реєстрацію (`POST /api/v1/invitations/accept`). */
export interface InvitationRegistration {
  readonly firstName: string;
  readonly lastName: string;
  /** Порожнє йде в запит як `null`. */
  readonly patronymic: string | null;
  readonly password: string;
  readonly confirmPassword: string;
}

/** Запрошення колеги (відповідь `POST /api/v1/invitations`): токен, за яким він завершує реєстрацію. */
export interface Invitation {
  readonly token: string;
  /** Коли токен перестає діяти (ISO-дата з відповіді). */
  readonly expiresAt: string;
}

/** Запрошення колег (`/api/v1/invitations`). */
@Injectable({ providedIn: 'root' })
export class InvitationsService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /**
   * `POST <сервер>/api/v1/invitations` — запрошує колегу (потрібне право `CreateUser`; тіло
   * `{email, roleIds}`, ролей має бути хоча б одна). Користувача це ще не створює: сервер
   * віддає токен, за яким колега завершує реєстрацію (`POST /invitations/accept`). Повертає
   * токен або причину невдачі (`invalid` — 400, зокрема коли пошта вже зайнята, а
   * `error` — ще й відповідь без токена).
   */
  async invite(email: string, roleIds: readonly string[]): Promise<Invitation | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_INVITATIONS_PATH}`;
    console.log(`[InvitationsService] Створюю запрошення: ${url}`);
    try {
      const body = await firstValueFrom(this.http.post<Record<string, unknown>>(url, { email, roleIds }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const token = body?.['token'];
      if (typeof token !== 'string' || !token) {
        console.warn('[InvitationsService] Відповідь без токена запрошення', body);
        return 'error';
      }
      const expiresAt = body['expiresAt'];
      return { token, expiresAt: typeof expiresAt === 'string' ? expiresAt : '' };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[InvitationsService] Не вдалося створити запрошення (${failure})`, error);
      return failure;
    }
  }

  /**
   * `GET <server>/api/v1/invitations/validate?token=…` (без авторизації) — перевіряє токен
   * із посилання й віддає пошту, на яку його видано. Сервер явний: колега відкриває
   * запрошення, ще не маючи акаунта на цьому сервері (див. `ConnectionService.pageServerUrl`).
   * Повертає дані або причину невдачі (недійсний чи прострочений токен — `invalid`).
   */
  async validate(server: string, token: string): Promise<InvitationDetails | RequestFailure> {
    const url = `${server}${API_INVITATIONS_PATH}/validate`;
    console.log(`[InvitationsService] Перевіряю запрошення: ${url}`);
    try {
      const body = await firstValueFrom(
        this.http.get<Record<string, unknown>>(url, { params: new HttpParams().set('token', token) }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const email = body?.['email'];
      if (typeof email !== 'string' || !email) {
        console.warn('[InvitationsService] Відповідь без пошти запрошення', body);
        return 'error';
      }
      return { email };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[InvitationsService] Запрошення не пройшло перевірку (${failure})`, error);
      return failure;
    }
  }

  /**
   * `POST <server>/api/v1/invitations/accept` (без авторизації) — завершує реєстрацію
   * колеги за токеном запрошення. Повертає пару токенів уже створеного користувача або
   * причину невдачі (`invalid` — 400: токен недійсний чи дані не пройшли перевірку).
   */
  async accept(server: string, token: string, registration: InvitationRegistration): Promise<AuthTokens | RequestFailure> {
    const url = `${server}${API_INVITATIONS_PATH}/accept`;
    console.log(`[InvitationsService] Завершую реєстрацію за запрошенням: ${url}`);
    try {
      const body = await firstValueFrom(
        this.http.post<Record<string, unknown>>(url, { token, ...registration }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const accessToken = body?.['accessToken'];
      const refreshToken = body?.['refreshToken'];
      if (typeof accessToken !== 'string' || !accessToken || typeof refreshToken !== 'string' || !refreshToken) {
        console.warn('[InvitationsService] Відповідь без токенів доступу', body);
        return 'error';
      }
      return { accessToken, refreshToken };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[InvitationsService] Не вдалося завершити реєстрацію (${failure})`, error);
      return failure;
    }
  }
}
