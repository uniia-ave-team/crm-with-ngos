import { DestroyRef, Directive, ElementRef, Injector, afterNextRender, effect, inject, input } from '@angular/core';

/**
 * Висота поля `<textarea>` — за текстом: без прокрутки всередині, росте з кожним новим рядком і
 * меншає, коли текст стирають. Перераховується, коли людина пише, коли значення приходить ззовні
 * (вхід `appAutoSize` — те саме значення, що й у `[value]`) і коли змінюється ширина поля (текст
 * переноситься інакше, наприклад після повороту телефона чи зміни розміру вікна).
 *
 *   <textarea rows="1" [value]="text" [appAutoSize]="text" (input)="…"></textarea>
 */
@Directive({
  selector: 'textarea[appAutoSize]',
  host: { '(input)': 'resize()' },
})
export class AutoSizeDirective {
  /** Значення поля: його зміна ззовні теж перераховує висоту. */
  readonly appAutoSize = input<string | null | undefined>('');

  private readonly element = inject<ElementRef<HTMLTextAreaElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  constructor() {
    // Лише ширина: висоту змінює сама директива, тож на неї не реагуємо (інакше — цикл).
    if (typeof ResizeObserver !== 'undefined') {
      let width = -1;
      const observer = new ResizeObserver(([entry]) => {
        if (entry && entry.contentRect.width !== width) {
          width = entry.contentRect.width;
          this.resize();
        }
      });
      observer.observe(this.element);
      inject(DestroyRef).onDestroy(() => observer.disconnect());
    }
    effect(() => {
      this.appAutoSize();
      // Висоту видно лише після того, як нове значення потрапило в поле.
      afterNextRender(() => this.resize(), { injector: this.injector });
    });
  }

  resize(): void {
    this.element.style.height = 'auto';
    this.element.style.height = `${this.element.scrollHeight}px`;
  }
}
