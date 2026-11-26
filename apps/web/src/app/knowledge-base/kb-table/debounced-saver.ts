import { Signal, signal } from '@angular/core';

/** Затримка після останньої зміни перед запитом: поки людина пише, запитів нема. */
export const SAVE_DELAY_MS = 2000;

/**
 * Стан збереження для тегу в шапці: усе збережено, зміни чекають чи йдуть на сервер, або сервер їх
 * не прийняв (лишаються незбереженими й підуть разом із наступною зміною).
 */
export type SaveState = 'saved' | 'saving' | 'failed';

/**
 * Відкладене збереження: після кожної зміни (`changed`) чекає `delayMs` тиші, тоді викликає `save`.
 * Збереження йдуть по черзі (нове чекає, поки скінчиться попереднє), тож новий рядок не створиться
 * двічі. Стан `saving` — від першої зміни до відповіді на останнє збереження; `save` повертає, чи
 * все вдалося (`false` — `failed`, доки не прийде наступна зміна).
 */
export class DebouncedSaver {
  private readonly stateSignal = signal<SaveState>('saved');
  readonly state: Signal<SaveState> = this.stateSignal.asReadonly();

  private timer: ReturnType<typeof setTimeout> | undefined;
  private queue: Promise<void> = Promise.resolve();
  /** Є зміни, які ще не передано в `save`. */
  private dirty = false;

  constructor(
    private readonly save: () => Promise<boolean>,
    private readonly delayMs = SAVE_DELAY_MS,
  ) {}

  /** Щось змінилося: тег — «Зберігаємо зміни», відлік затримки починається заново. */
  changed(): void {
    this.dirty = true;
    this.stateSignal.set('saving');
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.flush(), this.delayMs);
  }

  /** Повторити невдале збереження (клік по тегу «Зміни не збережено»): одразу, без затримки. */
  retry(): void {
    this.dirty = true;
    this.stateSignal.set('saving');
    void this.flush();
  }

  /**
   * Зберігає зараз, не чекаючи кінця затримки (перед перезавантаженням записів, переходом на іншу
   * таблицю чи закриттям виду). Проміс — коли все, що було змінено, збережено (чи не вдалося).
   */
  flush(): Promise<void> {
    clearTimeout(this.timer);
    this.timer = undefined;
    this.queue = this.queue.then(async () => {
      if (!this.dirty) {
        return;
      }
      this.dirty = false;
      const ok = await this.save();
      if (!ok) {
        this.stateSignal.set('failed');
      } else if (!this.dirty && this.timer === undefined) {
        this.stateSignal.set('saved');
      }
    });
    return this.queue;
  }
}
