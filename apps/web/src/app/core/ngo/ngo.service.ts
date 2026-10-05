import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';

const API_NGO_PATH = '/api/v1/ngo';
const REQUEST_TIMEOUT_MS = 10000;

/**
 * Чим закінчилося створення організації:
 * - `created` — 201;
 * - `invalid` — 400, сервер не прийняв дані;
 * - `unauthorized` / `forbidden` — 401 / 403 (потрібна авторизація / немає
 *   права `CreateNgo`);
 * - `unreachable` — відповіді немає (мережа, таймаут, CORS);
 * - `error` — будь-яка інша помилка сервера.
 */
export type CreateNgoResult = 'created' | 'invalid' | 'unauthorized' | 'forbidden' | 'unreachable' | 'error';

@Injectable({ providedIn: 'root' })
export class NgoService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /**
   * Створює організацію (`POST <сервер>/api/v1/ngo`). Логотип поки не
   * передаємо — `logoUrl` завжди `null`.
   */
  async create(name: string): Promise<CreateNgoResult> {
    const url = `${this.connection.serverUrl()}${API_NGO_PATH}`;
    console.log(`[NgoService] Створюю організацію: ${url}`);

    try {
      await firstValueFrom(this.http.post(url, { name, logoUrl: null }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[NgoService] Організацію створено');
      return 'created';
    } catch (error) {
      const result = toResult(error);
      console.warn(`[NgoService] Не вдалося створити організацію (${result})`, error);
      return result;
    }
  }
}

function toResult(error: unknown): CreateNgoResult {
  if (!(error instanceof HttpErrorResponse) || error.status === 0) {
    return 'unreachable';
  }
  switch (error.status) {
    case 400:
      return 'invalid';
    case 401:
      return 'unauthorized';
    case 403:
      return 'forbidden';
    default:
      return 'error';
  }
}
