import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import {
  CreateCustomFieldDto,
  CreateCustomTableCommand,
  CreateCustomTableFromTemplateCommand,
  CustomFieldDto,
  CustomTableDto,
  CustomTableSummaryDto,
  PagedResultOfCustomTableSummaryDto,
  UpdateCustomFieldDto,
  UpdateCustomTableCommand,
} from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { readPagedItems, text } from './paged';
import { Gone, isGone } from './gone';
import { writeRequestOptions } from './write-options';

const API_CUSTOM_TABLES_PATH = '/api/v1/customtables';

/** Найбільша сторінка списку, яку приймає сервер (`PaginationConstants.MaxPageSize`). */
export const CUSTOM_TABLES_PAGE_SIZE = 100;
/** Найдовші назва й опис таблиці, які приймає сервер (`CustomTableValidationConstants`). */
export const CUSTOM_TABLE_NAME_MAX_LENGTH = 255;
export const CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH = 1000;
/** Найдовша назва поля (`FieldSchemaValidationConstants.MaxNameLength`). */
export const CUSTOM_FIELD_NAME_MAX_LENGTH = 255;
/** Найдовше значення клітинки (`CustomRecordDataValidationExtensions.MaxStringValueLength`). */
export const CUSTOM_RECORD_VALUE_MAX_LENGTH = 10_000;
/** Найбільше полів у таблиці (`CustomRecordValidationConstants.MaxFields`). */
export const CUSTOM_TABLE_MAX_FIELDS = 50;

/** Таблиця зі списку `GET /api/v1/customtables`: те, що потрібно змісту бази знань. */
export interface CustomTableSummary {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
}

/** Поле (стовпчик) таблиці в тому вигляді, у якому його повертають серверу в `PUT` без змін. */
export type CustomTableField = UpdateCustomFieldDto & { readonly id: string };

/** Таблиця з `GET /api/v1/customtables/{id}`: назва, опис і структура (поля). */
export interface CustomTableDetails {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly fields: readonly CustomTableField[];
}

/** Таблиця для `PUT`: назва, опис і всі поля (нове поле — з `id: null`). */
export interface CustomTableUpdate {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly fields: readonly UpdateCustomFieldDto[];
}

/** Нова таблиця без шаблону: назва, опис і поля (сервер вимагає хоча б одне). */
export interface NewCustomTable {
  readonly name: string;
  readonly description: string | null;
  readonly fields: readonly CreateCustomFieldDto[];
}

/** Нова таблиця за шаблоном. */
export interface NewCustomTableFromTemplate {
  readonly templateId: string;
  readonly name: string;
  readonly description: string | null;
}

/** Створено таблицю (її `id`) чи ні. */
export type CreateCustomTableResult = { readonly id: string } | RequestFailure;
export type CustomTableResult = 'done' | RequestFailure;
/** Таблицю змінено; `gone` — її вже видалили (404). */
export type UpdateCustomTableResult = 'done' | Gone | RequestFailure;

/**
 * Запити до таблиць організації (`/api/v1/customtables`, права `ViewCustomTable`,
 * `CreateCustomTable`, `UpdateCustomTable`, `DeleteCustomTable`). Відповіді перевіряються
 * поелементно: на сервер іншої версії не покладаємося.
 */
