import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

const API_NGO_PATH = '/api/v1/ngo';
const REQUEST_TIMEOUT_MS = 10000;

export type CreateNgoResult = 'created' | RequestFailure;

@Injectable({ providedIn: 'root' })
export class NgoService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

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
      return 'created';
    } catch (error) {
      const result = toRequestFailure(error);
      console.warn(`[NgoService] Не вдалося створити організацію (${result})`, error);
      return result;
    }
  }
}
