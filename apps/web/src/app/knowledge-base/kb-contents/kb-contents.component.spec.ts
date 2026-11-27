import { Component, CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { KnowledgeBaseEntry } from '../../core/knowledge-base/knowledge-base';
import { KbItemMenuComponent } from '../../core/knowledge-base/kb-item-menu/kb-item-menu.component';
import { KnowledgeBaseService } from '../../core/knowledge-base/knowledge-base.service';
import { KbContentsComponent } from './kb-contents.component';

@Component({
  template: `<app-kb-contents [entries]="entries()" />`,
  imports: [KbContentsComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly entries = signal<readonly KnowledgeBaseEntry[]>([
    { id: 'h', type: 'home', title: '' },
    {
      id: 'f1',
      type: 'folder',
      title: 'Розділ',
      items: [
        { id: 'p1', type: 'page', title: 'Перша' },
        { id: 'p2', type: 'customtable', title: 'Друга' },
      ],
    },
    { id: 't1', type: 'customtable', title: 'Окрема' },
  ]);
}

describe('KbContentsComponent', () => {
  /** Чи є права на дії (`KnowledgeBaseService.can`); за замовчуванням — на всі. */
  let allowed: boolean;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    allowed = true;
    vi.spyOn(TestBed.inject(KnowledgeBaseService), 'can').mockImplementation(() => allowed);
  });

  afterEach(() => vi.restoreAllMocks());

  function render() {
    return renderWithHost().element;
  }
  function renderWithHost() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return { element: fixture.nativeElement as HTMLElement, host: fixture.componentInstance };
  }
  const text = (element: Element | null) => element?.textContent?.trim();
  const iconName = (element: Element) => (element.querySelector('.kb-contents__icon') as unknown as { name: string }).name;

  it('shows the title and the intro with the waving hand from the language file', () => {
    const element = render();
    expect(text(element.querySelector('h1.kb-contents__title'))).toBe(uk.knowledgeBase.guide);
    const intro = element.querySelector('.kb-contents__intro')!;
    expect(intro.textContent).toContain(uk.knowledgeBase.intro);
    expect(intro.querySelector('.kb-contents__wave')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('says it is empty yet, with the light and the dark picture, when there is nothing but the home page', () => {
    const { element, host } = renderWithHost();
    expect(element.querySelector('.kb-contents__empty')).toBeNull();
    host.entries.set([{ id: 'h', type: 'home', title: '' }]);
    TestBed.tick();
    const empty = element.querySelector('.kb-contents__empty')!;
    expect(text(empty.querySelector('.kb-contents__empty-text'))).toBe(uk.knowledgeBase.empty);
    expect(empty.querySelector('img')?.getAttribute('src')).toBe('assets/empty_light.svg');
    expect(empty.querySelector('source')?.getAttribute('srcset')).toBe('assets/empty_dark.svg');
    expect(empty.querySelector('source')?.getAttribute('media')).toBe('(prefers-color-scheme: dark)');
  });

  it('puts the groups into the grid in their order and all the materials without a folder into one column, without the home page', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.entries.update((entries) => [
      ...entries,
      { id: 'f2', type: 'folder', title: 'Порожній розділ', items: [] },
      { id: 'p3', type: 'page', title: 'Ще одна' },
    ]);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const blocks = Array.from(element.querySelector('.kb-contents__grid')!.children);
    const items = (block: Element) => Array.from(block.querySelectorAll('.kb-contents__item')).map((item) => text(item));
    expect(blocks).toHaveLength(3);
    expect(text(blocks[0].querySelector('h2'))).toBe('Розділ');
    expect(items(blocks[0])).toEqual(['Перша', 'Друга']);
    // Матеріали без папки — один блок на місці першого з них.
    expect(blocks[1].classList).toContain('kb-contents__items--loose');
    expect(items(blocks[1])).toEqual(['Окрема', 'Ще одна']);
    expect(text(blocks[2].querySelector('h2'))).toBe('Порожній розділ');
  });

  it('makes a table and a page links to their views (a folder title is not a link)', () => {
    const element = render();
    expect(Array.from(element.querySelectorAll('a.kb-contents__link')).map((link) => [link.textContent?.trim(), link.getAttribute('href')])).toEqual([
      ['Перша', '/knowledge-base/pages/p1'],
      ['Друга', '/knowledge-base/tables/p2'],
      ['Окрема', '/knowledge-base/tables/t1'],
    ]);
    expect(element.querySelector('h2 a')).toBeNull();
  });

  it('draws a page with document-outline and a table with grid-outline', () => {
    const items = Array.from(render().querySelectorAll('.kb-contents__item'));
    expect(items.map(iconName)).toEqual(['document-outline', 'grid-outline', 'grid-outline']);
  });

  it('gives every page and every group title a three-dot button named after it, opening the menu for it', () => {
    const open = vi.spyOn(KbItemMenuComponent.prototype, 'open').mockImplementation(() => undefined);
    const { element } = renderWithHost();
    const rows = Array.from(element.querySelectorAll('.kb-contents__row'));
    const dots = (row: Element) => row.querySelector<HTMLButtonElement>('.kb-contents__menu:not(.kb-contents__add)')!;
    expect(rows.map((row) => dots(row).getAttribute('aria-label'))).toEqual([
      uk.knowledgeBase.groupActions.replace('{{title}}', 'Розділ'),
      uk.knowledgeBase.pageActions.replace('{{title}}', 'Перша'),
      uk.knowledgeBase.pageActions.replace('{{title}}', 'Друга'),
      uk.knowledgeBase.pageActions.replace('{{title}}', 'Окрема'),
    ]);
    for (const row of rows) {
      const button = dots(row);
      expect(button.getAttribute('tabindex')).toBe('0');
      expect((button.querySelector('ion-icon') as unknown as { name: string }).name).toBe('ellipsis-horizontal');
    }
    dots(rows[0]).click();
    dots(rows[3]).click();
    expect(open.mock.calls.map(([, target]) => target.id)).toEqual(['f1', 't1']);
    vi.restoreAllMocks();
  });

  it('shows the folder title as always hovered, with «+» that asks to add a page to the folder (pages have no «+»)', () => {
    const element = render();
    const folder = element.querySelector('h2.kb-contents__row')!;
    expect(folder.classList).toContain('kb-contents__row--folder');
    const add = folder.querySelector<HTMLButtonElement>('.kb-contents__add')!;
    expect(add.getAttribute('aria-label')).toBe(uk.knowledgeBase.addToFolder.replace('{{title}}', 'Розділ'));
    expect(element.querySelectorAll('.kb-contents__add')).toHaveLength(1);

    add.click();
    expect(TestBed.inject(KnowledgeBaseService).insertRequest()).toEqual({ folder: { id: 'f1', title: 'Розділ' } });
  });

  it('has no «+» and no three dots without the rights for the actions', () => {
    allowed = false;
    const element = render();
    expect(element.querySelectorAll('.kb-contents__menu')).toHaveLength(0);
  });

  it('keeps the full name in the title', () => {
    const names = Array.from(render().querySelectorAll('.kb-contents__name'));
    expect(names.map((name) => name.getAttribute('title'))).toEqual(['Розділ', 'Перша', 'Друга', 'Окрема']);
  });
});
