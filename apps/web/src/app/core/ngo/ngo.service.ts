import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

const API_NGO_PATH = '/api/v1/ngo';
const REQUEST_TIMEOUT_MS = 10000;

export type CreateNgoResult = 'created' | RequestFailure;

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
