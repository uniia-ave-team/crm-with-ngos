import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { UserRole } from './user-profile';

const API_USERS_PATH = '/api/v1/users';
const REQUEST_TIMEOUT_MS = 10000;
/** Завантаження файлу триває довше за звичайний запит. */
const UPLOAD_TIMEOUT_MS = 60000;

export interface NewUser {
  firstName: string;
  lastName: string;
  /** По батькові; необовʼязкове (`null` — не вказано), але поле в запиті обовʼязкове. */
  patronymic: string | null;
  email: string;
  password: string;
  confirmPassword: string;
}

/**
 * Колега зі списку `GET /api/v1/users`. Таблиця колег читає всі ці поля, але
 * сервер поки віддає лише `id`, пошту й імена: решта (`null` / порожній список)
 * виставляється в `toUserSummary` — коли відповідь їх міститиме, міняти треба
 * лише її.
 */
export interface UserSummary {
  readonly id: string;
  readonly email: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly phoneNumber: string | null;
  readonly country: string | null;
  readonly emergencyContact: string | null;
  readonly pronouns: string | null;
  /** Категорія займенників — число з `PronounCategory`. */
  readonly pronounCategory: number | null;
  readonly avatarUrl: string | null;
  readonly roles: readonly UserRole[];
}

/**
 * Сортування списку колег: `OrderBy` (одне поле — сервер приймає лише одне імʼя
 * властивості) і `SortOrder` (`asc` від меншого до більшого, `desc` навпаки).
 * Порожній `orderBy` — сортування не передається.
 */
export interface UsersSort {
  readonly orderBy: string;
  readonly sortOrder: 'asc' | 'desc';
}

/** Сторінка списку колег. */
export interface UsersPage {
  readonly items: readonly UserSummary[];
  readonly hasNextPage: boolean;
}

export type CreateUserResult = 'created' | RequestFailure;
export type ChangePasswordResult = 'changed' | RequestFailure;
export type UpdateProfileResult = 'updated' | RequestFailure;
export type UploadAvatarResult = 'uploaded' | RequestFailure;

/**
 * Тіло `PUT /api/v1/users/me`: сервер вимагає всі поля (кожне — рядок або
 * `null`). Категорія займенників — число з `PronounCategory`. Аватара тут
 * нема: він завантажується окремо (`uploadAvatar`).
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
   * `GET <сервер>/api/v1/users?PageNumber=&PageSize=[&OrderBy=&SortOrder=]` — одна
   * сторінка колег (потрібне право `ViewUser`). Сортування (`sort`) передається
   * лише коли воно є й має непорожній `orderBy`: без нього сервер сортує за
   * замовчуванням (за імʼям). Елементи без `id` пропускаються; відсутні імена й пошта —
   * порожні рядки. Повертає сторінку або причину невдачі; відповідь без списку
   * `items` вважається помилкою. Токен додає `authInterceptor`.
   */
  async list(pageNumber: number, pageSize: number, sort: UsersSort | null = null): Promise<UsersPage | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}`;
    console.log(`[UsersService] Запитую колег (сторінка ${pageNumber}, по ${pageSize}): ${url}`);

    try {
      let params = new HttpParams().set('PageNumber', pageNumber).set('PageSize', pageSize);
      if (sort?.orderBy.trim()) {
        params = params.set('OrderBy', sort.orderBy.trim()).set('SortOrder', sort.sortOrder);
      }
      const body = await firstValueFrom(this.http.get<Record<string, unknown>>(url, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      if (!Array.isArray(body?.['items'])) {
        console.warn('[UsersService] Відповідь без списку колег', body);
        return 'error';
      }
      const items = (body['items'] as (Record<string, unknown> | null)[]).flatMap((item) => {
        const user = toUserSummary(item);
        return user ? [user] : [];
      });
      return { items, hasNextPage: body['hasNextPage'] === true };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[UsersService] Не вдалося отримати колег (${failure})`, error);
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
   * `PUT <сервер>/api/v1/users/me` — оновлення профілю поточного користувача
   * (лише авторизація, права `UpdateUser` не потрібно; 204 без тіла). Токен
   * додає `authInterceptor`.
   */
  async updateProfile(profile: ProfileUpdate): Promise<UpdateProfileResult> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}/me`;
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
   * `POST <сервер>/api/v1/users/me/avatar` — завантаження аватара поточного
   * користувача (`multipart/form-data`, поле `file`; лише авторизація; 200 без
   * тіла). Передається саме зображення, а не посилання: посилання на нього
   * призначає сервер, тож новий `avatarUrl` читається з профілю (`GET /users/me`).
   */
  async uploadAvatar(file: File): Promise<UploadAvatarResult> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}/me/avatar`;
    console.log(`[UsersService] Завантажую аватар: ${url}`);

    try {
      const form = new FormData();
      form.append('file', file, file.name);
      await firstValueFrom(this.http.post(url, form).pipe(timeout(UPLOAD_TIMEOUT_MS)));
      console.log('[UsersService] Аватар завантажено');
      return 'uploaded';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[UsersService] Не вдалося завантажити аватар (${failure})`, error);
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

/**
 * Колега з елемента списку; без `id` — `null`. Поля, яких список поки не віддає,
 * тут сталі (`null` й порожній список): коли сервер почне їх віддавати, їх
 * читають звідси з `item`, а таблиця колег лишається як є.
 */
function toUserSummary(item: Record<string, unknown> | null): UserSummary | null {
  if (typeof item?.['id'] !== 'string') {
    return null;
  }
  const text = (value: unknown) => (typeof value === 'string' ? value : '');
  return {
    id: item['id'],
    email: text(item['email']),
    firstName: text(item['firstName']),
    lastName: text(item['lastName']),
    phoneNumber: null,
    country: null,
    emergencyContact: null,
    pronouns: null,
    pronounCategory: null,
    avatarUrl: null,
    roles: [],
  };
}
