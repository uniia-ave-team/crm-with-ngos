import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { RequestFailure, toRequestFailure } from '../http/request-failure';

const API_LOGIN_PAGE_IMAGES_PATH = '/api/v1/loginpageimages';
const REQUEST_TIMEOUT_MS = 10000;
/** Завантаження файлу триває довше за звичайний запит. */
const UPLOAD_TIMEOUT_MS = 60000;

/** Зображення сторінки входу: `id` на сервері й посилання на файл (може бути відносним). */
export interface LoginPageImage {
  readonly id: string;
  readonly url: string;
}

export type DeleteImageResult = 'deleted' | RequestFailure;
export type UploadImageResult = { readonly id: string } | RequestFailure;

/** Одна сторінка зображень: список і чи є за нею наступна. */
export interface LoginPageImagesPage {
  readonly images: LoginPageImage[];
  readonly hasNextPage: boolean;
}

interface LoginPageImagesResponse {
  readonly items?: unknown;
  readonly hasNextPage?: boolean;
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
   * треба). Відносні посилання перераховуються від адреси сервера; елементи без
   * `id` чи `url` пропускаються. Повертає сторінку або причину невдачі.
   */
  async page(pageNumber: number, pageSize: number): Promise<LoginPageImagesPage | RequestFailure> {
    const url = `${this.connection.serverUrl()}${API_LOGIN_PAGE_IMAGES_PATH}`;
    console.log(`[LoginPageImagesService] Запитую зображення сторінки входу (сторінка ${pageNumber}): ${url}`);

    try {
      const params = new HttpParams().set('PageNumber', pageNumber).set('PageSize', pageSize);
      const body = await firstValueFrom(
        this.http.get<LoginPageImagesResponse>(url, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      if (!Array.isArray(body?.items)) {
        console.warn('[LoginPageImagesService] Відповідь без списку зображень', body);
        return 'error';
      }
      const images: LoginPageImage[] = [];
      for (const item of body.items as { id?: unknown; url?: unknown }[]) {
        if (typeof item?.id === 'string' && typeof item.url === 'string' && item.url.trim()) {
          images.push({ id: item.id, url: this.absolute(item.url.trim()) });
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
   * зображення за замовчуванням, тож це не помилка для користувача.
   */
  async random(): Promise<LoginPageImage | null> {
    const url = `${this.connection.serverUrl()}${API_LOGIN_PAGE_IMAGES_PATH}/random`;
    console.log(`[LoginPageImagesService] Запитую випадкове зображення для форми входу: ${url}`);

    try {
      const body = await firstValueFrom(
        this.http.get<{ id?: unknown; url?: unknown }>(url).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const link = typeof body?.url === 'string' ? body.url.trim() : '';
      if (typeof body?.id !== 'string' || !link) {
        console.warn('[LoginPageImagesService] Відповідь без зображення', body);
        return null;
      }
      return { id: body.id, url: this.absolute(link) };
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        console.log('[LoginPageImagesService] Зображень для форми входу нема');
      } else {
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
    console.log(`[LoginPageImagesService] Видаляю зображення сторінки входу: ${url}`);

    try {
      await firstValueFrom(this.http.delete(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      console.log('[LoginPageImagesService] Зображення видалено');
      return 'deleted';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        console.warn('[LoginPageImagesService] Зображення вже немає на сервері');
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
   * `id` у тілі). Повертає `{ id }` або причину невдачі.
   */
  async upload(file: File): Promise<UploadImageResult> {
    const url = `${this.connection.serverUrl()}${API_LOGIN_PAGE_IMAGES_PATH}`;
    console.log(`[LoginPageImagesService] Завантажую зображення сторінки входу: ${url}`);

    try {
      const form = new FormData();
      form.append('file', file, file.name);
      const body = await firstValueFrom(this.http.post<unknown>(url, form).pipe(timeout(UPLOAD_TIMEOUT_MS)));
      console.log('[LoginPageImagesService] Зображення завантажено');
      return { id: typeof body === 'string' ? body : '' };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[LoginPageImagesService] Не вдалося завантажити зображення (${failure})`, error);
      return failure;
    }
  }

  /** Відносне посилання — від адреси сервера; некоректне лишається як є. */
  private absolute(url: string): string {
    try {
      return new URL(url, `${this.connection.serverUrl()}/`).toString();
    } catch {
      return url;
    }
  }
}
