import { Component, CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ModalComponent } from './modal.component';
import { ModalModule } from './modal.module';

@Component({
  template: `
    <button id="opener" type="button" (click)="dialog.open()">open</button>
    <app-modal #dialog [width]="width()" [label]="label()" [dismissOnBackdrop]="dismissOnBackdrop()" (opened)="events.push('opened')" (closed)="events.push('closed')">
      <p class="content">зміст</p>
    </app-modal>
  `,
  imports: [ModalModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly width = signal<string | null>(null);
  readonly label = signal<string | null>('Підтвердження');
  readonly dismissOnBackdrop = signal(true);
  readonly events: string[] = [];
}

describe('ModalComponent', () => {
  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const modal = fixture.debugElement.children.find((c) => c.componentInstance instanceof ModalComponent)!.componentInstance as ModalComponent;
    const ion = () => element.querySelector('ion-modal') as unknown as HTMLElement & Record<string, unknown>;
    return { fixture, host: fixture.componentInstance, element, modal, ion, opener: element.querySelector<HTMLButtonElement>('#opener')! };
  }

  it('is closed at first and opens by open(), with no anchor needed', () => {
    const { fixture, modal, ion, opener } = render();
    expect(modal.isOpen()).toBe(false);
    expect(ion()['isOpen']).toBe(false);

    opener.click();
    fixture.detectChanges();
    expect(modal.isOpen()).toBe(true);
    expect(ion()['isOpen']).toBe(true);
  });

  it('is built on ion-modal: it covers the whole screen with its own backdrop, above the menus', () => {
    const { ion } = render();
    expect(ion().tagName.toLowerCase()).toBe('ion-modal');
    expect(ion().classList).toContain('app-modal');
  });

  it('reports that it opened when ion-modal has presented it', () => {
    const { fixture, host, ion } = render();
    ion().dispatchEvent(new CustomEvent('didPresent'));
    fixture.detectChanges();
    expect(host.events).toEqual(['opened']);
  });

  it('closes by close() and by the ion-modal dismissal (the backdrop, Escape), and reports it', () => {
    const { fixture, host, modal, ion, opener } = render();
    opener.click();
    fixture.detectChanges();
    modal.close();
    fixture.detectChanges();
    expect(modal.isOpen()).toBe(false);
    expect(ion()['isOpen']).toBe(false);

    opener.click();
    fixture.detectChanges();
    ion().dispatchEvent(new CustomEvent('didDismiss'));
    fixture.detectChanges();
    expect(modal.isOpen()).toBe(false);
    expect(host.events).toEqual(['closed']);
  });

  it('resolves close() only after ion-modal has been dismissed, and at once when it is not open', async () => {
    const { fixture, modal, ion, opener } = render();
    await expect(modal.close()).resolves.toBeUndefined();

    opener.click();
    fixture.detectChanges();
    let done = false;
    const closing = modal.close().then(() => (done = true));
    await Promise.resolve();
    expect(done).toBe(false);

    // Елемент зʼявляється асинхронно; під навантаженням (весь набір тестів) це не завжди одразу.
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(ion()).not.toBeNull();
    });
    ion().dispatchEvent(new CustomEvent('didDismiss'));
    await closing;
    expect(done).toBe(true);
  });

  it('stops waiting for the closing animation after a timeout', async () => {
    vi.useFakeTimers();
    try {
      const { fixture, modal, opener } = render();
      opener.click();
      fixture.detectChanges();
      let done = false;
      void modal.close().then(() => (done = true));
      await vi.advanceTimersByTimeAsync(1000);
      expect(done).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it('can be opened again after it was closed', () => {
    const { fixture, modal, ion, opener } = render();
    opener.click();
    fixture.detectChanges();
    ion().dispatchEvent(new CustomEvent('didDismiss'));
    fixture.detectChanges();
    opener.click();
    fixture.detectChanges();
    expect(modal.isOpen()).toBe(true);
    expect(ion()['isOpen']).toBe(true);
  });

  describe('closing by the click on the backdrop', () => {
    it('is on by default and can be turned off', () => {
      const { fixture, host, ion } = render();
      expect(ion()['backdropDismiss']).toBe(true);
      host.dismissOnBackdrop.set(false);
      fixture.detectChanges();
      expect(ion()['backdropDismiss']).toBe(false);
    });
  });

  describe('the width', () => {
    const widthOf = (ion: HTMLElement) => ion.style.getPropertyValue('--app-modal-width');

    it('is by the content when none is given', () => {
      const { ion } = render();
      expect(widthOf(ion())).toBe('fit-content');
    });

    it('is the given one, and follows it when it changes', () => {
      const { fixture, host, ion } = render();
      host.width.set('360px');
      fixture.detectChanges();
      expect(widthOf(ion())).toBe('360px');
      host.width.set('min(420px, calc(100vw - 32px))');
      fixture.detectChanges();
      expect(widthOf(ion())).toBe('min(420px, calc(100vw - 32px))');
    });

    it('is kept in its own variable, so that Ionic stripping the inline --width on closing does not lose it', () => {
      const { fixture, host, ion } = render();
      host.width.set('300px');
      fixture.detectChanges();
      ion().style.removeProperty('--width');
      fixture.detectChanges();
      expect(widthOf(ion())).toBe('300px');
    });
  });

  it('has the padding of 18px at the top and on the sides and 26px at the bottom around the content', () => {
    const { fixture, opener } = render();
    opener.click();
    fixture.detectChanges();
    // Зміст у ng-template з'являється лише з Ionic, тож перевіряємо правило стилів компонента.
    const css = Array.from(document.querySelectorAll('style')).map((style) => style.textContent ?? '').join('\n');
    expect(css).toMatch(/\.app-modal__content[^{]*\{[^}]*padding:\s*18px 18px 26px/);
  });

  it('gives the dialog the name for the assistive technologies, and takes it away with none', () => {
    const { fixture, host, ion } = render();
    expect(ion().getAttribute('aria-label')).toBe('Підтвердження');
    host.label.set(null);
    fixture.detectChanges();
    expect(ion().hasAttribute('aria-label')).toBe(false);
  });
});
