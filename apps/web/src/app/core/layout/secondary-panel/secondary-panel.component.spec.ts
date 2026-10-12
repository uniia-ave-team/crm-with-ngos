import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { LayoutModule } from '../layout.module';
import { LayoutSection } from '../layout.service';
import { SecondaryPanelComponent } from './secondary-panel.component';

const menu = uk.settings.menu;

describe('SecondaryPanelComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [IonicModule.forRoot(), LayoutModule],
      providers: [provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    }).compileComponents();
    // Тексти живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render(section: LayoutSection) {
    const fixture = TestBed.createComponent(SecondaryPanelComponent);
    fixture.componentRef.setInput('section', section);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  describe('settings section', () => {
    it('lists the personal data, password and notifications items from the language file, in that order', () => {
      const element = render('settings');
      const labels = Array.from(element.querySelectorAll('.tree__link span:last-child')).map((s) => s.textContent?.trim());
      expect(labels).toEqual([menu.personal, menu.password, menu.notifications]);
    });

    it('gives every item an icon', () => {
      const element = render('settings');
      // `[name]` — властивість елемента (не атрибут), тож читаємо її.
      const icons = Array.from(element.querySelectorAll('.tree__link ion-icon')).map((i) => (i as unknown as { name: string }).name);
      expect(icons).toEqual(['person-outline', 'key-outline', 'notifications-outline']);
    });

    it('hides the notifications item under the planned class, leaving the other two visible', () => {
      const element = render('settings');
      const items = Array.from(element.querySelectorAll('ul.tree > li'));
      expect(items.map((li) => li.classList.contains('planned'))).toEqual([false, false, true]);
      expect(items[2].textContent).toContain(menu.notifications);
    });

    it('makes the items links to the tabs of the settings page', () => {
      const element = render('settings');
      const links = Array.from(element.querySelectorAll<HTMLAnchorElement>('a.tree__link'));
      expect(links.map((a) => a.getAttribute('href'))).toEqual([
        '/settings/personal-data',
        '/settings/password',
        '/settings/notifications',
      ]);
    });

    it('does not hard-code the texts: they follow the language file', () => {
      TestBed.inject(TranslateService).setTranslation('uk', {
        settings: { menu: { personal: 'P', password: 'Q', notifications: 'N' } },
      });
      const element = render('settings');
      const labels = Array.from(element.querySelectorAll('.tree__link span:last-child')).map((s) => s.textContent?.trim());
      expect(labels).toEqual(['P', 'Q', 'N']);
    });
  });

  it.each(['settings', 'planner', 'knowledge-base'] as const)('puts 8px between the items on every page (%s)', (section) => {
    const element = render(section);
    const list = element.querySelector('ul.tree') as HTMLElement;
    const style = getComputedStyle(list);
    expect(style.display).toBe('flex');
    expect(style.flexDirection).toBe('column');
    expect(style.rowGap || style.gap).toBe('8px');
  });

  it('keeps the planner panel as it was', () => {
    const element = render('planner');
    expect(element.querySelectorAll('a.tree__link')).toHaveLength(1);
    expect(element.querySelector('.hint')).not.toBeNull();
  });
});
