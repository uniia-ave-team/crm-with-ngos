import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

const API_ROLES_PATH = '/api/v1/roles';
const REQUEST_TIMEOUT_MS = 10000;
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

/** Роль з правами (`GET /api/v1/roles/{id}`): `claims` — рядки прав, як їх віддає сервер. */
export interface RoleDetails extends Role {
  readonly claims: readonly string[];
}

interface RolesResponse {
  readonly items?: unknown;
  readonly hasNextPage?: boolean;
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
   * невдача всього запиту, щоб не показувати неповний список як повний.
   */
  async all(): Promise<readonly Role[] | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_ROLES_PATH}`;
    const roles: Role[] = [];

    for (let pageNumber = 1; pageNumber <= MAX_PAGES; pageNumber++) {
      console.log(`[RolesService] Запитую ролі (сторінка ${pageNumber}): ${url}`);
      try {
        const params = new HttpParams().set('PageNumber', pageNumber).set('PageSize', PAGE_SIZE);
        const body = await firstValueFrom(this.http.get<RolesResponse>(url, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)));
        if (!Array.isArray(body?.items)) {
          console.warn('[RolesService] Відповідь без списку ролей', body);
          return 'error';
        }
        for (const item of body.items as Record<string, unknown>[]) {
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
    return roles;
  }

  /**
   * `GET <сервер>/api/v1/roles/{id}` — одна роль з її правами. Повертає роль або
   * причину невдачі (`forbidden` — 403, `error` — зокрема 404 і відповідь без
   * `id` чи назви).
   */
  async get(id: string): Promise<RoleDetails | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_ROLES_PATH}/${encodeURIComponent(id)}`;
    console.log(`[RolesService] Запитую роль: ${url}`);
    try {
      const body = await firstValueFrom(this.http.get<Record<string, unknown>>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const role = toRole(body);
      if (!role) {
        console.warn('[RolesService] Відповідь без id чи назви ролі', body);
        return 'error';
      }
      const claims = Array.isArray(body['claims']) ? body['claims'].filter((claim): claim is string => typeof claim === 'string') : [];
      return { ...role, claims };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[RolesService] Не вдалося отримати роль (${failure})`, error);
      return failure;
    }
  }
}

function toRole(item: Record<string, unknown> | null | undefined): Role | null {
  const name = typeof item?.['name'] === 'string' ? item['name'].trim() : '';
  if (typeof item?.['id'] !== 'string' || !name) {
    return null;
  }
  const optional = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : null);
  return {
    id: item['id'],
    name,
    feminitiveName: optional(item['feminitiveName']),
    pluralName: optional(item['pluralName']),
  };
}