@Injectable({ providedIn: 'root' })
export class CustomTablesService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  private url(id?: string): string {
    return `${this.connection.serverUrl()}${API_CUSTOM_TABLES_PATH}${id ? `/${encodeURIComponent(id)}` : ''}`;
  }

  /**
   * Усі таблиці організації за назвою: `GET /api/v1/customtables` сторінками по
   * `CUSTOM_TABLES_PAGE_SIZE`, доки сервер каже, що є наступна.
   */
  async listAll(): Promise<readonly CustomTableSummary[] | RequestFailure> {
    const tables: CustomTableSummary[] = [];
    try {
      for (let page = 1; ; page++) {
        const params = new HttpParams()
          .set('OrderBy', 'Name')
          .set('SortOrder', 'asc')
          .set('PageNumber', page)
          .set('PageSize', CUSTOM_TABLES_PAGE_SIZE);
        const body = await firstValueFrom(
          this.http.get<Untrusted<PagedResultOfCustomTableSummaryDto>>(this.url(), { params }).pipe(timeout(REQUEST_TIMEOUT_MS)),
        );
        const { items, hasNextPage } = readPagedItems(body, page, toSummary);
        tables.push(...items);
        if (!hasNextPage) {
          return tables;
        }
      }
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomTablesService] Не вдалося завантажити таблиці (${failure})`, error);
      return failure;
    }
  }

  /** `GET /api/v1/customtables/{id}`: назва, опис і поля. Відповідь, з якої не скласти поля, — `error`. */
  async get(id: string): Promise<CustomTableDetails | Gone | RequestFailure> {
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<CustomTableDto>>(this.url(id)).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const details = toDetails(body);
      if (!details) {
        console.warn('[CustomTablesService] Сервер віддав таблицю в невідомому вигляді', body);
        return 'error';
      }
      return details;
    } catch (error) {
      if (isGone(error)) {
        return 'gone';
      }
      const failure = toRequestFailure(error);
      console.warn(`[CustomTablesService] Не вдалося завантажити таблицю (${failure})`, error);
      return failure;
    }
  }

  /** `POST /api/v1/customtables`: таблиця без шаблону. */
  create(table: NewCustomTable): Promise<CreateCustomTableResult> {
    const body = { name: table.name, description: table.description, fields: [...table.fields] } satisfies CreateCustomTableCommand;
    return this.post(this.url(), body);
  }

  /** `POST /api/v1/customtables/from-template`: таблиця зі структурою шаблону. */
  createFromTemplate(table: NewCustomTableFromTemplate): Promise<CreateCustomTableResult> {
    const body = {
      sourceTemplateId: table.templateId,
      name: table.name,
      description: table.description,
    } satisfies CreateCustomTableFromTemplateCommand;
    return this.post(`${this.url()}/from-template`, body);
  }

  /**
   * `PUT /api/v1/customtables/{id}`: сервер приймає назву й опис лише разом з усією структурою —
   * усі поля в їхньому порядку (наявні — з `id`, нові — з `id: null`; поля, якого нема в списку,
   * сервер видаляє).
   */
  async update(table: CustomTableUpdate): Promise<UpdateCustomTableResult> {
    const body = { name: table.name, description: table.description, fields: [...table.fields] } satisfies UpdateCustomTableCommand;
    try {
      await firstValueFrom(this.http.put(this.url(table.id), body, writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      if (isGone(error)) {
        return 'gone';
      }
      const failure = toRequestFailure(error);
      console.warn(`[CustomTablesService] Не вдалося змінити таблицю (${failure})`, error);
      return failure;
    }
  }

  /** `DELETE /api/v1/customtables/{id}`. */
  async remove(id: string): Promise<CustomTableResult> {
    try {
      await firstValueFrom(this.http.delete(this.url(id)).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomTablesService] Не вдалося видалити таблицю (${failure})`, error);
      return failure;
    }
  }

  private async post(url: string, body: object): Promise<CreateCustomTableResult> {
    try {
      const id = await firstValueFrom(this.http.post<unknown>(url, body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return { id: text(id) };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[CustomTablesService] Не вдалося створити таблицю (${failure})`, error);
      return failure;
    }
  }
}

function toSummary(value: Untrusted<CustomTableSummaryDto>): CustomTableSummary[] {
  const id = text(value?.id);
  const name = text(value?.name);
  return id && name ? [{ id, name, description: text(value?.description) || null }] : [];
}

function toDetails(value: Untrusted<CustomTableDto>): CustomTableDetails | null {
  const id = text(value?.id);
  const name = text(value?.name);
  if (!id || !name || !Array.isArray(value?.fields)) {
    return null;
  }
  const fields = (value.fields as Untrusted<CustomFieldDto>[]).map(toField);
  if (fields.some((field) => field === null)) {
    return null;
  }
  return { id, name, description: text(value?.description) || null, fields: fields as CustomTableField[] };
}

/** Поле таблиці, як його треба віддати в `PUT`; щось не того типу — `null` (структуру не відправляємо). */
function toField(value: Untrusted<CustomFieldDto>): CustomTableField | null {
  const { id, name, key, dataType, isRequired, order } = value ?? {};
  if (
    typeof id !== 'string' ||
    typeof name !== 'string' ||
    typeof key !== 'string' ||
    typeof dataType !== 'number' ||
    typeof isRequired !== 'boolean' ||
    typeof order !== 'number'
  ) {
    return null;
  }
  return { id, name, key, dataType, isRequired, order };
}
