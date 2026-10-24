import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ColleagueAction, ColleagueActionItem } from './colleague-actions.component';
import { ColleaguesPageModule } from '../colleagues.module';

@Component({
  template: `<app-colleague-actions [items]="items()" (choose)="chosen.push($event)" />`,
  imports: [ColleaguesPageModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly items = signal<readonly ColleagueActionItem[]>([]);
  readonly chosen: ColleagueAction[] = [];
}

describe('ColleagueActionsComponent', () => {
  const ITEMS: ColleagueActionItem[] = [
    { id: 'edit', labelKey: 'colleagues.actions.edit', icon: 'create-outline' },
    { id: 'delete', labelKey: 'colleagues.actions.delete', icon: 'trash-outline' },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render(items: ColleagueActionItem[]) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.items.set(items);
    fixture.detectChanges();
    return { fixture, host: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  it('draws one button per item, in the given order, with the text from the language file and the icon', () => {
    const { element } = render(ITEMS);
    const buttons = Array.from(element.querySelectorAll('button'));
    expect(buttons.map((b) => b.textContent?.trim())).toEqual([uk.colleagues.actions.edit, uk.colleagues.actions.delete]);
    // Іконка задається властивістю `name` елемента (прив'язка `[name]`).
    expect(buttons.map((b) => (b.querySelector('ion-icon') as unknown as { name: string }).name)).toEqual(['create-outline', 'trash-outline']);
  });

  it('is a menu: the list has the role menu and the buttons the role menuitem, with a tabindex', () => {
    const { element } = render(ITEMS);
    expect(element.querySelector('ul')?.getAttribute('role')).toBe('menu');
    for (const button of Array.from(element.querySelectorAll('button'))) {
      expect(button.getAttribute('role')).toBe('menuitem');
      expect(button.getAttribute('tabindex')).toBe('0');
    }
  });

  it('emits the id of the chosen item', () => {
    const { fixture, host, element } = render(ITEMS);
    (element.querySelectorAll('button')[1] as HTMLButtonElement).click();
    (element.querySelectorAll('button')[0] as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(host.chosen).toEqual(['delete', 'edit']);
  });

  it('follows the items when they change', () => {
    const { fixture, host, element } = render(ITEMS);
    host.items.set([ITEMS[0]]);
    fixture.detectChanges();
    expect(element.querySelectorAll('button')).toHaveLength(1);
  });
});
