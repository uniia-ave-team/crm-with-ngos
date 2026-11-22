import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { CreateRoleCommand, PagedResultOfRoleDto, RoleDetailsDto, RoleDto, UpdateRoleCommand } from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';

const API_ROLES_PATH = '/api/v1/roles';
/** Найбільший розмір сторінки, який приймає сервер. */
const PAGE_SIZE = 100;
/** Запобіжник: стільки сторінок ролей ми не очікуємо, і якщо сервер весь час каже «є ще», зупиняємось. */
const MAX_PAGES = 50;

/** Роль у системі (`GET /api/v1/roles`). */
export interface Role {
  readonly id: string;
  readonly name: string;
  /** Назва жіночого роду; `null` — нема. */
  readonly feminitiveName: string | null;
  /** Назва у множині; `null` — нема. */
  readonly pluralName: string | null;
}

/** Дані нової ролі для `POST /api/v1/roles`: порожні необовʼязкові назви — `null`. */
export interface RoleDraft {
  readonly name: string;
  readonly feminitiveName: string | null;
  readonly pluralName: string | null;
}

/**
 * Результат створення ролі: або причина невдачі (роль не створено), або `id` створеної
 * ролі разом із правами, які не вдалося додати (`failedClaims`; порожньо — усе додано).
 */
export type CreateRoleResult =
  | RequestFailure
  | { readonly id: string; readonly failedClaims: readonly string[] };

/**
 * Результат оновлення ролі: або причина невдачі (дані ролі не збережено, права не чіпали),
 * або перелік прав, зміну яких не вдалося застосувати (`failedClaims`; порожньо — усе зроблено).
 */
export type UpdateRoleResult = RequestFailure | { readonly failedClaims: readonly string[] };

/** Роль з правами (`GET /api/v1/roles/{id}`): `claims` — рядки прав, як їх віддає сервер. */
export interface RoleDetails extends Role {
  readonly claims: readonly string[];
}

