/** Скільки (мс) чекаємо завершення анімації закриття, перш ніж вважати вікно закритим. */
export const CLOSE_TIMEOUT_MS = 1000;

/**
 * Очікування кінця анімації закриття вікна (`app-popover`, `app-modal` — обидва на оверлеях
 * Ionic). `close()` вікна віддає проміс, який виконується, коли вікно справді закрилося
 * (`settle()` з обробника `didDismiss`), а якщо цього не сталося за `CLOSE_TIMEOUT_MS` — однаково.
 * Так той, хто закриває вікно й одразу прибирає сторінку (наприклад, переходить на іншу),
 * не лишає вікно, що ще закривається, висіти без стилів.
 */
export class ClosingWaiters {
  private waiters: Array<() => void> = [];

  /** Проміс кінця закриття: виконується на `settle()` або через `timeoutMs`. */
  wait(timeoutMs = CLOSE_TIMEOUT_MS): Promise<void> {
    return new Promise((resolve) => {
      const timer = setTimeout(done, timeoutMs);
      function done(): void {
        clearTimeout(timer);
        resolve();
      }
      this.waiters.push(done);
    });
  }

  /** Вікно закрилося: виконує всі очікування, що є. */
  settle(): void {
    const waiters = this.waiters;
    this.waiters = [];
    waiters.forEach((done) => done());
  }
}
