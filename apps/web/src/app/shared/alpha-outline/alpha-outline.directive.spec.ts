import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AlphaOutlineModule } from './alpha-outline.module';

@Component({
  template: `<img
    id="logo"
    src="logo.png"
    alt=""
    [appAlphaOutline]="width()"
    [outlineColor]="color()"
  />`,
  imports: [AlphaOutlineModule],
})
class HostComponent {
  readonly width = signal<number | string>(2);
  readonly color = signal<string | null>('rgb(240, 241, 241)');
}

@Component({
  template: `<img id="plain" src="logo.png" alt="" appAlphaOutline />`,
  imports: [AlphaOutlineModule],
})
class PlainHostComponent {}

describe('AlphaOutlineDirective', () => {
  afterEach(() => {
    document.querySelectorAll('svg[aria-hidden="true"]').forEach((svg) => svg.remove());
  });

  const filterIds = () => Array.from(document.querySelectorAll('filter')).map((f) => f.id);

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const image = fixture.nativeElement.querySelector('#logo') as HTMLImageElement;
    return { fixture, image };
  }

  const filterOf = (image: HTMLElement) => document.getElementById(/#([^)]+)\)/.exec(image.style.filter)![1])!;
  const floodColor = (filter: Element) => (filter.querySelector('feFlood') as SVGElement).style.getPropertyValue('flood-color');

  it('applies an SVG filter to the image, flooded with the outline colour', () => {
    const { image } = render();
    expect(image.style.filter).toMatch(/^url\(#app-alpha-outline-2-[0-9a-f]{8}\)$/);
    expect(filterIds()).toHaveLength(1);
    expect(floodColor(filterOf(image))).toBe('rgb(240, 241, 241)');
  });

  it('builds the outline from the alpha channel: shifted silhouettes merged, flooded with currentColor, the image on top', () => {
    render();
    const filter = filterOf(document.querySelector('#logo') as HTMLElement);
    const offsets = Array.from(filter.querySelectorAll('feOffset'));
    expect(offsets).toHaveLength(16);
    // Береться лише альфа-канал, а не весь піксель.
    expect(offsets.every((o) => o.getAttribute('in') === 'SourceAlpha')).toBe(true);
    // Усі зсуви лежать на колі радіуса 2.
    for (const offset of offsets) {
      const dx = Number(offset.getAttribute('dx'));
      const dy = Number(offset.getAttribute('dy'));
      expect(Math.hypot(dx, dy)).toBeCloseTo(2, 2);
    }
    expect(filter.querySelector('feComposite')?.getAttribute('operator')).toBe('in');
    const finalMerge = Array.from(filter.querySelectorAll('feMerge')).pop()!;
    expect(Array.from(finalMerge.querySelectorAll('feMergeNode')).map((n) => n.getAttribute('in'))).toEqual([
      'outline',
      'SourceGraphic',
    ]);
  });

  it('leaves room around the element so the outline is not clipped', () => {
    render();
    const filter = filterOf(document.querySelector('#logo') as HTMLElement);
    expect(filter.getAttribute('x')).toBe('-100%');
    expect(filter.getAttribute('width')).toBe('300%');
  });

  it('creates one filter per width and colour and reuses it', () => {
    const first = render();
    const base = first.image.style.filter;
    first.fixture.componentInstance.width.set(3);
    first.fixture.detectChanges();
    expect(first.image.style.filter).not.toBe(base);
    expect(first.image.style.filter).toMatch(/^url\(#app-alpha-outline-3-/);

    first.fixture.componentInstance.width.set(2);
    first.fixture.detectChanges();
    expect(first.image.style.filter).toBe(base);
    render();
    expect(filterIds()).toHaveLength(2);
  });

  it('accepts the width as a string attribute and falls back to 2px for a missing or invalid one', () => {
    const { fixture, image } = render();
    fixture.componentInstance.width.set('4');
    fixture.detectChanges();
    expect(image.style.filter).toMatch(/^url\(#app-alpha-outline-4-/);

    fixture.componentInstance.width.set('abc');
    fixture.detectChanges();
    expect(image.style.filter).toMatch(/^url\(#app-alpha-outline-2-/);

    const plain = TestBed.createComponent(PlainHostComponent);
    plain.detectChanges();
    expect((plain.nativeElement.querySelector('#plain') as HTMLElement).style.filter).toMatch(/^url\(#app-alpha-outline-2-/);
  });

  it('uses black when no outline colour is given', () => {
    const { fixture, image } = render();
    fixture.componentInstance.color.set(null);
    fixture.detectChanges();
    expect(floodColor(filterOf(image))).toBe('rgb(0, 0, 0)');
  });

  it('accepts a CSS variable as the colour: it is resolved at the filter, from the document root', () => {
    const { fixture, image } = render();
    fixture.componentInstance.color.set('var(--nav-avatar-bg)');
    fixture.detectChanges();
    expect(floodColor(filterOf(image))).toBe('var(--nav-avatar-bg)');
  });
});