/** Ролі на сервері (`/api/v1/roles`; для читання потрібна лише авторизація). */
@Injectable({ providedIn: 'root' })
export class RolesService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /**
   * `GET <сервер>/api/v1/roles` — усі ролі: сторінками по `PAGE_SIZE`, поки сервер
   * каже, що є наступна. Елементи без `id` чи назви пропускаються. Повертає
   * список (порядок сервера) або причину невдачі; невдача на будь-якій сторінці —
   * невдача всього запиту, щоб не показувати неповний список як повний. Так само й
   * запобіжник `MAX_PAGES`: якщо сервер і після нього каже «є ще», це `error`, а не
   * зібрана частина.
   */
  async all(): Promise<readonly Role[] | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_ROLES_PATH}`;
    const roles: Role[] = [];

    for (let pageNumber = 1; pageNumber <= MAX_PAGES; pageNumber++) {
      try {
        const params = new HttpParams().set('PageNumber', pageNumber).set('PageSize', PAGE_SIZE);
        const body = await firstValueFrom(this.http.get<Untrusted<PagedResultOfRoleDto>>(url, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)));
        if (!Array.isArray(body?.items)) {
          console.warn('[RolesService] Відповідь без списку ролей', body);
          return 'error';
        }
        for (const item of body.items as Untrusted<RoleDto>[]) {
          const role = toRole(item);
          if (role) {
            roles.push(role);
          }
        }
        if (body.hasNextPage !== true) {
          return roles;
        }
      } catch (error) {
        const failure = toRequestFailure(error);
        console.warn(`[RolesService] Не вдалося отримати ролі (${failure})`, error);
        return failure;
      }
    }
    console.warn(`[RolesService] Сервер і після ${MAX_PAGES} сторінок каже, що ролі ще є: список неповний`);
    return 'error';
  }

  /**
   * Створює роль із правами: `POST /api/v1/roles` (потрібне право `ManageRole`; у відповіді `id`
   * нової ролі), а потім для кожного права окремо `POST /api/v1/roles/{id}/claims/{право}` — іншого
   * способу задати права сервер не має. Права додаються по черзі (кожне змінює ту саму роль),
   * невдачі окремого права не зупиняють решту: їх повертає `failedClaims`. Якщо роль не створено
   * — причина невдачі. Усі запити йдуть на сервер, що був поточним на початку, навіть якщо
   * тим часом перемкнули акаунт.
   */
  async create(draft: RoleDraft, claims: readonly string[]): Promise<CreateRoleResult> {
    const server = this.connection.serverUrl();
    const url = `${server}${API_ROLES_PATH}`;
    let id: string;
    try {
      const command = { name: draft.name, feminitiveName: draft.feminitiveName, pluralName: draft.pluralName } satisfies CreateRoleCommand;
      // У відповідь (201) — `id` нової ролі. Контракт (`openapi.json`) обіцяє голий рядок, а сервер
      // насправді віддає об'єкт `{ id }` (`CreatedAtRoute(..., new { Id = roleId })`): беремо обидва.
      const created = await firstValueFrom(this.http.post<unknown>(url, command).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const createdId = typeof created === 'string' ? created : (created as { id?: unknown } | null)?.id;
      if (typeof createdId !== 'string' || !createdId) {
        console.warn('[RolesService] Відповідь без id створеної ролі', created);
        return 'error';
      }
      id = createdId;
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося створити роль (${failure})`, error);
      return failure;
    }

    const failedClaims: string[] = [];
    for (const claim of claims) {
      if ((await this.addClaim(server, id, claim)) !== 'added') {
        failedClaims.push(claim);
      }
    }
    return { id, failedClaims };
  }

  /**
   * Оновлює роль: `PUT /api/v1/roles/{id}` з новими назвами (потрібне право `ManageRole`; тіло
   * `{newName, feminitiveName, pluralName}`, у відповідь 204), а потім для кожного права з `change`
   * окремий запит `POST /api/v1/roles/{id}/claims/{право}` (додати) чи `DELETE` (прибрати), по
   * черзі. Невдача окремого права не зупиняє решту: їх повертає `failedClaims`. Якщо назви не
   * збережено — причина невдачі, права не чіпаються. Усі запити йдуть на сервер, що був
   * поточним на початку.
   */
  async update(id: string, draft: RoleDraft, change: { readonly add: readonly string[]; readonly remove: readonly string[] }): Promise<UpdateRoleResult> {
    const server = this.connection.serverUrl();
    const url = `${server}${API_ROLES_PATH}/${encodeURIComponent(id)}`;
    try {
      const body = { newName: draft.name, feminitiveName: draft.feminitiveName, pluralName: draft.pluralName } satisfies UpdateRoleCommand;
      await firstValueFrom(this.http.put(url, body).pipe(timeout(REQUEST_TIMEOUT_MS)));
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося оновити роль (${failure})`, error);
      return failure;
    }

    const failedClaims: string[] = [];
    for (const claim of change.add) {
      if ((await this.addClaim(server, id, claim)) !== 'added') {
        failedClaims.push(claim);
      }
    }
    for (const claim of change.remove) {
      if ((await this.removeClaim(server, id, claim)) !== 'removed') {
        failedClaims.push(claim);
      }
    }
    return { failedClaims };
  }

  /**
   * Видаляє роль: `DELETE /api/v1/roles/{id}` (потрібне право `ManageRole`; 204 без тіла).
   * Сервер відмовляє (400 — `invalid`), якщо роль має хтось із колег або це системна роль
   * адміністратора. Повертає `deleted` або причину невдачі.
   */
  async delete(id: string): Promise<'deleted' | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_ROLES_PATH}/${encodeURIComponent(id)}`;
    try {
      await firstValueFrom(this.http.delete(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'deleted';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося видалити роль ${id} (${failure})`, error);
      return failure;
    }
  }

  /** `DELETE /api/v1/roles/{id}/claims/{право}` — прибирає одне право ролі (204 без тіла). */
  private async removeClaim(server: string, roleId: string, claim: string): Promise<'removed' | RequestFailure> {
    const url = `${server}${API_ROLES_PATH}/${encodeURIComponent(roleId)}/claims/${encodeURIComponent(claim)}`;
    try {
      await firstValueFrom(this.http.delete(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'removed';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося прибрати право ${claim} ролі ${roleId} (${failure})`, error);
      return failure;
    }
  }

  /** `POST /api/v1/roles/{id}/claims/{право}` — додає одне право ролі (204 без тіла). */
  private async addClaim(server: string, roleId: string, claim: string): Promise<'added' | RequestFailure> {
    const url = `${server}${API_ROLES_PATH}/${encodeURIComponent(roleId)}/claims/${encodeURIComponent(claim)}`;
    try {
      await firstValueFrom(this.http.post(url, null).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'added';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося додати право ${claim} ролі ${roleId} (${failure})`, error);
      return failure;
    }
  }

  /**
   * `GET <сервер>/api/v1/roles/{id}` — одна роль з її правами. Повертає роль або
   * причину невдачі (`forbidden` — 403, `error` — зокрема 404 і відповідь без
   * `id` чи назви).
   */
  async get(id: string): Promise<RoleDetails | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_ROLES_PATH}/${encodeURIComponent(id)}`;
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<RoleDetailsDto>>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const role = toRole(body);
      if (!role) {
        console.warn('[RolesService] Відповідь без id чи назви ролі', body);
        return 'error';
      }
      const claims = Array.isArray(body?.claims) ? body.claims.filter((claim): claim is string => typeof claim === 'string') : [];
      return { ...role, claims };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося отримати роль (${failure})`, error);
      return failure;
    }
  }
}

function toRole(item: Untrusted<RoleDto>): Role | null {
  const name = typeof item?.name === 'string' ? item.name.trim() : '';
  if (typeof item?.id !== 'string' || !name) {
    return null;
  }
  const optional = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : null);
  return {
    id: item.id,
    name,
    feminitiveName: optional(item.feminitiveName),
    pluralName: optional(item.pluralName),
  };
}
