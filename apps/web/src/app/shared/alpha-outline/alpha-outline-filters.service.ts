import { Injectable } from '@angular/core';

const SVG_NS = 'http://www.w3.org/2000/svg';
/** Скільки копій силуета (по колу) зсуваємо й обʼєднуємо: чим більше, тим плавніший контур. */
const DIRECTIONS = 16;

/**
 * SVG-фільтри для контуру за альфа-каналом (див. `AlphaOutlineDirective`).
 * Кожен фільтр створюється один раз (на кожну товщину контуру) і лежить у
 * прихованому `<svg>` у `<body>`; на нього посилається CSS `filter: url(#id)`.
 */
@Injectable({ providedIn: 'root' })
export class AlphaOutlineFilters {
  private container: SVGSVGElement | null = null;
  private readonly created = new Set<string>();

  /**
   * `id` фільтра для контуру завтовшки `width` px і кольору `color` (створює його,
   * якщо ще нема). Колір — будь-яке CSS-значення; `var(--…)` береться з
   * кореня документа (`:root`), бо фільтр лежить у `<body>`, а не всередині
   * елемента, до якого застосовано.
   */
  ensure(width: number, color: string): string {
    const id = `app-alpha-outline-${String(width).replace('.', '_')}-${hash(color)}`;
    if (!this.created.has(id) && typeof document !== 'undefined') {
      this.getContainer().appendChild(this.buildFilter(id, width, color));
      this.created.add(id);
    }
    return id;
  }

  private getContainer(): SVGSVGElement {
    if (!this.container) {
      const svg = document.createElementNS(SVG_NS, 'svg');
      svg.setAttribute('width', '0');
      svg.setAttribute('height', '0');
      svg.setAttribute('aria-hidden', 'true');
      svg.style.position = 'absolute';
      document.body.appendChild(svg);
      this.container = svg;
    }
    return this.container;
  }

  /**
   * Контур = силует (альфа-канал) зображення, зсунутий у `DIRECTIONS` напрямків
   * по колу радіуса `width` й обʼєднаний, залитий кольором `color`; поверх
   * нього — саме зображення. Працює з будь-якою формою
   * (растр із прозорістю, вектор), бо береться лише альфа-канал.
   */
  private buildFilter(id: string, width: number, color: string): SVGFilterElement {
    const filter = document.createElementNS(SVG_NS, 'filter');
    filter.setAttribute('id', id);
    filter.setAttribute('color-interpolation-filters', 'sRGB');
    // Із запасом навколо елемента, щоб контур не обрізався.
    filter.setAttribute('x', '-100%');
    filter.setAttribute('y', '-100%');
    filter.setAttribute('width', '300%');
    filter.setAttribute('height', '300%');

    const union = document.createElementNS(SVG_NS, 'feMerge');
    union.setAttribute('result', 'silhouette');
    for (let i = 0; i < DIRECTIONS; i++) {
      const angle = (2 * Math.PI * i) / DIRECTIONS;
      const offset = document.createElementNS(SVG_NS, 'feOffset');
      offset.setAttribute('in', 'SourceAlpha');
      offset.setAttribute('dx', (width * Math.cos(angle)).toFixed(3));
      offset.setAttribute('dy', (width * Math.sin(angle)).toFixed(3));
      offset.setAttribute('result', `offset${i}`);
      filter.appendChild(offset);

      const node = document.createElementNS(SVG_NS, 'feMergeNode');
      node.setAttribute('in', `offset${i}`);
      union.appendChild(node);
    }
    filter.appendChild(union);

    const flood = document.createElementNS(SVG_NS, 'feFlood');
    flood.style.setProperty('flood-color', color);
    flood.setAttribute('result', 'color');
    filter.appendChild(flood);

    const outline = document.createElementNS(SVG_NS, 'feComposite');
    outline.setAttribute('in', 'color');
    outline.setAttribute('in2', 'silhouette');
    outline.setAttribute('operator', 'in');
    outline.setAttribute('result', 'outline');
    filter.appendChild(outline);

    const result = document.createElementNS(SVG_NS, 'feMerge');
    for (const source of ['outline', 'SourceGraphic']) {
      const node = document.createElementNS(SVG_NS, 'feMergeNode');
      node.setAttribute('in', source);
      result.appendChild(node);
    }
    filter.appendChild(result);
    return filter;
  }
}

/** Короткий безпечний для `id` відбиток рядка (FNV-1a). */
function hash(text: string): string {
  let value = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    value ^= text.charCodeAt(i);
    value = Math.imul(value, 0x01000193) >>> 0;
  }
  return value.toString(16).padStart(8, '0');
}
