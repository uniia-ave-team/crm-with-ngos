import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { LoginPageImageDto, PagedResultOfLoginPageImageDto } from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, UploadFailure, toRequestFailure, toUploadFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS, UPLOAD_TIMEOUT_MS } from '../http/timeouts';

/** Зображення збоку форм входу, поки з сервера нічого не отримано (або його нема). */
export const DEFAULT_LOGIN_IMAGE = 'assets/login_image.jpg';

const API_LOGIN_PAGE_IMAGES_PATH = '/api/v1/loginpageimages';

/** Зображення сторінки входу: `id` на сервері й посилання на файл (може бути відносним). */
export interface LoginPageImage {
  readonly id: string;
  readonly url: string;
}

export type DeleteImageResult = 'deleted' | RequestFailure;
export type UploadImageResult = 'uploaded' | UploadFailure;

/** Одна сторінка зображень: список і чи є за нею наступна. */
export interface LoginPageImagesPage {
  readonly images: LoginPageImage[];
  readonly hasNextPage: boolean;
}

/**
 * Зображення сторінки входу на сервері (`/api/v1/loginpageimages`, права
 * `ViewLoginPageImages` та інші окремо для кожної дії).
 */
@Injectable({ providedIn: 'root' })
export class LoginPageImagesService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  /**
   * `GET <сервер>/api/v1/loginpageimages?PageNumber=…&PageSize=…` — одна
   * сторінка зображень (зображень може бути дуже багато, тому по одній, коли
   * треба). Відносні посилання перераховуються від адреси сервера, якого стосувався запит;
   * елементи без `id` чи `url` пропускаються. Повертає сторінку або причину невдачі.
   */
  async page(pageNumber: number, pageSize: number): Promise<LoginPageImagesPage | RequestFailure> {
    const server = this.connection.serverUrl();
    const url = `${server}${API_LOGIN_PAGE_IMAGES_PATH}`;

    try {
      const params = new HttpParams().set('PageNumber', pageNumber).set('PageSize', pageSize);
      const body = await firstValueFrom(
        this.http.get<Untrusted<PagedResultOfLoginPageImageDto>>(url, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      if (!Array.isArray(body?.items)) {
        console.warn('[LoginPageImagesService] Відповідь без списку зображень', body);
        return 'error';
      }
      const images: LoginPageImage[] = [];
      for (const item of body.items as Untrusted<LoginPageImageDto>[]) {
        if (typeof item?.id === 'string' && typeof item.url === 'string' && item.url.trim()) {
          images.push({ id: item.id, url: absolute(item.url.trim(), server) });
        }
      }
      return { images, hasNextPage: body.hasNextPage === true };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[LoginPageImagesService] Не вдалося отримати зображення (${failure})`, error);
      return failure;
    }
  }

  /**
   * `GET <сервер>/api/v1/loginpageimages/random` — випадкове зображення для форми
   * входу (запит публічний, токен не потрібен). `null`, якщо зображень нема
   * (`404`) або отримати його не вдалося: форма входу тоді лишає своє
   * зображення за замовчуванням, тож це не помилка для користувача. `server` — origin
   * сервера (за замовчуванням поточний): інший потрібен, коли форма входу показується для
   * сервера, що ще не став поточним.
   */
  async random(server: string = this.connection.serverUrl()): Promise<LoginPageImage | null> {
    const url = `${server}${API_LOGIN_PAGE_IMAGES_PATH}/random`;

    try {
      const body = await firstValueFrom(
        this.http.get<Untrusted<LoginPageImageDto>>(url).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const link = typeof body?.url === 'string' ? body.url.trim() : '';
      if (typeof body?.id !== 'string' || !link) {
        console.warn('[LoginPageImagesService] Відповідь без зображення', body);
        return null;
      }
      return { id: body.id, url: absolute(link, server) };
    } catch (error) {
      // 404 — зображень для форми входу просто нема, це не помилка.
      if (!(error instanceof HttpErrorResponse && error.status === 404)) {
        console.warn('[LoginPageImagesService] Не вдалося отримати випадкове зображення', error);
      }
      return null;
    }
  }

  /**
   * `DELETE <сервер>/api/v1/loginpageimages/{id}` — видалення зображення
   * (потрібне право `DeleteLoginPageImages`; 204 без тіла). Відповідь `404`
   * (його вже немає) теж вважається видаленим: цього ми й хотіли.
   */
  async delete(id: string): Promise<DeleteImageResult> {
    const url = `${this.connection.serverUrl()}${API_LOGIN_PAGE_IMAGES_PATH}/${encodeURIComponent(id)}`;

    try {
      await firstValueFrom(this.http.delete(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'deleted';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return 'deleted';
      }
      const failure = toRequestFailure(error);
      console.warn(`[LoginPageImagesService] Не вдалося видалити зображення (${failure})`, error);
      return failure;
    }
  }

  /**
   * `POST <сервер>/api/v1/loginpageimages` (`multipart/form-data`, поле `file`) —
   * завантаження нового зображення (потрібне право `CreateLoginPageImages`; 201 з
   * `id` у тілі). `id` не потрібен: галерея після збереження однаково перечитує список.
   * Тому й відповідь не за контрактом — теж успіх: файл уже на сервері, і повторна спроба
   * його задублювала б.
   */
  async upload(file: File): Promise<UploadImageResult> {
    const url = `${this.connection.serverUrl()}${API_LOGIN_PAGE_IMAGES_PATH}`;

    try {
      const form = new FormData();
      form.append('file', file, file.name);
      await firstValueFrom(this.http.post(url, form).pipe(timeout(UPLOAD_TIMEOUT_MS)));
      return 'uploaded';
    } catch (error) {
      const failure = toUploadFailure(error);
      console.warn(`[LoginPageImagesService] Не вдалося завантажити зображення (${failure})`, error);
      return failure;
    }
  }

}

/** Відносне посилання — від адреси сервера `server`; некоректне лишається як є. */
function absolute(url: string, server: string): string {
  try {
    return new URL(url, `${server}/`).toString();
  } catch {
    return url;
  }
}
