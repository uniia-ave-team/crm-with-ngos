import { Component, DestroyRef, ElementRef, Injector, Signal, afterNextRender, inject, input, output, signal, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Рядок шапки сторінки з пошуком: заголовок, кнопки дій (`ng-content`), поле пошуку й лупа.
 *
 *   <ion-header>
 *     <ion-toolbar>
 *       <app-search-header [title]="…" [placeholder]="…" [maxLength]="100" (searchChange)="onSearch($event)">
 *         <app-action-button icon="people" tone="blue" [label]="…" (click)="…" />
 *       </app-search-header>
 *     </ion-toolbar>
 *   </ion-header>
 *
 * Лупа вмикає пошук (`open`): заголовок виїжджає за лівий край, кнопки (`app-action-button`)
 * стискаються до іконок і зсуваються на його місце, а поле росте на звільненому місці й отримує
 * курсор; лупа стає хрестиком. Хрестик чи Esc у полі повертають усе назад, поле порожніє, а
 * фокус іде на лупу. Усе — CSS-переходами тих самих елементів з однаковою тривалістю й без
 * прозорості: суцільний зсув. Ширини передаються через `flex-grow` (заголовок 1 → 0, поле
 * 0 → 1), тож рух рівномірний і без стрибків. Схований заголовок і закрите поле — `inert`: до
 * них не дістатися ні клавіатурою, ні допоміжними технологіями.
 *
 * `searchChange` — текст поля при кожній його зміні, як є (з пробілами); закриття з непорожнім полем
 * теж зміна: `searchChange` віддає порожній рядок.
 */
/** Трохи довше за анімацію (0.25s у стилях): тоді ширину назви звільняємо й без події `transitionend`. */
const CLOSE_FALLBACK_MS = 300;

@Component({
  selector: 'app-search-header',
  templateUrl: './search-header.component.html',
  styleUrls: ['./search-header.component.scss'],
  // Пошук відкрито — клас і на самому елементі: так сторінка може сховати щось своє в шапці (тег).
  host: { '[class.search-header-host--open]': 'open()' },
  imports: [IonIcon, TranslatePipe],
})
export class SearchHeaderComponent {
  readonly title = input.required<string>();
  /** Підказка в порожньому полі. */
  readonly placeholder = input('');
  /**
   * Скільки рядків може займати назва: 1 (за замовчуванням) — в один рядок із трикрапкою; більше —
   * переноситься, а після останнього рядка трикрапка (довгі назви таблиць). Повна назва — у підказці.
   */
  readonly titleLines = input(1);
  /** Найбільша довжина тексту в полі; `null` — без обмеження. */
  readonly maxLength = input<number | null>(null);

  /** Текст поля при кожній його зміні. */
  readonly searchChange = output<string>();

  private readonly openSignal = signal(false);
  /** Шапка в режимі пошуку: заголовок прибрано, кнопки стиснуто до іконок, видно поле й хрестик. */
  readonly open: Signal<boolean> = this.openSignal.asReadonly();
  protected readonly text = signal('');

  /**
   * Ширина назви, зафіксована на час пошуку й анімації закриття: місце під назву тоді стискається
   * до нуля й росте назад, і назва в кілька рядків (`titleLines`) інакше перескладалася б на кожному
   * кадрі. З фіксованою шириною текст не перескладається, а лише зсувається. `null` — як завжди.
   */
  protected readonly titleWidth = signal<number | null>(null);
  private readonly titleElement = viewChild.required<ElementRef<HTMLElement>>('titleText');
  private releaseTimer: ReturnType<typeof setTimeout> | undefined;

  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('input');
  private readonly toggle = viewChild.required<ElementRef<HTMLButtonElement>>('toggle');
  private readonly injector = inject(Injector);

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.releaseTimer));
  }

  protected onToggle(): void {
    if (this.open()) {
      this.close();
    } else {
      clearTimeout(this.releaseTimer);
      if (this.titleWidth() === null) {
        this.titleWidth.set(this.titleElement().nativeElement.getBoundingClientRect().width);
      }
      this.openSignal.set(true);
      afterNextRender(() => this.input().nativeElement.focus(), { injector: this.injector });
    }
  }

  protected onTitleSlotTransitionEnd(event: TransitionEvent): void {
    if (event.propertyName === 'flex-grow') {
      this.releaseTitleWidth();
    }
  }

  private releaseTitleWidth(): void {
    if (!this.open()) {
      clearTimeout(this.releaseTimer);
      this.titleWidth.set(null);
    }
  }

  protected onInput(value: string): void {
    this.text.set(value);
    this.searchChange.emit(value);
  }

  protected close(): void {
    this.openSignal.set(false);
    // Ширина звільняється, коли місце під назву розгорнулося (`onTitleSlotTransitionEnd`); запасний
    // таймер — якщо анімації нема (`prefers-reduced-motion`) чи подія не прийшла.
    clearTimeout(this.releaseTimer);
    this.releaseTimer = setTimeout(() => this.releaseTitleWidth(), CLOSE_FALLBACK_MS);
    afterNextRender(() => this.toggle().nativeElement.focus(), { injector: this.injector });
    if (this.text()) {
      this.onInput('');
    }
  }
}
