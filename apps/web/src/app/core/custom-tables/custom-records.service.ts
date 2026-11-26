import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { CreateCustomRecordCommand, CustomRecordDto, PagedResultOfCustomRecordDto, UpdateCustomRecordCommand } from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { CUSTOM_TABLES_PAGE_SIZE } from './custom-tables.service';
import { readPagedItems, text } from './paged';
import { writeRequestOptions } from './write-options';

const API_CUSTOM_RECORDS_PATH = '/api/v1/customrecords';

/** Запис (рядок) таблиці: `id` і значення полів за їхніми ключами (`key` поля). */
export interface CustomRecord {
  readonly id: string;
  readonly data: CustomRecordData;
}

/** Порядок записів: ключ поля (`OrderBy`; сервер сортує за значенням цього поля) і напрям. */
export interface CustomRecordsSort {
  readonly orderBy: string;
  readonly sortOrder: 'asc' | 'desc';
}

/** Дані запису: значення полів за ключами (`null` — порожньо). */
export type CustomRecordData = Readonly<Record<string, unknown>>;

/** Створено запис (його `id`) чи ні. */
export type CreateCustomRecordResult = { readonly id: string } | RequestFailure;

/**
 * Записи таблиць (`/api/v1/customrecords`): перегляд (`ViewCustomRecord`), створення
 * (`CreateCustomRecord`), зміна (`UpdateCustomRecord`) і видалення (`DeleteCustomRecord`).
 */
@Injectable({ providedIn: 'root' })
export class CustomRecordsService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /**
   * Усі записи таблиці `tableId`: `GET /api/v1/customrecords?CustomTableId=…` сторінками по
   * `CUSTOM_TABLES_PAGE_SIZE`, доки сервер каже, що є наступна; `sort` — порядок (без нього — серверний),
   * `searchTerm` — пошук у значеннях записів (`SearchTerm`; `null` — без пошуку).
   */
  async listAll(tableId: string, sort: CustomRecordsSort | null = null, searchTerm: string | null = null): Promise<readonly CustomRecord[] | RequestFailure> {
    const records: CustomRecord[] = [];
    try {
      for (let page = 1; ; page++) {
        let params = new HttpParams().set('CustomTableId', tableId).set('PageNumber', page).set('PageSize', CUSTOM_TABLES_PAGE_SIZE);
        if (sort) {
          params = params.set('OrderBy', sort.orderBy).set('SortOrder', sort.sortOrder);
        }
        if (searchTerm) {
          params = params.set('SearchTerm', searchTerm);
        }
        const body = await firstValueFrom(
          this.http
            .get<Untrusted<PagedResultOfCustomRecordDto>>(this.url(), { params })
            .pipe(timeout(REQUEST_TIMEOUT_MS)),
        );
        const { items, hasNextPage } = readPagedItems(body, page, toRecord);
        records.push(...items);
        if (!hasNextPage) {
          return records;
        }
      }
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomRecordsService] Не вдалося завантажити записи (${failure})`, error);
      return failure;
    }
  }

  /** `POST /api/v1/customrecords`: новий запис таблиці `tableId` з даними `data` (усі поля). */
  async create(tableId: string, data: CustomRecordData): Promise<CreateCustomRecordResult> {
    const body = { customTableId: tableId, data: { ...data } } satisfies CreateCustomRecordCommand;
    try {
      const id = await firstValueFrom(this.http.post<unknown>(this.url(), body, writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return { id: text(id) };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomRecordsService] Не вдалося створити запис (${failure})`, error);
      return failure;
    }
  }

  /** `PUT /api/v1/customrecords/{id}`: сервер замінює дані запису цілком, тож передаються всі поля. */
  async update(id: string, data: CustomRecordData): Promise<'done' | RequestFailure> {
    const body = { data: { ...data } } satisfies UpdateCustomRecordCommand;
    try {
      await firstValueFrom(this.http.put(`${this.url()}/${encodeURIComponent(id)}`, body, writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomRecordsService] Не вдалося змінити запис (${failure})`, error);
      return failure;
    }
  }

  /** `DELETE /api/v1/customrecords/{id}` (право `DeleteCustomRecord`). */
  async remove(id: string): Promise<'done' | RequestFailure> {
    try {
      await firstValueFrom(this.http.delete(`${this.url()}/${encodeURIComponent(id)}`, writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomRecordsService] Не вдалося видалити запис (${failure})`, error);
      return failure;
    }
  }

  private url(): string {
    return `${this.connection.serverUrl()}${API_CUSTOM_RECORDS_PATH}`;
  }
}

function toRecord(value: Untrusted<CustomRecordDto>): CustomRecord[] {
  const id = text(value?.id);
  const data = value?.data;
  return id && data && typeof data === 'object' && !Array.isArray(data) ? [{ id, data: data as Record<string, unknown> }] : [];
}
