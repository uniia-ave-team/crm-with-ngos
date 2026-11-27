import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import { ActionBarComponent } from './action-bar.component';
import { ActionBarService } from './action-bar.service';

describe('ActionBarComponent', () => {
  function render() {
    TestBed.configureTestingModule({ providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', { bar: 'Дії', a: 'Перша', b: 'Друга', c: 'Третя' });
    const fixture = TestBed.createComponent(ActionBarComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement, service: TestBed.inject(ActionBarService) };
  }

  it('shows the content given to it in the flow of a form (inline), not the one of the service', () => {
    const { fixture, element, service } = render();
    service.show({}, { labelKey: 'bar', groups: [[{ id: 'a', labelKey: 'a', icon: 'copy-outline' }]], choose: () => undefined });
    fixture.componentRef.setInput('inline', true);
    fixture.componentRef.setInput('content', { labelKey: 'bar', groups: [[{ id: 'c', labelKey: 'c', icon: 'trash-outline' }]], choose: () => undefined });
    fixture.detectChanges();
    expect(element.classList).toContain('action-bar--inline');
    expect(Array.from(element.querySelectorAll('button')).map((button) => button.getAttribute('aria-label'))).toEqual(['Третя']);
  });

  it('is not there without content', () => {
    const { element } = render();
    expect(element.classList).not.toContain('action-bar--shown');
    expect(element.querySelector('.action-bar')).toBeNull();
  });

  it('shows the groups of icon buttons with their names in the hint and for screen readers, dividers between the groups', () => {
    const { fixture, element, service } = render();
    const chosen: string[] = [];
    const owner = {};
    service.show(owner, {
      labelKey: 'bar',
      groups: [
        [
          { id: 'a', labelKey: 'a', icon: 'copy-outline' },
          { id: 'b', labelKey: 'b', icon: 'eraser-outline', disabled: true },
        ],
        [{ id: 'c', labelKey: 'c', icon: 'trash-outline' }],
      ],
      choose: (id) => chosen.push(id),
    });
    fixture.detectChanges();
    expect(element.classList).toContain('action-bar--shown');
    expect(element.querySelector('[role="toolbar"]')?.getAttribute('aria-label')).toBe('Дії');
    expect(Array.from(element.querySelector('.action-bar')!.children).map((child) => child.classList[0])).toEqual([
      'action-bar__group',
      'action-bar__divider',
      'action-bar__group',
    ]);
    const buttons = Array.from(element.querySelectorAll<HTMLButtonElement>('.action-bar__button'));
    expect(buttons.map((button) => [button.getAttribute('aria-label'), button.getAttribute('title'), button.disabled])).toEqual([
      ['Перша', 'Перша', false],
      ['Друга', 'Друга', true],
      ['Третя', 'Третя', false],
    ]);
    buttons[2].click();
    expect(chosen).toEqual(['c']);

    // Ховає лише той, хто показав.
    service.hide({});
    expect(service.content()).not.toBeNull();
    service.hide(owner);
    expect(service.content()).toBeNull();
  });

  it('marks the divider in the middle of the groups: on a phone it breaks the bar into two even rows', () => {
    const { fixture, element, service } = render();
    const group = (id: string) => [{ id, labelKey: id, icon: 'copy-outline' }];
    service.show({}, { labelKey: 'bar', groups: [group('a'), group('b'), group('c'), group('d')], choose: () => undefined });
    fixture.detectChanges();
    const dividers = Array.from(element.querySelectorAll('.action-bar__divider'));
    expect(dividers.map((divider) => divider.classList.contains('action-bar__divider--middle'))).toEqual([false, true, false]);
  });

  it('does not take the focus away from the chosen field when pressed', () => {
    const { element } = render();
    const event = new Event('pointerdown', { cancelable: true });
    element.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
  });
});
