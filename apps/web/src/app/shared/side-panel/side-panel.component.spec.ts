import { Component, signal } from '@angular/core';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { SidePanelComponent } from './side-panel.component';
import { SidePanelModule } from './side-panel.module';

@Component({
  template: `
    <button id="opener" type="button">open</button>
    <app-side-panel #panel [width]="width()" [label]="label()" (opened)="events.push('opened')" (closed)="events.push('closed')">
      <p class="content">зміст</p>
    </app-side-panel>
  `,
  imports: [SidePanelModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly width = signal('280px');
  readonly label = signal<string | null>('Назва вікна');
  readonly events: string[] = [];
}

describe('SidePanelComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const panel = fixture.debugElement.children.find((c) => c.componentInstance instanceof SidePanelComponent)!
      .componentInstance as SidePanelComponent;
    const aside = () => element.querySelector('aside.side-panel') as HTMLElement;
    return { fixture, host: fixture.componentInstance, element, panel, aside, opener: element.querySelector<HTMLButtonElement>('#opener')! };
  }

  it('is closed at first: out of the way, and not reachable for the focus and the screen readers (inert)', () => {
    const { panel, aside } = render();
    expect(panel.isOpen()).toBe(false);
    expect(aside().classList).not.toContain('side-panel--open');
    expect(aside().hasAttribute('inert')).toBe(true);
  });

  it('opens by open(): marked open, no longer inert, the event "opened" is emitted once', () => {
    const { fixture, host, panel, aside } = render();
    panel.open();
    panel.open();
    fixture.detectChanges();

    expect(panel.isOpen()).toBe(true);
    expect(aside().classList).toContain('side-panel--open');
    expect(aside().hasAttribute('inert')).toBe(false);
    expect(host.events).toEqual(['opened']);
  });

  it('shows the content that was put into it, also while it is closed (it is not recreated)', () => {
    const { fixture, element, panel } = render();
    const before = element.querySelector('.content');
    expect(before?.textContent).toBe('зміст');
    panel.open();
    fixture.detectChanges();
    expect(element.querySelector('.content')).toBe(before);
  });

  it('is a dialog with the given name', () => {
    const { aside } = render();
    expect(aside().getAttribute('role')).toBe('dialog');
    expect(aside().getAttribute('aria-label')).toBe('Назва вікна');
  });

  describe('the close button', () => {
    it('is the cross in the corner, with a label from the language file and a tabindex', () => {
      const { element } = render();
      const button = element.querySelector('.side-panel__close') as HTMLButtonElement;
      expect(button.getAttribute('aria-label')).toBe(uk.common.close);
      expect(button.getAttribute('tabindex')).toBe('0');
      expect((button.querySelector('ion-icon') as unknown as { name: string }).name).toBe('close-outline');
      expect(element.querySelector('.side-panel__header')?.contains(button)).toBe(true);
    });

    it('closes the panel and emits "closed"', () => {
      const { fixture, host, element, panel, aside } = render();
      panel.open();
      fixture.detectChanges();

      (element.querySelector('.side-panel__close') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(panel.isOpen()).toBe(false);
      expect(aside().classList).not.toContain('side-panel--open');
      expect(aside().hasAttribute('inert')).toBe(true);
      expect(host.events).toEqual(['opened', 'closed']);
    });
  });

  describe('Escape', () => {
    it('closes an open panel', () => {
      const { fixture, panel } = render();
      panel.open();
      fixture.detectChanges();

      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      fixture.detectChanges();
      expect(panel.isOpen()).toBe(false);
    });

    it('does nothing when the panel is closed (no "closed" event)', () => {
      const { host } = render();
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      expect(host.events).toEqual([]);
    });
  });

  describe('the focus', () => {
    it('goes to the close button when the panel opens, and back to the element that had it when the panel closes', async () => {
      const { fixture, element, panel, opener } = render();
      document.body.appendChild(element);
      opener.focus();
      expect(document.activeElement).toBe(opener);

      panel.open();
      fixture.detectChanges();
      await vi.waitFor(() => expect(document.activeElement).toBe(element.querySelector('.side-panel__close')));

      panel.close();
      fixture.detectChanges();
      expect(document.activeElement).toBe(opener);
      element.remove();
    });
  });

  it('takes the width of the desktop from the input (280px, like the second side menu, by default)', () => {
    const { fixture, host, aside } = render();
    expect(aside().style.getPropertyValue('--side-panel-width')).toBe('280px');

    host.width.set('420px');
    fixture.detectChanges();
    expect(aside().style.getPropertyValue('--side-panel-width')).toBe('420px');
  });

  it('can be closed when it is already closed: nothing happens', () => {
    const { host, panel } = render();
    panel.close();
    expect(host.events).toEqual([]);
  });
});
