import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import {
  CreateTableTemplateCommand,
  PagedResultOfTableTemplateDto,
  TableTemplateDto,
  TemplateFieldDto,
  UpdateTableTemplateCommand,
} from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { CUSTOM_TABLES_PAGE_SIZE } from './custom-tables.service';
import { readPagedItems, text } from './paged';
import { writeRequestOptions } from './write-options';

const API_TABLE_TEMPLATES_PATH = '/api/v1/tabletemplates';

/** Шаблон таблиці для вибору «за основу»: `id` і назва. */
export interface TableTemplateOption {
  readonly id: string;
  readonly name: string;
}

/** Поле (стовпчик) шаблону: назва, ключ і позиція. */
export interface TableTemplateField {
  readonly id: string;
  readonly name: string;
  readonly key: string;
  readonly order: number;
}

/** Шаблон таблиці зі списку: назва, опис, чи системний і поля в їхньому порядку. */
export interface TableTemplate extends TableTemplateOption {
  readonly description: string | null;
  readonly isSystemPreset: boolean;
  readonly fields: readonly TableTemplateField[];
}

/** Новий шаблон: назва, опис і назви стовпчиків з ключами в їхньому порядку. */
export interface NewTableTemplate {
  readonly name: string;
  readonly description: string | null;
  readonly fields: readonly { readonly name: string; readonly key: string }[];
}

/** Створено шаблон (його `id`) чи ні. */
export type CreateTableTemplateResult = { readonly id: string } | RequestFailure;

/** Шаблон для `PUT`: назва, опис і всі стовпчики в їхньому порядку (новий — з `id: null`). */
export interface TableTemplateUpdate {
  readonly name: string;
  readonly description: string | null;
  readonly fields: readonly { readonly id: string | null; readonly name: string; readonly key: string }[];
}

export type TableTemplateResult = 'done' | RequestFailure;

/**
 * Шаблони таблиць: список (`GET /api/v1/tabletemplates`, право `ViewTableTemplate`), створення
 * (`POST`, `CreateTableTemplate`), зміна (`PUT`, `UpdateTableTemplate`) і видалення (`DELETE`,
 * `DeleteTableTemplate`; системні шаблони сервер не дає ні змінити, ні видалити).
 */
@Injectable({ providedIn: 'root' })
export class TableTemplatesService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /** Усі шаблони за назвою (системні й створені в організації), сторінками до кінця списку. */
  async listAll(): Promise<readonly TableTemplate[] | RequestFailure> {
    const templates: TableTemplate[] = [];
    try {
      for (let page = 1; ; page++) {
        const params = new HttpParams()
          .set('OrderBy', 'Name')
          .set('SortOrder', 'asc')
          .set('PageNumber', page)
          .set('PageSize', CUSTOM_TABLES_PAGE_SIZE);
        const body = await firstValueFrom(
          this.http
            .get<Untrusted<PagedResultOfTableTemplateDto>>(`${this.connection.serverUrl()}${API_TABLE_TEMPLATES_PATH}`, { params })
            .pipe(timeout(REQUEST_TIMEOUT_MS)),
        );
        const { items, hasNextPage } = readPagedItems(body, page, toTemplate);
        templates.push(...items);
        if (!hasNextPage) {
          return templates;
        }
      }
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[TableTemplatesService] Не вдалося завантажити шаблони (${failure})`, error);
      return failure;
    }
  }

  /**
   * `POST /api/v1/tabletemplates`: шаблон зі стовпчиками `fields` у їхньому порядку. Тип і
   * обовʼязковість сервер ще вимагає — усі поля текстові й необовʼязкові (на сервері їх приберуть,
   * усе — рядки).
   */
  async create(template: NewTableTemplate): Promise<CreateTableTemplateResult> {
    const body = {
      name: template.name,
      description: template.description,
      fields: template.fields.map((field, order) => ({ name: field.name, key: field.key, dataType: 0, isRequired: false, order })),
    } satisfies CreateTableTemplateCommand;
    try {
      const id = await firstValueFrom(
        this.http.post<unknown>(`${this.connection.serverUrl()}${API_TABLE_TEMPLATES_PATH}`, body, writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      return { id: text(id) };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[TableTemplatesService] Не вдалося створити шаблон (${failure})`, error);
      return failure;
    }
  }

  /**
   * `PUT /api/v1/tabletemplates/{id}`: назва, опис і всі стовпчики (наявні — з `id`, нові — з
   * `id: null`; стовпчика, якого нема в списку, сервер видаляє). Тип і обовʼязковість — як у
   * `create`: текстові, необовʼязкові.
   */
  async update(id: string, template: TableTemplateUpdate): Promise<TableTemplateResult> {
    const body = {
      name: template.name,
      description: template.description,
      fields: template.fields.map((field, order) => ({ id: field.id, name: field.name, key: field.key, dataType: 0, isRequired: false, order })),
    } satisfies UpdateTableTemplateCommand;
    try {
      await firstValueFrom(this.http.put(this.url(id), body, writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[TableTemplatesService] Не вдалося змінити шаблон (${failure})`, error);
      return failure;
    }
  }

  /** `DELETE /api/v1/tabletemplates/{id}`. */
  async remove(id: string): Promise<TableTemplateResult> {
    try {
      await firstValueFrom(this.http.delete(this.url(id), writeRequestOptions()).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[TableTemplatesService] Не вдалося видалити шаблон (${failure})`, error);
      return failure;
    }
  }

  private url(id: string): string {
    return `${this.connection.serverUrl()}${API_TABLE_TEMPLATES_PATH}/${encodeURIComponent(id)}`;
  }
}

function toTemplate(value: Untrusted<TableTemplateDto>): TableTemplate[] {
  const id = text(value?.id);
  const name = text(value?.name);
  if (!id || !name) {
    return [];
  }
  const fields = (Array.isArray(value?.fields) ? (value.fields as Untrusted<TemplateFieldDto>[]) : [])
    .flatMap((field): TableTemplateField[] => {
      const fieldId = text(field?.id);
      const fieldName = text(field?.name);
      const key = text(field?.key);
      const order = Number(field?.order);
      return fieldId && fieldName && key && Number.isFinite(order) ? [{ id: fieldId, name: fieldName, key, order }] : [];
    })
    .sort((a, b) => a.order - b.order);
  return [{ id, name, description: text(value?.description) || null, isSystemPreset: value?.isSystemPreset === true, fields }];
}
