import { Component, DestroyRef, Signal, computed, inject, input, output, signal } from '@angular/core';

let nextAnchorId = 0;

/** Скільки (мс) чекаємо завершення анімації закриття, перш ніж вважати вікно закритим. */
const CLOSE_TIMEOUT_MS = 1000;

/** Бік відносно елемента, до якого прив'язано вікно (як у `ion-popover`). */
export type PopoverSide = 'top' | 'bottom' | 'start' | 'end' | 'left' | 'right';
/** Вирівнювання вікна вздовж цього боку. */
export type PopoverAlignment = 'start' | 'center' | 'end';

/**
 * Універсальне спливаюче діалогове вікно, прив'язане до елемента.
 * Зміст довільний (`ng-content`), вікно саме закривається клацанням поза ним і
 * клавішею Esc, а фокус повертається на елемент, що його відкрив (це робить
 * `ion-popover`, на якому воно збудоване).
 *
 * Адаптивність: на екранах вужчих за `compactBreakpoint` (за замовчуванням 992 px,
 * як у меню) вікно відкривається з боку `compactSide` замість `side`.
 *
 *   <button (click)="menu.open($event)">…</button>
 *   <app-popover #menu side="end" compactSide="bottom" label="Меню">
 *     …будь-який зміст…
 *   </app-popover>
 *
 * `open()` приймає подію клацання (береться її елемент) або сам елемент.
 */
@Component({
  selector: 'app-popover',
  templateUrl: './popover.component.html',
  styleUrls: ['./popover.component.scss'],
  standalone: false,
})
export class PopoverComponent {
  /** Бік вікна на широких екранах. */
  readonly side = input<PopoverSide>('end');
  /** Бік вікна на вузьких екранах. */
  readonly compactSide = input<PopoverSide>('bottom');
  readonly alignment = input<PopoverAlignment>('center');
  /** Вирівнювання на вузьких екранах (за замовчуванням те саме, що й `alignment`). */
  readonly compactAlignment = input<PopoverAlignment | null>(null);
  /** Ширина вікна (CSS-значення); за замовчуванням — за змістом. */
  readonly width = input<string | null>(null);
  /** Ширина на вузьких екранах (за замовчуванням та сама, що й `width`). */
  readonly compactWidth = input<string | null>(null);
  /** Відступ вікна від елемента, до якого воно прив'язане (px). */
  readonly gap = input(12);
  /** Ширина екрана (px), нижче якої діє `compactSide`. */
  readonly compactBreakpoint = input(992);
  /** Назва вікна для допоміжних технологій. */
  readonly label = input<string | null>(null);

  /** Вікно відкрилося. */
  readonly opened = output<void>();
  /** Вікно закрилося (будь-яким способом). */
  readonly closed = output<void>();

  private readonly openSignal = signal(false);
  /** Ті, хто чекає завершення закриття (`close()`). */
  private closeWaiters: Array<() => void> = [];
  readonly isOpen: Signal<boolean> = this.openSignal.asReadonly();

  /** `id` елемента, до якого вікно прив'язано, поки воно відкрите (`ion-popover` прив'язується за `trigger`). */
  protected readonly anchorId = signal<string | undefined>(undefined);

  private readonly viewportWidth = signal(typeof window === 'undefined' ? Infinity : window.innerWidth);
  /** Розмір вікна браузера на момент відкриття: його зміна закриває вікно. */
  private openedViewport: { width: number; height: number } | null = null;

  protected readonly compact: Signal<boolean> = computed(() => this.viewportWidth() < this.compactBreakpoint());
  protected readonly effectiveSide: Signal<PopoverSide> = computed(() =>
    this.compact() ? this.compactSide() : this.side(),
  );
  protected readonly effectiveAlignment: Signal<PopoverAlignment> = computed(() =>
    this.compact() ? (this.compactAlignment() ?? this.alignment()) : this.alignment(),
  );

  protected readonly effectiveWidth: Signal<string> = computed(
    () => (this.compact() ? (this.compactWidth() ?? this.width()) : this.width()) ?? 'fit-content',
  );

  /** Зсув від елемента вздовж осі X (px): від'ємний для боку start/left, додатний для end/right. */
  protected readonly offsetX: Signal<number> = computed(() => {
    const side = this.effectiveSide();
    return side === 'end' || side === 'right' ? this.gap() : side === 'start' || side === 'left' ? -this.gap() : 0;
  });
  /** Зсув від елемента вздовж осі Y (px): додатний для боку bottom, від'ємний для top. */
  protected readonly offsetY: Signal<number> = computed(() => {
    const side = this.effectiveSide();
    return side === 'bottom' ? this.gap() : side === 'top' ? -this.gap() : 0;
  });

  constructor() {
    if (typeof window !== 'undefined') {
      // Позиція вікна рахується один раз при відкритті й сама за розміром
      // вікна браузера не змінюється, тож при зміні розміру просто закриваємо.
      const onResize = () => {
        this.viewportWidth.set(window.innerWidth);
        const opened = this.openedViewport;
        if (this.openSignal() && opened && (opened.width !== window.innerWidth || opened.height !== window.innerHeight)) {
          void this.close();
        }
      };
      window.addEventListener('resize', onResize);
      inject(DestroyRef).onDestroy(() => window.removeEventListener('resize', onResize));
    }
  }

  /** Відкриває вікно біля елемента: подія клацання (береться її елемент) або сам елемент. */
  open(anchor: Event | HTMLElement): void {
    const element = anchor instanceof Event ? (anchor.currentTarget ?? anchor.target) : anchor;
    if (!(element instanceof HTMLElement)) {
      return;
    }
    // ion-popover знаходить елемент за id: даємо його, якщо в елемента ще нема.
    if (!element.id) {
      element.id = `app-popover-anchor-${++nextAnchorId}`;
    }
    this.viewportWidth.set(window.innerWidth);
    this.openedViewport = { width: window.innerWidth, height: window.innerHeight };
    this.anchorId.set(element.id);
    this.openSignal.set(true);
  }

  /**
   * Закриває вікно. Проміс виконується, коли анімацію закриття завершено (або
   * минув `CLOSE_TIMEOUT_MS`): якщо одразу після закриття прибрати сторінку (наприклад,
   * перейти на /login), вікно, що ще закривається, лишилося б висіти без стилів.
   */
  close(): Promise<void> {
    if (!this.openSignal()) {
      return Promise.resolve();
    }
    this.openSignal.set(false);
    this.openedViewport = null;
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
    this.openedViewport = null;
    // Без прив'язки ion-popover не слухає клацань по елементу: наступне
    // відкриття знову йде через open().
    this.anchorId.set(undefined);
    const waiters = this.closeWaiters;
    this.closeWaiters = [];
    waiters.forEach((done) => done());
    this.closed.emit();
  }
}
