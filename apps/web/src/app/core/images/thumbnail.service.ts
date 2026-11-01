import { Injectable } from '@angular/core';

/** Як вписати зображення в квадрат мініатюри. */
export type ThumbnailFit = 'cover' | 'contain';

/** Зображення, яке можна намалювати на полотні. */
export interface DrawableImage {
  readonly width: number;
  readonly height: number;
  readonly source: CanvasImageSource;
}

/**
 * Мініатюри: стискає зображення до квадрата заданого розміру й віддає його
 * `data:`-посиланням, яке можна зберегти в записі акаунта.
 *
 * - `cover` (аватар) — зображення заповнює квадрат, зайве обрізається по центру;
 * - `contain` (логотип) — зображення вписується цілком, прозорі поля лишаються
 *   прозорими.
 *
 * Формат — WebP з прозорістю (браузер, що його не вміє, дає PNG).
 */
@Injectable({ providedIn: 'root' })
export class ThumbnailService {
  /**
   * Мініатюра зображення за посиланням (`blob:`, `data:` або з того самого сайту);
   * `null`, якщо зображення не вдалося прочитати чи намалювати.
   */
  async create(src: string, size: number, fit: ThumbnailFit): Promise<string | null> {
    try {
      const image = await this.load(src);
      const canvas = document.createElement('canvas');
      canvas.width = size;
      canvas.height = size;
      const context = canvas.getContext('2d');
      if (!context || image.width <= 0 || image.height <= 0) {
        return null;
      }
      const scale = fit === 'cover' ? Math.max(size / image.width, size / image.height) : Math.min(size / image.width, size / image.height);
      const width = image.width * scale;
      const height = image.height * scale;
      context.drawImage(image.source, (size - width) / 2, (size - height) / 2, width, height);
      const data = canvas.toDataURL('image/webp', 0.85);
      return data.startsWith('data:image/') ? data : null;
    } catch (error) {
      console.warn('[ThumbnailService] Не вдалося зробити мініатюру', error);
      return null;
    }
  }

  /** Завантажує зображення в елемент `Image` (так читаються й SVG, у яких може не бути власного розміру). */
  protected async load(src: string): Promise<DrawableImage> {
    const element = new Image();
    element.src = src;
    await element.decode();
    // SVG без розмірів дає 0×0: беремо умовний розмір, далі масштабування підженеться.
    return { width: element.naturalWidth || 128, height: element.naturalHeight || 128, source: element };
  }
}
