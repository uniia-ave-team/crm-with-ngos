import { Component, Signal, computed, input, output, signal } from '@angular/core';

/** Скільки (мс) чекаємо завершення анімації закриття, перш ніж вважати вікно закритим. */
const CLOSE_TIMEOUT_MS = 1000;

/**
 * Універсальне модальне вікно: діалог по центру екрана, що затемнює весь інший вміст —
 * і сторінку, і меню, тобто весь екран (це робить `ion-modal`, на якому воно збудоване:
 * він відкривається поверх усього застосунку з підкладкою на весь екран). Вигляд — як у
 * спливаючого вікна (`app-popover`, меню акаунтів): ті самі токени, заокруглення й тінь,
 * тільки по центру.
 *
 * Зміст довільний (`ng-content`). Вікно закривається клацанням по затемненню (вимикається
 * входом `dismissOnBackdrop`), клавішею Esc і викликом `close()`; фокус тримається всередині
 * й повертається на елемент, що його відкрив (це теж робить `ion-modal`).
 *
 *   <button (click)="dialog.open()">…</button>
 *   <app-modal #dialog width="360px" label="Підтвердження" (closed)="…">
 *     …будь-який зміст…
 *   </app-modal>
 */
@Component({
  selector: 'app-modal',
  templateUrl: './modal.component.html',
  styleUrls: ['./modal.component.scss'],
  standalone: false,
})
export class ModalComponent {
  /** Ширина вікна (CSS-значення); за замовчуванням — за змістом. */
  readonly width = input<string | null>(null);
  /** Назва вікна для допоміжних технологій. */
  readonly label = input<string | null>(null);
  /** Чи закривається вікно клацанням по затемненню навколо. */
  readonly dismissOnBackdrop = input(true);

  /** Вікно відкрилося. */
  readonly opened = output<void>();
  /** Вікно закрилося (будь-яким способом). */
  readonly closed = output<void>();

  private readonly openSignal = signal(false);
  /** Ті, хто чекає завершення закриття (`close()`). */
  private closeWaiters: Array<() => void> = [];
  readonly isOpen: Signal<boolean> = this.openSignal.asReadonly();

  protected readonly effectiveWidth: Signal<string> = computed(() => this.width() ?? 'fit-content');

  /** Відкриває вікно по центру екрана. */
  open(): void {
    this.openSignal.set(true);
  }

  /**
   * Закриває вікно. Проміс виконується, коли анімацію закриття завершено (або минув
   * `CLOSE_TIMEOUT_MS`): якщо одразу після закриття прибрати сторінку, вікно, що ще
   * закривається, лишилося б висіти без стилів.
   */
  close(): Promise<void> {
    if (!this.openSignal()) {
      return Promise.resolve();
    }
    this.openSignal.set(false);
    return new Promise((resolve) => {
      const timer = setTimeout(done, CLOSE_TIMEOUT_MS);
      const waiters = this.closeWaiters;
      function done(): void {
        clearTimeout(timer);
        resolve();
      }
      waiters.push(done);
    });
  }

  protected onPresented(): void {
    this.opened.emit();
  }

  protected onDismissed(): void {
    this.openSignal.set(false);
    const waiters = this.closeWaiters;
    this.closeWaiters = [];
    waiters.forEach((done) => done());
    this.closed.emit();
  }
}
