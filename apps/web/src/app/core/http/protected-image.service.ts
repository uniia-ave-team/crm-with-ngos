import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, untracked } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';

const REQUEST_TIMEOUT_MS = 15000;
/** Скільки зображень тримаємо в памʼяті (старіші й не використані звільняються). */
const MAX_CACHED = 30;

/** Зображення сторінки входу віддаються без авторизації: їх можна показувати напряму. */
const PUBLIC_IMAGE_PREFIX = '/api/v1/loginpageimages/';

/**
 * Користування зображенням: поки воно не віддане (`release`), сервіс не звільнить
 * його `blob:`-посилання, навіть коли кеш переповнений чи очищений.
 */
export interface ImageLease {
  /** `blob:`-посилання (або сама адреса, якщо авторизація не потрібна); `null`, якщо не вдалося. */
  readonly src: Promise<string | null>;
  /** Віддає зображення (можна викликати кілька разів: діє лише перший). */
  release(): void;
}

/** Запис кешу: завантаження, `blob:`-посилання й скільки користувачів його зараз показують. */
interface CacheEntry {
  blobUrl: string | null;
  users: number;
  /** Запис прибрано з кешу (переповнення чи зміна сесії): його посилання звільниться, коли ним ніхто не користується. */
  detached: boolean;
  loading: Promise<string | null>;
}

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
 *
 * Два правила, що стосуються кеша:
 * - **Кеш належить сесії.** Закриті зображення не мають переходити від одного
 *   користувача до іншого (інакше сервер не відмовить, бо запиту не буде), тож при
 *   зміні активного акаунта (вихід, вхід, перемикання) кеш очищується.
 * - **Показане не звільняється.** Кожне користування береться (`acquire`) і
 *   віддається (`release`); при переповненні (понад `MAX_CACHED`) звільняються
 *   лише ті, що не використовуються, починаючи з давно не потрібних. Те, що ще
 *   на екрані, звільниться, коли його віддадуть.
 */
@Injectable({ providedIn: 'root' })
export class ProtectedImageService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);

  /** Порядок у `Map` = порядок використання: найдавніше використані спочатку. */
  private readonly cache = new Map<string, CacheEntry>();
  /** Активний акаунт, для якого цей кеш (`undefined` — ще не запам'ятали). */
  private session: string | null | undefined;

  constructor() {
    // Кеш прибираємо одразу після зміни акаунта, навіть якщо зображення більше ніхто не просить.
    effect(() => {
      const active = this.accounts.activeId();
      untracked(() => this.checkSession(active));
    });
  }

  /** Чи треба для цього посилання авторизація (воно веде в закритий `/api/` активного сервера). */
  needsAuth(url: string): boolean {
    const api = `${this.connection.serverUrl()}/api/`;
    return url.startsWith(api) && !url.slice(this.connection.serverUrl().length).startsWith(PUBLIC_IMAGE_PREFIX);
  }

  /**
   * Бере зображення в користування: `src` — посилання, яке можна класти в `src`
   * (саме `url`, якщо авторизація не потрібна, інакше `blob:`-посилання на
   * завантажений файл), або `null`, якщо завантажити не вдалося (помилку не
   * кешуємо: наступна спроба спробує знову). Коли зображення вже не показується,
   * треба викликати `release()`.
   */
  acquire(url: string): ImageLease {
    if (!this.needsAuth(url)) {
      return { src: Promise.resolve(url), release: () => undefined };
    }
    this.checkSession(this.accounts.activeId());

    let entry = this.cache.get(url);
    if (entry) {
      // Використали — переносимо в кінець черги на звільнення.
      this.cache.delete(url);
      this.cache.set(url, entry);
    } else {
      entry = this.createEntry(url);
      this.cache.set(url, entry);
    }
    entry.users++;
    this.trim();

    const leased = entry;
    let released = false;
    return {
      src: leased.loading,
      release: () => {
        if (released) {
          return;
        }
        released = true;
        leased.users--;
        this.freeIfUnused(leased);
      },
    };
  }

  /**
   * Очищує кеш: незайняті зображення звільняються одразу, ті, що ще показуються,
   * — коли їх віддадуть. Запити, що ще тривають, після завершення дадуть `null`.
   */
  clear(): void {
    for (const entry of this.cache.values()) {
      entry.detached = true;
      this.freeIfUnused(entry);
    }
    this.cache.clear();
  }

  private checkSession(active: string | null): void {
    if (this.session !== undefined && this.session !== active) {
      this.clear();
    }
    this.session = active;
  }

  private createEntry(url: string): CacheEntry {
    const entry: CacheEntry = { blobUrl: null, users: 0, detached: false, loading: Promise.resolve(null) };
    entry.loading = this.load(url).then((blobUrl) => {
      if (blobUrl === null) {
        // Помилку не кешуємо.
        if (this.cache.get(url) === entry) {
          this.cache.delete(url);
        }
        return null;
      }
      if (entry.detached) {
        // Поки файл вантажився, кеш очистили (інша сесія) чи запис витіснили: не показуємо.
        URL.revokeObjectURL(blobUrl);
        return null;
      }
      entry.blobUrl = blobUrl;
      return blobUrl;
    });
    return entry;
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

  /** Звільняє `blob:`-посилання прибраного з кешу запису, коли ним ніхто не користується. */
  private freeIfUnused(entry: CacheEntry): void {
    if (entry.detached && entry.users <= 0 && entry.blobUrl) {
      URL.revokeObjectURL(entry.blobUrl);
      entry.blobUrl = null;
    }
  }

  /**
   * Тримає в кеші не більше `MAX_CACHED` зображень: звільняє ті, що не
   * використовуються, найдавніші першими. Якщо всі використовуються, кеш
   * тимчасово більший за ліміт.
   */
  private trim(): void {
    for (const [url, entry] of this.cache) {
      if (this.cache.size <= MAX_CACHED) {
        return;
      }
      if (entry.users > 0) {
        continue;
      }
      this.cache.delete(url);
      entry.detached = true;
      this.freeIfUnused(entry);
    }
  }
}
