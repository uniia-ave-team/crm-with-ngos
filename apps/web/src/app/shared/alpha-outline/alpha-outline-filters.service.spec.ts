import { TestBed } from '@angular/core/testing';

import { AlphaOutlineFilters } from './alpha-outline-filters.service';

describe('AlphaOutlineFilters', () => {
  let filters: AlphaOutlineFilters;

  beforeEach(() => {
    document.body.querySelectorAll('svg[aria-hidden="true"]').forEach((svg) => svg.remove());
    filters = TestBed.inject(AlphaOutlineFilters);
  });

  afterEach(() => {
    document.body.querySelectorAll('svg[aria-hidden="true"]').forEach((svg) => svg.remove());
  });

  const containers = () => Array.from(document.body.querySelectorAll(':scope > svg[aria-hidden="true"]'));
  const filterById = (id: string) => document.getElementById(id) as unknown as SVGFilterElement | null;

  it('creates one filter for a pair of width and color and gives the same id for the same pair again', () => {
    const id = filters.ensure(2, '#000');
    expect(filters.ensure(2, '#000')).toBe(id);
    expect(document.querySelectorAll(`[id="${id}"]`)).toHaveLength(1);
    expect(filterById(id)?.tagName.toLowerCase()).toBe('filter');
  });

  it('makes a separate filter for another width or another color', () => {
    const ids = [filters.ensure(2, '#000'), filters.ensure(3, '#000'), filters.ensure(2, 'var(--nav-avatar-bg)'), filters.ensure(1.5, '#000')];
    expect(new Set(ids).size).toBe(4);
    // Крапка товщини не потрапляє в `id`: на нього посилається CSS `url(#…)`.
    expect(ids[3]).toMatch(/^app-alpha-outline-1_5-[0-9a-f]{8}$/);
  });

  it('keeps all the filters in one hidden svg in the body', () => {
    filters.ensure(2, '#000');
    filters.ensure(4, '#fff');
    expect(containers()).toHaveLength(1);
    const [svg] = containers();
    expect(svg.getAttribute('width')).toBe('0');
    expect(svg.getAttribute('height')).toBe('0');
    expect(svg.querySelectorAll('filter')).toHaveLength(2);
  });

  it('fills the outline with the given color and lays the image itself over it', () => {
    const filter = filterById(filters.ensure(2, 'var(--nav-avatar-bg)'))!;
    expect((filter.querySelector('feFlood') as unknown as SVGElement).style.getPropertyValue('flood-color')).toBe('var(--nav-avatar-bg)');
    const last = filter.lastElementChild!;
    expect(last.tagName).toBe('feMerge');
    expect(Array.from(last.children).map((node) => node.getAttribute('in'))).toEqual(['outline', 'SourceGraphic']);
  });

  it('shifts the silhouette in sixteen directions around a circle of the given width', () => {
    const filter = filterById(filters.ensure(3, '#000'))!;
    const offsets = Array.from(filter.querySelectorAll('feOffset'));
    expect(offsets).toHaveLength(16);
    for (const offset of offsets) {
      expect(offset.getAttribute('in')).toBe('SourceAlpha');
      const radius = Math.hypot(Number(offset.getAttribute('dx')), Number(offset.getAttribute('dy')));
      expect(radius).toBeCloseTo(3, 2);
    }
  });
});
