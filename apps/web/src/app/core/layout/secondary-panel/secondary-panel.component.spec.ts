import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MenuController, provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { KbItemMenuComponent } from '../../knowledge-base/kb-item-menu/kb-item-menu.component';
import { KNOWLEDGE_BASE_SAMPLE_URL, KNOWLEDGE_BASE_SOURCE, KnowledgeBaseService } from '../../knowledge-base/knowledge-base.service';
import { LayoutSection } from '../layout.service';
import { COLLAPSED_FOLDERS_KEY, SecondaryPanelComponent } from './secondary-panel.component';

const menu = uk.settings.menu;

describe('SecondaryPanelComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [SecondaryPanelComponent],
      providers: [
        provideIonicAngular(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        // Зразковий зміст: тут перевіряється вигляд папок і сторінок (у таблиць їх нема).
        { provide: KNOWLEDGE_BASE_SOURCE, useValue: 'sample' },
      ],
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

  it('keeps the planner panel as it was, with «Головна» from the language file', () => {
    const element = render('planner');
    expect(element.querySelectorAll('a.tree__link')).toHaveLength(1);
    expect(element.querySelector('a.tree__link')?.textContent?.trim()).toBe(uk.common.home);
    expect(element.querySelector('.hint')).not.toBeNull();
  });

  describe('knowledge base section (the contents loaded by KnowledgeBaseService)', () => {
    const http = () => TestBed.inject(HttpTestingController);
    /** Чи є права на дії (`KnowledgeBaseService.can`); за замовчуванням — на всі. */
    let allowed: boolean;

    beforeEach(() => {
      allowed = true;
      vi.spyOn(TestBed.inject(KnowledgeBaseService), 'can').mockImplementation(() => allowed);
    });

    afterEach(() => vi.restoreAllMocks());

    async function renderLoaded(entries: object[]) {
      const fixture = TestBed.createComponent(SecondaryPanelComponent);
      fixture.componentRef.setInput('section', 'knowledge-base');
      fixture.detectChanges();
      http().expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush(entries);
      await fixture.whenStable();
      fixture.detectChanges();
      return fixture.nativeElement as HTMLElement;
    }

    it('asks for the contents and says it is loading until the answer', () => {
      const element = render('knowledge-base');
      expect(element.querySelector('.hint')?.textContent?.trim()).toBe(uk.knowledgeBase.loading);
      http().expectOne(KNOWLEDGE_BASE_SAMPLE_URL);
    });

    it('draws the home page added by the client as the link to the guide («Головна» from the language file), the folders and the materials', async () => {
      const element = await renderLoaded([
        { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
        { id: 't1', type: 'customtable', title: 'Таблиця' },
      ]);
      const home = element.querySelector<HTMLAnchorElement>('a.tree__link--home')!;
      expect(home.textContent?.trim()).toBe(uk.common.home);
      expect(home.getAttribute('href')).toBe('/knowledge-base');
      expect(element.querySelector('.tree__group-label')?.textContent?.trim()).toBe('Розділ');
      // Таблиця й сторінка — посилання на свій вид.
      const items = Array.from(element.querySelectorAll<HTMLAnchorElement>('.tree__row--item a.tree__link'));
      expect(items.map((item) => item.textContent?.trim())).toEqual(['Документ', 'Таблиця']);
      expect(items.map((item) => item.getAttribute('href'))).toEqual(['/knowledge-base/pages/p1', '/knowledge-base/tables/t1']);
      expect(items.map((item) => (item.querySelector('ion-icon') as unknown as { name: string }).name)).toEqual([
        'document-outline',
        'grid-outline',
      ]);
      expect(element.querySelector('.hint')).toBeNull();
    });

    it('gives every group «+» and three dots, every page three dots (not the home link), the full name in the title', async () => {
      const element = await renderLoaded([
        { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
        { id: 't1', type: 'customtable', title: 'Таблиця' },
      ]);
      expect(Array.from(element.querySelectorAll('.tree__menu')).map((button) => button.getAttribute('aria-label'))).toEqual([
        uk.knowledgeBase.addToFolder.replace('{{title}}', 'Розділ'),
        uk.knowledgeBase.groupActions.replace('{{title}}', 'Розділ'),
        uk.knowledgeBase.pageActions.replace('{{title}}', 'Документ'),
        uk.knowledgeBase.pageActions.replace('{{title}}', 'Таблиця'),
      ]);
      expect(element.querySelector('a.tree__link--home .tree__menu')).toBeNull();
      expect(Array.from(element.querySelectorAll('.tree__name')).map((name) => name.getAttribute('title'))).toEqual(['Розділ', 'Документ', 'Таблиця']);
    });

    it('closes the side menu (a curtain on a phone) when a material or the home page is chosen', async () => {
      const close = vi.spyOn(TestBed.inject(MenuController), 'close').mockResolvedValue(true);
      const element = await renderLoaded([{ id: 't1', type: 'customtable', title: 'Таблиця' }]);
      (element.querySelector('.tree__row--item a') as HTMLAnchorElement).click();
      (element.querySelector('a.tree__link--home') as HTMLAnchorElement).click();
      expect(close).toHaveBeenCalledTimes(2);
      expect(close).toHaveBeenCalledWith('secondary');
    });

    it('has no «+» and no three dots without the rights for the actions', async () => {
      allowed = false;
      const element = await renderLoaded([
        { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
        { id: 't1', type: 'customtable', title: 'Таблиця' },
      ]);
      expect(element.querySelectorAll('.tree__menu')).toHaveLength(0);
      expect(Array.from(element.querySelectorAll('.tree__name')).map((name) => name.getAttribute('title'))).toEqual(['Розділ', 'Документ', 'Таблиця']);
    });

    it('collapses a group by a click on its title and opens it by the next click, the folder turning into an arrow', async () => {
      const fixture = TestBed.createComponent(SecondaryPanelComponent);
      fixture.componentRef.setInput('section', 'knowledge-base');
      fixture.detectChanges();
      http().expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush([{ id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] }]);
      await fixture.whenStable();
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;
      const toggle = element.querySelector<HTMLButtonElement>('.tree__toggle')!;
      const pages = element.querySelector('.tree__group ul') as HTMLElement;
      const iconNames = () =>
        Array.from(toggle.querySelectorAll('ion-icon')).map((icon) => (icon as unknown as { name: string }).name);

      expect(toggle.getAttribute('aria-expanded')).toBe('true');
      expect(toggle.getAttribute('aria-controls')).toBe(pages.id);
      expect(pages.hidden).toBe(false);
      expect(iconNames()).toEqual(['folder-outline', 'chevron-down-outline']);

      toggle.click();
      fixture.detectChanges();
      expect(toggle.getAttribute('aria-expanded')).toBe('false');
      expect(pages.hidden).toBe(true);
      expect(getComputedStyle(pages).display).toBe('none');
      expect(iconNames()).toEqual(['folder-outline', 'chevron-forward-outline']);

      toggle.click();
      fixture.detectChanges();
      expect(toggle.getAttribute('aria-expanded')).toBe('true');
      expect(pages.hidden).toBe(false);
    });

    it('asks to add a page to the folder by its «+», passing the id and the title of the folder', async () => {
      const element = await renderLoaded([{ id: 'f1', type: 'folder', title: 'Розділ', items: [] }]);
      element.querySelector<HTMLButtonElement>('.tree__add')!.click();
      expect(TestBed.inject(KnowledgeBaseService).insertRequest()).toEqual({ folder: { id: 'f1', title: 'Розділ' } });
      // «+» не згортає папку.
      expect(element.querySelector('.tree__toggle')?.getAttribute('aria-expanded')).toBe('true');
    });

    it('opens the menu of a folder or a material by its three dots', async () => {
      const open = vi.spyOn(KbItemMenuComponent.prototype, 'open').mockImplementation(() => undefined);
      const element = await renderLoaded([
        { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
      ]);
      const dots = Array.from(element.querySelectorAll<HTMLButtonElement>('.tree__menu:not(.tree__add)'));
      dots.forEach((button) => button.click());
      expect(open.mock.calls.map(([, target]) => target.id)).toEqual(['f1', 'p1']);
      // Меню не згортає папку.
      expect(element.querySelector('.tree__toggle')?.getAttribute('aria-expanded')).toBe('true');
      vi.restoreAllMocks();
    });

    it('remembers the collapsed folders by their id after a reload, and forgets them when opened again', async () => {
      localStorage.removeItem(COLLAPSED_FOLDERS_KEY);
      const entries = [
        { id: 'f1', type: 'folder', title: 'Перша', items: [] },
        { id: 'f2', type: 'folder', title: 'Друга', items: [] },
      ];
      const element = await renderLoaded(entries);
      const toggles = () => Array.from(element.querySelectorAll<HTMLButtonElement>('.tree__toggle'));
      toggles()[1].click();
      expect(JSON.parse(localStorage.getItem(COLLAPSED_FOLDERS_KEY)!)).toEqual(['f2']);

      // Нова панель (як після перезавантаження) читає згорнуті папки зі сховища.
      const again = TestBed.createComponent(SecondaryPanelComponent);
      again.componentRef.setInput('section', 'knowledge-base');
      again.detectChanges();
      const states = Array.from((again.nativeElement as HTMLElement).querySelectorAll('.tree__toggle')).map((t) => t.getAttribute('aria-expanded'));
      expect(states).toEqual(['true', 'false']);

      toggles()[1].click();
      expect(localStorage.getItem(COLLAPSED_FOLDERS_KEY)).toBeNull();
    });

    it('says when the contents could not be loaded', async () => {
      vi.spyOn(console, 'warn').mockImplementation(() => undefined);
      const fixture = TestBed.createComponent(SecondaryPanelComponent);
      fixture.componentRef.setInput('section', 'knowledge-base');
      fixture.detectChanges();
      http().expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush({}, { status: 404, statusText: 'x' });
      await fixture.whenStable();
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).querySelector('.hint')?.textContent?.trim()).toBe(uk.knowledgeBase.error);
    });
  });
});
