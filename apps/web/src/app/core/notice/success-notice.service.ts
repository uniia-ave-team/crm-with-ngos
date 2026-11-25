import { Injectable, Signal, signal } from '@angular/core';

/**
 * Коротке повідомлення про успіх, що переживає перехід на іншу сторінку: його показує плашка
 * `ion-toast` у корені застосунку (`AppComponent`), а не сторінка, яка вже закривається.
 * Наприклад, `/reset-password` після нового пароля веде на `/relogin` (а звідти, без збережених
 * акаунтів, на `/login`), а «Пароль змінено…» видно вже там.
 *
 *   inject(SuccessNoticeService).show('resetPassword.done');
 */
@Injectable({ providedIn: 'root' })
export class SuccessNoticeService {
  private readonly keySignal = signal<string | null>(null);
  /** Ключ тексту повідомлення в мовному файлі; `null` — повідомлення нема. */
  readonly key: Signal<string | null> = this.keySignal.asReadonly();

  show(key: string): void {
    this.keySignal.set(key);
  }

  /** Плашку закрили (хрестиком чи сама зникла). */
  dismiss(): void {
    this.keySignal.set(null);
  }
}
