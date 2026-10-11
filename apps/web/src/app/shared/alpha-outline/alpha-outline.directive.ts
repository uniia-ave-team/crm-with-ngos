import { Directive, ElementRef, effect, inject, input } from '@angular/core';

import { AlphaOutlineFilters } from './alpha-outline-filters.service';

/**
 * Малює контур навколо зображення за його альфа-каналом (а не по рамці
 * елемента): однаково для растра з прозорістю (PNG, WebP) і для вектора (SVG)
 * довільної форми. Контур — це силует зображення, розширений на задану
 * товщину й залитий заданим кольором.
 *
 *   <img src="logo.png" appAlphaOutline="2" outlineColor="var(--nav-avatar-bg)" alt="" />
 *
 * - `appAlphaOutline` — товщина контуру в px (за замовчуванням 2);
 * - `outlineColor` — колір (будь-яке CSS-значення, зокрема `var(--…)` зі змінних
 *   кореня документа); за замовчуванням чорний.
 *
 * Контур виходить за межі елемента, тож місце під нього (відступ від сусідів)
 * лишається на розсуд розмітки.
 */
@Directive({
  selector: '[appAlphaOutline]',
  standalone: false,
})
export class AlphaOutlineDirective {
  readonly appAlphaOutline = input<number | string>('');
  readonly outlineColor = input<string | null>(null);

  private readonly element: HTMLElement = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly filters = inject(AlphaOutlineFilters);

  constructor() {
    effect(() => {
      const width = parseWidth(this.appAlphaOutline());
      this.element.style.filter = `url(#${this.filters.ensure(width, this.outlineColor() || DEFAULT_COLOR)})`;
    });
  }
}

const DEFAULT_WIDTH = 2;
const DEFAULT_COLOR = '#000';

/** Товщина з атрибута: порожнє або некоректне значення — стандартна. */
function parseWidth(value: number | string): number {
  const width = typeof value === 'number' ? value : parseFloat(value);
  return Number.isFinite(width) && width > 0 ? width : DEFAULT_WIDTH;
}
