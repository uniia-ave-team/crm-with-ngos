import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AutoSizeDirective } from './auto-size.directive';

@Component({
  imports: [AutoSizeDirective],
  template: `<textarea rows="1" [value]="text()" [appAutoSize]="text()"></textarea>`,
})
class HostComponent {
  readonly text = signal('один');
}

describe('AutoSizeDirective', () => {
  it('sets the height of the field to its text, when typing and when the value comes from outside', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea')!;
    let scrollHeight = 20;
    Object.defineProperty(textarea, 'scrollHeight', { get: () => scrollHeight });
    await fixture.whenStable();

    scrollHeight = 60;
    textarea.dispatchEvent(new Event('input'));
    expect(textarea.style.height).toBe('60px');

    scrollHeight = 40;
    fixture.componentInstance.text.set('два\nрядки');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(textarea.style.height).toBe('40px');
  });

  it('recalculates the height when the width of the field changes, but not when only its height does', async () => {
    let notify: (width: number) => void = () => undefined;
    vi.stubGlobal(
      'ResizeObserver',
      class {
        constructor(callback: ResizeObserverCallback) {
          notify = (width) => callback([{ contentRect: { width } } as ResizeObserverEntry], this as unknown as ResizeObserver);
        }
        observe(): void {}
        disconnect(): void {}
      },
    );
    try {
      const fixture = TestBed.createComponent(HostComponent);
      fixture.detectChanges();
      const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea')!;
      let scrollHeight = 20;
      Object.defineProperty(textarea, 'scrollHeight', { get: () => scrollHeight });
      await fixture.whenStable();
      notify(300);

      scrollHeight = 60;
      notify(300);
      expect(textarea.style.height).toBe('20px');

      notify(200);
      expect(textarea.style.height).toBe('60px');
    } finally {
      vi.unstubAllGlobals();
    }
  });
});
