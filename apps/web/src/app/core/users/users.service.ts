import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { CreateUserCommand, PagedResultOfUserDto, UpdatePasswordRequest, UpdateProfileRequest, UserDto } from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS, UPLOAD_TIMEOUT_MS } from '../http/timeouts';
import { UserRole, sanitizeRoles } from './user-profile';

const API_USERS_PATH = '/api/v1/users';

/** Тіло `POST /api/v1/users`: по батькові необовʼязкове (`null` — не вказано), але поле в запиті обовʼязкове. */
export type NewUser = CreateUserCommand;

/**
 * Колега зі списку `GET /api/v1/users`. Поля після прізвища (по батькові, телефон, країна,
 * екстрений контакт, займенники з категорією, аватар, ролі) сервер віддає не в усіх
 * версіях: якщо відповідь їх не містить (чи вони не того типу), вони `null` / порожній
 * список (`toUserSummary`), і таблиця показує свої запасні значення.
 */
export interface UserSummary {
  readonly id: string;
  readonly email: string;
  readonly firstName: string;
  readonly lastName: string;
  /** По батькові; `null` — нема. */
  readonly patronymic: string | null;
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
 * Тіло `PUT /api/v1/users/me`: сервер вимагає всі поля (імʼя й прізвище — рядки, решта —
 * рядок або `null`). Категорія займенників — число з `PronounCategory`. Аватара тут нема:
 * він завантажується окремо (`uploadAvatar`).
 */
export type ProfileUpdate = UpdateProfileRequest;

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /** `POST <сервер>/api/v1/users` — створення користувача (першого адміна). */
  async create(user: NewUser): Promise<CreateUserResult> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}`;

    try {
      await firstValueFrom(this.http.post(url, user).pipe(timeout(REQUEST_TIMEOUT_MS)));
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
   * лише коли воно є й має непорожній `orderBy`. Без нього сервер сортує за `id`, тобто
   * у випадковому для людини порядку, тож сторінка колег завжди передає своє (за
   * замовчуванням — за ПІБ). Елементи без `id` пропускаються; відсутні імена й пошта —
   * порожні рядки. Повертає сторінку або причину невдачі; відповідь без списку
   * `items` вважається помилкою. Токен додає `authInterceptor`.
   */
  async list(pageNumber: number, pageSize: number, sort: UsersSort | null = null): Promise<UsersPage | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_USERS_PATH}`;

    try {
      let params = new HttpParams().set('PageNumber', pageNumber).set('PageSize', pageSize);
      if (sort?.orderBy.trim()) {
        params = params.set('OrderBy', sort.orderBy.trim()).set('SortOrder', sort.sortOrder);
      }
      const body = await firstValueFrom(this.http.get<Untrusted<PagedResultOfUserDto>>(url, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      if (!Array.isArray(body?.items)) {
        console.warn('[UsersService] Відповідь без списку колег', body);
        return 'error';
      }
      const items = (body.items as Untrusted<UserDto>[]).flatMap((item) => {
        const user = toUserSummary(item);
        return user ? [user] : [];
      });
      return { items, hasNextPage: body.hasNextPage === true };
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

    try {
      await firstValueFrom(this.http.put(url, profile).pipe(timeout(REQUEST_TIMEOUT_MS)));
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

    try {
      const form = new FormData();
      form.append('file', file, file.name);
      await firstValueFrom(this.http.post(url, form).pipe(timeout(UPLOAD_TIMEOUT_MS)));
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

    try {
      await firstValueFrom(this.http.post(url, { currentPassword, newPassword } satisfies UpdatePasswordRequest).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'changed';
    } catch (error) {
      const failure = toRequestFailure(error);
      // Саму помилку не виводимо: у ній є тіло запиту з паролями.
      console.warn(`[UsersService] Не вдалося змінити пароль (${failure})`);
      return failure;
    }
  }
}

/** Рядок із відповіді або `null`, якщо його нема (чи він не рядок, чи порожній). */
function optionalText(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value : null;
}

/**
 * Колега з елемента списку; без `id` — `null` (такий елемент пропускається). Сервер іншої
 * версії може не віддати якесь поле або віддати не того типу: імена й пошта тоді — порожні
 * рядки, решта — `null`, ролі — порожній список. Таблиця колег на місці `null` показує свої
 * запасні значення.
 */
function toUserSummary(item: Untrusted<UserDto>): UserSummary | null {
  if (typeof item?.id !== 'string') {
    return null;
  }
  const text = (value: unknown) => (typeof value === 'string' ? value : '');
  return {
    id: item.id,
    email: text(item.email),
    firstName: text(item.firstName),
    lastName: text(item.lastName),
    patronymic: optionalText(item.patronymic),
    phoneNumber: optionalText(item.phoneNumber),
    country: optionalText(item.country),
    emergencyContact: optionalText(item.emergencyContact),
    pronouns: optionalText(item.pronouns),
    pronounCategory: typeof item.pronounCategory === 'number' ? item.pronounCategory : null,
    avatarUrl: optionalText(item.avatarUrl),
    roles: sanitizeRoles(item.roles),
  };
}
