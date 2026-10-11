import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';

import { PopoverComponent } from './popover.component';
import { PopoverModule } from './popover.module';

@Component({
  template: `
    <button id="anchor" type="button" (click)="popover.open($event)">open</button>
    <app-popover #popover [side]="side()" [compactSide]="compactSide()" [alignment]="alignment()" [label]="'Меню'">
      <p class="content">зміст</p>
    </app-popover>
  `,
  imports: [PopoverModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly side = signal<'end' | 'bottom'>('end');
  readonly compactSide = signal<'end' | 'bottom'>('bottom');
  readonly alignment = signal<'start' | 'center' | 'end'>('center');
}

describe('PopoverComponent', () => {
  function setWidth(width: number): void {
    Object.defineProperty(window, 'innerWidth', { value: width, configurable: true });
  }
  function setHeight(height: number): void {
    Object.defineProperty(window, 'innerHeight', { value: height, configurable: true });
  }

  afterEach(() => {
    setWidth(1024);
    setHeight(768);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const popover = fixture.debugElement.children.find((c) => c.componentInstance instanceof PopoverComponent)!
      .componentInstance as PopoverComponent;
    const ion = () => element.querySelector('ion-popover') as unknown as Record<string, unknown>;
    return { fixture, element, popover, ion, anchor: element.querySelector<HTMLElement>('#anchor')! };
  }

  it('is closed at first and opens by the click on its anchor', () => {
    const { fixture, popover, ion, anchor } = render();
    expect(popover.isOpen()).toBe(false);

    anchor.click();
    fixture.detectChanges();
    expect(popover.isOpen()).toBe(true);
    expect(ion()['isOpen']).toBe(true);
  });

  it('binds ion-popover to the anchor element through its id', () => {
    const { fixture, ion, anchor } = render();
    anchor.click();
    fixture.detectChanges();
    expect(ion()['trigger']).toBe('anchor');
    expect(ion()['reference']).toBe('trigger');
  });

  it('gives an anchor without an id a generated one', () => {
    const { fixture, popover, ion } = render();
    const bare = document.createElement('div');
    document.body.appendChild(bare);
    popover.open(bare);
    fixture.detectChanges();
    expect(bare.id).toMatch(/^app-popover-anchor-\d+$/);
    expect(ion()['trigger']).toBe(bare.id);
    bare.remove();
  });

  it('closes by close() and by the ion-popover dismissal, and reports it', () => {
    const { fixture, popover, ion, anchor } = render();
    let closed = 0;
    popover.closed.subscribe(() => closed++);

    anchor.click();
    fixture.detectChanges();
    popover.close();
    fixture.detectChanges();
    expect(popover.isOpen()).toBe(false);

    anchor.click();
    fixture.detectChanges();
    (ion() as unknown as HTMLElement).dispatchEvent(new CustomEvent('didDismiss'));
    fixture.detectChanges();
    expect(popover.isOpen()).toBe(false);
    expect(closed).toBe(1);
    // Після закриття прив'язки нема: ion-popover не слухає клацань сам.
    expect(ion()['trigger']).toBeUndefined();
  });

  it('resolves close() only after the ion-popover has been dismissed, and at once when it is not open', async () => {
    const { fixture, popover, ion, anchor } = render();
    await expect(popover.close()).resolves.toBeUndefined();

    anchor.click();
    fixture.detectChanges();
    let done = false;
    const closing = popover.close().then(() => (done = true));
    await Promise.resolve();
    expect(done).toBe(false);

    (ion() as unknown as HTMLElement).dispatchEvent(new CustomEvent('didDismiss'));
    await closing;
    expect(done).toBe(true);
  });

  it('stops waiting for the closing animation after a timeout', async () => {
    vi.useFakeTimers();
    try {
      const { fixture, popover, anchor } = render();
      anchor.click();
      fixture.detectChanges();
      let done = false;
      void popover.close().then(() => (done = true));
      await vi.advanceTimersByTimeAsync(1000);
      expect(done).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it('opens on the side for wide screens and on the compact side for narrow ones', () => {
    setWidth(1300);
    const wide = render();
    wide.anchor.click();
    wide.fixture.detectChanges();
    expect(wide.ion()['side']).toBe('end');

    setWidth(390);
    const narrow = render();
    narrow.anchor.click();
    narrow.fixture.detectChanges();
    expect(narrow.ion()['side']).toBe('bottom');
  });

  it('follows the alignment and the label', () => {
    const { fixture, element, ion, anchor } = render();
    fixture.componentInstance.alignment.set('end');
    anchor.click();
    fixture.detectChanges();
    expect(ion()['alignment']).toBe('end');
    expect(element.querySelector('app-popover')).not.toBeNull();
  });

  it('ignores open() without an element', () => {
    const { popover } = render();
    popover.open(new Event('click'));
    expect(popover.isOpen()).toBe(false);
  });

  it.each([
    ['width', () => setWidth(900)],
    ['height', () => setHeight(500)],
  ])('closes when the window %s changes, because the position is not recalculated', (_name, change) => {
    const { fixture, popover, anchor } = render();
    let closed = 0;
    popover.closed.subscribe(() => closed++);
    anchor.click();
    fixture.detectChanges();
    expect(popover.isOpen()).toBe(true);

    change();
    window.dispatchEvent(new Event('resize'));
    fixture.detectChanges();
    expect(popover.isOpen()).toBe(false);
  });

  it('stays open when a resize event brings no actual change of the window size', () => {
    const { fixture, popover, anchor } = render();
    anchor.click();
    fixture.detectChanges();
    window.dispatchEvent(new Event('resize'));
    expect(popover.isOpen()).toBe(true);
  });

  it('does nothing on resize while it is closed', () => {
    const { popover } = render();
    setWidth(700);
    window.dispatchEvent(new Event('resize'));
    expect(popover.isOpen()).toBe(false);
  });

  it('keeps a gap from the anchor: to the right for side end, downwards for bottom', () => {
    setWidth(1300);
    const wide = render();
    wide.anchor.click();
    wide.fixture.detectChanges();
    const wideStyle = (wide.element.querySelector('ion-popover') as HTMLElement).style;
    expect(wideStyle.getPropertyValue('--offset-x')).toBe('12px');
    expect(wideStyle.getPropertyValue('--offset-y')).toBe('0px');

    setWidth(390);
    const narrow = render();
    narrow.anchor.click();
    narrow.fixture.detectChanges();
    const narrowStyle = (narrow.element.querySelector('ion-popover') as HTMLElement).style;
    expect(narrowStyle.getPropertyValue('--offset-x')).toBe('0px');
    expect(narrowStyle.getPropertyValue('--offset-y')).toBe('12px');
  });

  it('uses the compact width on narrow screens and the normal width otherwise', () => {
    @Component({
      template: `<button id="a" (click)="p.open($event)">x</button>
        <app-popover #p width="300px" compactWidth="min(340px, calc(100vw - 24px))">y</app-popover>`,
      imports: [PopoverModule],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    class WidthHostComponent {}

    const widthOf = (screen: number) => {
      setWidth(screen);
      const fixture = TestBed.createComponent(WidthHostComponent);
      fixture.detectChanges();
      fixture.nativeElement.querySelector('#a').click();
      fixture.detectChanges();
      return (fixture.nativeElement.querySelector('ion-popover') as HTMLElement).style.getPropertyValue('--app-popover-width');
    };
    expect(widthOf(1300)).toBe('300px');
    expect(widthOf(390)).toBe('min(340px, calc(100vw - 24px))');
  });

  it('falls back to fit-content when no width is given', () => {
    const { fixture, element, anchor } = render();
    anchor.click();
    fixture.detectChanges();
    expect((element.querySelector('ion-popover') as HTMLElement).style.getPropertyValue('--app-popover-width')).toBe(
      'fit-content',
    );
  });

  it('keeps its width after Ionic strips the inline --width when the popover closes', () => {
    @Component({
      template: `<button id="a" (click)="p.open($event)">x</button>
        <app-popover #p width="300px">y</app-popover>`,
      imports: [PopoverModule],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    class StripHostComponent {}

    setWidth(1300);
    const fixture = TestBed.createComponent(StripHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('#a').click();
    fixture.detectChanges();
    const ion = fixture.nativeElement.querySelector('ion-popover') as HTMLElement;

    // Те, що робить Ionic при закритті (md.leave.js): знімає inline --width.
    ion.style.removeProperty('--width');
    fixture.detectChanges();

    expect(ion.style.getPropertyValue('--app-popover-width')).toBe('300px');
  });
});
