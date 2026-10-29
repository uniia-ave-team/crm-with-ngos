import { Component, ElementRef, Signal, effect, input, output, signal, viewChild } from '@angular/core';

/**
 * Універсальне висувне бокове вікно: зміст довільний (`ng-content`), праворуч від
 * вмісту сторінки, згори донизу сторінки (поверх її шапки). Закривається кнопкою
 * з хрестиком, клавішею Esc і кліком будь-де поза вікном (по іншому елементу
 * інтерфейсу чи порожньому тілу сторінки); фокус при відкритті йде на хрестик, а при
 * закритті повертається на елемент, що його мав.
 *
 *   <button (click)="panel.open()">…</button>
 *   <app-side-panel #panel label="Редагувати подію" (closed)="…">
 *     …будь-який зміст…
 *   </app-side-panel>
 *
 * Зліва в шапці, на одній висоті з хрестиком, стоїть те, що передано з атрибутом
 * `sidePanelHeader` (наприклад, кнопка дії вікна):
 *
 *   <app-side-panel>
 *     <button sidePanelHeader (click)="add()">Додати</button>
 *     …зміст…
 *   </app-side-panel>
 *
 * Розміри: на десктопі ширина `width` (за замовчуванням 280px — як у другої
 * бічної панелі, `ion-menu` в `app.component.scss`); нижче 992px (брейкпоінт
 * десктопа, як у меню) вікно займає всю ширину сторінки. Вікно позиціонується
 * абсолютно всередині сторінки (`.ion-page` обмежує його своїми межами), тож на
 * телефоні воно покриває сторінку між верхньою панеллю з лого й нижнім меню, а на
 * десктопі — праву частину екрана праворуч від рейла.
 */
@Component({
  selector: 'app-side-panel',
  templateUrl: './side-panel.component.html',
  styleUrls: ['./side-panel.component.scss'],
  host: { '(document:keydown.escape)': 'onEscape()' },
  standalone: false,
})
export class SidePanelComponent {
  /** Ширина на десктопі (CSS-значення). */
  readonly width = input('280px');
  /** Назва вікна для допоміжних технологій. */
  readonly label = input<string | null>(null);

  /** Вікно відкрилося. */
  readonly opened = output<void>();
  /** Вікно закрилося (будь-яким способом). */
  readonly closed = output<void>();

  private readonly openSignal = signal(false);
  readonly isOpen: Signal<boolean> = this.openSignal.asReadonly();

  private readonly closeButton = viewChild<ElementRef<HTMLButtonElement>>('closeButton');
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  /** Елемент, що мав фокус до відкриття: на нього фокус повертається при закритті. */
  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    // Клік поза вікном закриває його. Слухач стоїть на фазі занурення (capture),
    // тому спрацьовує до обробників самого кліка: кнопка, що відкриває вікно,
    // не закриє його одразу ж, а клік по кнопці вікна (її розмітка може
    // змінитися під час обробки) завжди лишається всередині.
    effect((onCleanup) => {
      if (!this.openSignal()) {
        return;
      }
      const onClick = (event: Event) => {
        const panel = this.panel()?.nativeElement;
        if (panel && !event.composedPath().includes(panel)) {
          this.close();
        }
      };
      document.addEventListener('click', onClick, true);
      onCleanup(() => document.removeEventListener('click', onClick, true));
    });
  }

  /** Відкриває вікно. */
  open(): void {
    if (this.openSignal()) {
      return;
    }
    const active = document.activeElement;
    this.previouslyFocused = active instanceof HTMLElement ? active : null;
    this.openSignal.set(true);
    this.opened.emit();
    // Хрестик стає доступним (знімається `inert`) після оновлення розмітки.
    setTimeout(() => this.closeButton()?.nativeElement.focus());
  }

  /** Закриває вікно. */
  close(): void {
    if (!this.openSignal()) {
      return;
    }
    this.openSignal.set(false);
    this.closed.emit();
    const target = this.previouslyFocused;
    this.previouslyFocused = null;
    target?.focus();
  }

  protected onEscape(): void {
    this.close();
  }
}
