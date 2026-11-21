import { Component, Injector, Signal, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
import { IonModal } from '@ionic/angular';

import { ClosingWaiters } from '../overlay/closing-waiters';

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
  imports: [IonModal],
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
  private readonly closing = new ClosingWaiters();
  readonly isOpen: Signal<boolean> = this.openSignal.asReadonly();

  protected readonly effectiveWidth: Signal<string> = computed(() => this.width() ?? 'fit-content');

  private readonly injector = inject(Injector);
  /** Ionic закриває вікно: між `willDismiss` і `didDismiss`. */
  private dismissing = false;
  /** Вікно попросили відкрити знову, поки воно ще закривалося. */
  private reopenRequested = false;

  /** Відкриває вікно по центру екрана. */
  open(): void {
    if (this.dismissing) {
      // Вікно ще закривається: відкриємо знову, щойно закриється (`onDismissed`). Якщо змінити
      // стан зараз, Ionic відкрив би вікно, а запізніле завершення закриття одразу його закрило б.
      this.reopenRequested = true;
      return;
    }
    this.openSignal.set(true);
  }

  /**
   * Закриває вікно. Проміс виконується, коли анімацію закриття завершено (або минув
   * `CLOSE_TIMEOUT_MS` з `ClosingWaiters`): якщо одразу після закриття прибрати сторінку, вікно, що ще
   * закривається, лишилося б висіти без стилів.
   */
  close(): Promise<void> {
    if (!this.openSignal()) {
      return Promise.resolve();
    }
    this.openSignal.set(false);
    return this.closing.wait();
  }

  protected onPresented(): void {
    this.opened.emit();
  }

  /** Ionic почав закривати вікно (затемнення, Esc чи `close()`). */
  protected onWillDismiss(): void {
    this.dismissing = true;
  }

  protected onDismissed(): void {
    // Закрилося саме (затемнення, Esc): ion-modal досі має `isOpen` = true.
    const closedByIonic = this.openSignal();
    this.dismissing = false;
    this.openSignal.set(false);
    this.closing.settle();
    this.closed.emit();

    if (this.reopenRequested) {
      this.reopenRequested = false;
      // Якщо закрив сам Ionic, спершу має дійти `isOpen` = false (після рендеру), інакше
      // нове `true` він не помітить.
      if (closedByIonic) {
        afterNextRender(() => this.open(), { injector: this.injector });
      } else {
        this.open();
      }
    }
  }
}
