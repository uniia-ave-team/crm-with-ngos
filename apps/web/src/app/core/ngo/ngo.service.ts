import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

const API_NGO_PATH = '/api/v1/ngo';
const REQUEST_TIMEOUT_MS = 10000;
/** Завантаження файлу триває довше за звичайний запит. */
const UPLOAD_TIMEOUT_MS = 60000;

export type CreateNgoResult = 'created' | RequestFailure;
export type UpdateNgoResult = 'updated' | RequestFailure;
export type UploadNgoLogoResult = 'uploaded' | RequestFailure;

/** Організація (`GET /api/v1/ngo`): лише те, що потрібно знімку акаунта. */
export interface NgoProfile {
  readonly name: string;
  /** Посилання на логотип, як його віддав сервер (може бути відносним); `null` — логотипа нема. */
  readonly logoUrl: string | null;
}

@Injectable({ providedIn: 'root' })
export class NgoService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);

  /**
   * Створює організацію (`POST <сервер>/api/v1/ngo`). Токен авторизації
   * додає й за потреби оновлює `authInterceptor`. Логотип поки не передаємо —
   * `logoUrl` завжди `null`.
   */
  async create(name: string): Promise<CreateNgoResult> {
    const url = `${this.connection.serverUrl()}${API_NGO_PATH}`;
    console.log(`[NgoService] Створюю організацію: ${url}`);

    try {
      await firstValueFrom(this.http.post(url, { name, logoUrl: null }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[NgoService] Організацію створено');
      // Назва й логотип ГО — частина знімка активного акаунта (плитка у вікні
      // акаунта). Логотип поки не передаємо, тож він `null`.
      const id = this.accounts.activeId();
      if (id) {
        this.accounts.update(id, { ngoName: name, ngoLogoUrl: null });
      }
      return 'created';
    } catch (error) {
      const result = toRequestFailure(error);
      console.warn(`[NgoService] Не вдалося створити організацію (${result})`, error);
      return result;
    }
  }

  /**
   * Завантажує логотип організації (`POST <сервер>/api/v1/ngo/logo`,
   * `multipart/form-data`, поле `file`; потрібне право `UpdateNgo`; 200 без
   * тіла). Саме зображення, а не посилання: посилання на нього призначає
   * сервер, тож після успіху організація читається заново (`GET /api/v1/ngo`) і
   * нове посилання потрапляє в знімок активного акаунта.
   */
  async uploadLogo(file: File): Promise<UploadNgoLogoResult> {
    const url = `${this.connection.serverUrl()}${API_NGO_PATH}/logo`;
    const accountId = this.accounts.activeId();
    console.log(`[NgoService] Завантажую логотип організації: ${url}`);

    try {
      const form = new FormData();
      form.append('file', file, file.name);
      await firstValueFrom(this.http.post(url, form).pipe(timeout(UPLOAD_TIMEOUT_MS)));
      console.log('[NgoService] Логотип завантажено');
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[NgoService] Не вдалося завантажити логотип (${failure})`, error);
      return failure;
    }

    // Посилання на логотип призначив сервер: читаємо його й оновлюємо знімок.
    const ngo = await this.fetch();
    if (ngo && accountId) {
      this.accounts.update(accountId, { ngoName: ngo.name, ngoLogoUrl: ngo.logoUrl });
    }
    return 'uploaded';
  }

  /**
   * Оновлює організацію (`PUT <сервер>/api/v1/ngo`, потрібне право
   * `Permissions.UpdateNgo`; 204 без тіла). Обидва поля обовʼязкові в запиті:
   * `logoUrl` — посилання на логотип або `null`. Токен додає `authInterceptor`.
   * Увага (так працює сервер): `null` **скидає** логотип організації, а не-null
   * значення записується як є й видаляє наявний файл логотипа; тож змінити
   * лише назву, не чіпаючи логотип, цим запитом не можна, а новий логотип
   * надсилається окремим `uploadLogo`.
   * Успіх одразу потрапляє в знімок організації в записі акаунта (`localStorage`):
   * в той акаунт, від імені якого йшов запит, навіть якщо за час запиту активним
   * став інший.
   */
  async update(name: string, logoUrl: string | null): Promise<UpdateNgoResult> {
    const url = `${this.connection.serverUrl()}${API_NGO_PATH}`;
    const accountId = this.accounts.activeId();
    console.log(`[NgoService] Оновлюю організацію: ${url}`);

    try {
      await firstValueFrom(this.http.put(url, { name, logoUrl }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[NgoService] Організацію оновлено');
      if (accountId) {
        this.accounts.update(accountId, { ngoName: name, ngoLogoUrl: logoUrl });
      }
      return 'updated';
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[NgoService] Не вдалося оновити організацію (${failure})`, error);
      return failure;
    }
  }

  /**
   * Організація (`GET <сервер>/api/v1/ngo`): назва й посилання на логотип.
   * `null`, якщо її нема або користувач не має права на перегляд (403) чи запит
   * не вдався — це лише прикраса плитки акаунта, тож не помилка.
   */
  async fetch(): Promise<NgoProfile | null> {
    const url = `${this.connection.serverUrl()}${API_NGO_PATH}`;
    try {
      const ngo = await firstValueFrom(
        this.http.get<{ name?: string; logoUrl?: string | null }>(url).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const name = ngo?.name?.trim();
      if (!name) {
        return null;
      }
      return { name, logoUrl: ngo.logoUrl?.trim() || null };
    } catch {
      return null;
    }
  }
}
