import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { UserProfile } from './user-profile';

const API_USERS_PATH = '/api/v1/users';
const REQUEST_TIMEOUT_MS = 10000;

export interface NewUser {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export type CreateUserResult = 'created' | RequestFailure;
export type ChangePasswordResult = 'changed' | RequestFailure;
export type UpdateProfileResult = 'updated' | RequestFailure;

/**
 * Тіло `PUT /api/v1/users/{id}`: сервер вимагає всі поля (кожне — рядок або
 * `null`). Категорія займенників — число з `PronounCategory`.
 */
export interface ProfileUpdate {
  firstName: string | null;
  lastName: string | null;
  patronymic: string | null;
  internalPosition: string | null;
  phoneNumber: string | null;
  country: string | null;
  emergencyContact: string | null;
  preferredLanguage: string | null;
  pronouns: string | null;
  pronounCategory: number | null;
  avatarUrl: string | null;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /** `POST <сервер>/api/v1/users` — створення користувача (першого адміна). */
  async create(user: NewUser): Promise<CreateUserResult> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}`;
    console.log(`[UsersService] Створюю користувача: ${url}`);

    try {
      await firstValueFrom(this.http.post(url, user).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[UsersService] Користувача створено');
      return 'created';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[UsersService] Не вдалося створити користувача (${failure})`, error);
      return failure;
    }
  }

  /**
   * `GET <сервер>/api/v1/users/{id}` — детальний профіль користувача (потрібне
   * право `ViewUser`). Повертає профіль або причину невдачі (404 — `error`).
   */
  async get(id: string): Promise<UserProfile | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}/${encodeURIComponent(id)}`;
    console.log(`[UsersService] Запитую профіль користувача: ${url}`);

    try {
      return await firstValueFrom(this.http.get<UserProfile>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[UsersService] Не вдалося отримати профіль (${failure})`, error);
      return failure;
    }
  }

  /**
   * `GET <сервер>/api/v1/users/me/permissions` — унікальні права поточного
   * користувача, за якими будується інтерфейс. Повертає список або причину
   * невдачі; відповідь не масивом вважається помилкою. Токен додає
   * `authInterceptor`.
   */
  async getMyPermissions(): Promise<string[] | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}/me/permissions`;
    console.log(`[UsersService] Запитую права поточного користувача: ${url}`);

    try {
      const body = await firstValueFrom(this.http.get<unknown>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      if (!Array.isArray(body)) {
        console.warn('[UsersService] Права прийшли не списком', body);
        return 'error';
      }
      return body.filter((permission): permission is string => typeof permission === 'string');
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[UsersService] Не вдалося отримати права (${failure})`, error);
      return failure;
    }
  }

  /**
   * `PUT <сервер>/api/v1/users/{id}` — оновлення профілю (потрібне право
   * `UpdateUser`; 204 без тіла). Токен додає `authInterceptor`.
   */
  async updateProfile(id: string, profile: ProfileUpdate): Promise<UpdateProfileResult> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}/${encodeURIComponent(id)}`;
    console.log(`[UsersService] Оновлюю профіль користувача: ${url}`);

    try {
      await firstValueFrom(this.http.put(url, profile).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[UsersService] Профіль оновлено');
      return 'updated';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[UsersService] Не вдалося оновити профіль (${failure})`, error);
      return failure;
    }
  }

  /**
   * `POST <сервер>/api/v1/users/me/password` — зміна пароля поточного
   * користувача (204 без тіла). Токен додає `authInterceptor`. Паролі не
   * потрапляють у консоль ні в тілі, ні в помилці.
   */
  async changePassword(currentPassword: string, newPassword: string): Promise<ChangePasswordResult> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}/me/password`;
    console.log(`[UsersService] Змінюю пароль: ${url}`);

    try {
      await firstValueFrom(this.http.post(url, { currentPassword, newPassword }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[UsersService] Пароль змінено');
      return 'changed';
    } catch (error) {
      const failure = toRequestFailure(error);
      // Саму помилку не виводимо: у ній є тіло запиту з паролями.
      console.warn(`[UsersService] Не вдалося змінити пароль (${failure})`);
      return failure;
    }
  }
}
