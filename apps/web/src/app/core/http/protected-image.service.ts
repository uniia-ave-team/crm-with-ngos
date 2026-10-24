import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';

const REQUEST_TIMEOUT_MS = 15000;
/** Скільки зображень тримаємо в памʼяті (старіші звільняються). */
const MAX_CACHED = 30;

/** Зображення сторінки входу віддаються без авторизації: їх можна показувати напряму. */
const PUBLIC_IMAGE_PREFIX = '/api/v1/loginpageimages/';

/**
 * Зображення, що лежать за авторизацією API (логотип ГО, аватар): `<img src>`
 * не може надіслати заголовок `Authorization`, тож сервер відповідає 401 і
 * зображення не показується. Цей сервіс завантажує такий файл через
 * `HttpClient` (токен додає `authInterceptor`) і віддає `blob:`-посилання, яке
 * вже можна класти в `src`.
 *
 * Посилання, яким авторизація не потрібна (`blob:`, `data:`, чужі сайти,
 * публічні зображення сторінки входу), повертаються як є. Результат кешується за
 * адресою (у ній є версія `?v=…`, тож нова версія — нова адреса).
 */
@Injectable({ providedIn: 'root' })
export class ProtectedImageService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  private readonly cache = new Map<string, Promise<string | null>>();

  /** Чи треба для цього посилання авторизація (воно веде в закритий `/api/` активного сервера). */
  needsAuth(url: string): boolean {
    const api = `${this.connection.serverUrl()}/api/`;
    return url.startsWith(api) && !url.slice(this.connection.serverUrl().length).startsWith(PUBLIC_IMAGE_PREFIX);
  }

  /**
   * Посилання, яке можна класти в `src`: саме `url`, якщо авторизація не потрібна,
   * інакше `blob:`-посилання на завантажений файл. `null`, якщо завантажити не
   * вдалося (помилку не кешуємо: наступна спроба спробує знову).
   */
  resolve(url: string): Promise<string | null> {
    if (!this.needsAuth(url)) {
      return Promise.resolve(url);
    }
    const cached = this.cache.get(url);
    if (cached) {
      return cached;
    }
    const loading = this.load(url).then((result) => {
      if (result === null) {
        this.cache.delete(url);
      }
      return result;
    });
    this.cache.set(url, loading);
    this.trim();
    return loading;
  }

  private async load(url: string): Promise<string | null> {
    try {
      const blob = await firstValueFrom(this.http.get(url, { responseType: 'blob' }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return URL.createObjectURL(blob);
    } catch (error) {
      console.warn(`[ProtectedImageService] Не вдалося завантажити зображення: ${url}`, error);
      return null;
    }
  }

  /** Тримає в кеші не більше `MAX_CACHED` зображень: найстаріші звільняються. */
  private trim(): void {
    while (this.cache.size > MAX_CACHED) {
      const oldest = this.cache.keys().next().value as string;
      const entry = this.cache.get(oldest);
      this.cache.delete(oldest);
      void entry?.then((blobUrl) => {
        if (blobUrl) {
          URL.revokeObjectURL(blobUrl);
        }
      });
    }
  }
}
