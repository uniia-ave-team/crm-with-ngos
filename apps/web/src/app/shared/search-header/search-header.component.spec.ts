import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ActionButtonComponent } from '../action-button/action-button.component';
import { SearchHeaderComponent } from './search-header.component';

@Component({
  template: `
    <app-search-header title="Заголовок" placeholder="Підказка" [maxLength]="20" (searchChange)="searches.push($event)">
      <app-action-button icon="add-outline" label="Дія" />
    </app-search-header>
  `,
  imports: [SearchHeaderComponent, ActionButtonComponent],
})
class HostComponent {
  readonly searches: string[] = [];
}

describe('SearchHeaderComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const toggle = () => element.querySelector<HTMLButtonElement>('.search-header__toggle')!;
    const input = () => element.querySelector<HTMLInputElement>('.search-header__input')!;
    const isOpen = () => element.querySelector('.search-header')!.classList.contains('search-header--open');
    const open = () => {
      toggle().click();
      fixture.detectChanges();
    };
    const type = (text: string) => {
      input().value = text;
      input().dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    return { fixture, host: fixture.componentInstance, element, toggle, input, isOpen, open, type };
  }

  it('keeps one row: the title, the buttons, the place of the field and the magnifier, in this order', () => {
    const { element } = render();
    const row = element.querySelector('.search-header')!;
    expect(Array.from(row.children).map((child) => child.className.split(' ')[0])).toEqual([
      'search-header__title-slot',
      'search-header__actions',
      'search-header__field-slot',
      'search-header__toggle',
    ]);
    expect(element.querySelector('.search-header__title')?.textContent?.trim()).toBe('Заголовок');
    expect(element.querySelector('.search-header__actions app-action-button')).not.toBeNull();
  });

  it('is closed at first: the field takes no place and cannot be reached, the magnifier opens it', () => {
    const { element, toggle, isOpen } = render();
    expect(isOpen()).toBe(false);
    expect(element.querySelector('.search-header__field-slot')?.hasAttribute('inert')).toBe(true);
    expect(element.querySelector('.search-header__title-slot')?.hasAttribute('inert')).toBe(false);
    expect(toggle().getAttribute('aria-label')).toBe(uk.common.search);
    expect(toggle().getAttribute('aria-expanded')).toBe('false');
  });

  it('opens: the title goes away, the field gets the cursor, the magnifier becomes the cross', async () => {
    const { element, toggle, input, isOpen, open } = render();
    open();
    expect(isOpen()).toBe(true);
    expect(element.querySelector('.search-header__title-slot')?.hasAttribute('inert')).toBe(true);
    expect(element.querySelector('.search-header__field-slot')?.hasAttribute('inert')).toBe(false);
    expect(input().getAttribute('aria-label')).toBe(uk.common.search);
    expect(input().placeholder).toBe('Підказка');
    expect(input().getAttribute('maxlength')).toBe('20');
    await vi.waitFor(() => expect(document.activeElement).toBe(input()));
    expect(toggle().getAttribute('aria-label')).toBe(uk.common.searchClose);
    expect(toggle().getAttribute('aria-expanded')).toBe('true');
    expect(toggle().querySelector('ion-icon')?.getAttribute('name')).toBe('close-outline');
  });

  it('gives every change of the field as it is, spaces included', () => {
    const { host, open, type } = render();
    open();
    type('  ab ');
    type('');
    expect(host.searches).toEqual(['  ab ', '']);
  });

  it.each([
    ['the cross', (ctx: ReturnType<typeof render>) => ctx.toggle().click()],
    ['Esc in the field', (ctx: ReturnType<typeof render>) => ctx.input().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))],
  ])('closes by %s: the field is emptied (an empty search is given), the focus goes to the magnifier', async (_name, close) => {
    const ctx = render();
    ctx.open();
    ctx.type('ab');
    close(ctx);
    ctx.fixture.detectChanges();
    expect(ctx.isOpen()).toBe(false);
    expect(ctx.input().value).toBe('');
    expect(ctx.host.searches).toEqual(['ab', '']);
    expect(ctx.toggle().getAttribute('aria-label')).toBe(uk.common.search);
    await vi.waitFor(() => expect(document.activeElement).toBe(ctx.toggle()));
  });

  it('gives nothing when it is closed with an empty field', () => {
    const { host, open, toggle, fixture } = render();
    open();
    toggle().click();
    fixture.detectChanges();
    expect(host.searches).toEqual([]);
  });

  it('marks its own element while the search is open (for the page to hide something then)', () => {
    const { element, open } = render();
    const header = element.querySelector('app-search-header')!;
    expect(header.classList).not.toContain('search-header-host--open');
    open();
    expect(header.classList).toContain('search-header-host--open');
  });

  it('keeps the width of the title while the search is open and until the place of the title has grown back', () => {
    const { element, open, toggle, fixture } = render();
    const title = element.querySelector<HTMLElement>('.search-header__title')!;
    vi.spyOn(title, 'getBoundingClientRect').mockReturnValue({ width: 240 } as DOMRect);
    expect(title.style.width).toBe('');
    open();
    expect(title.style.width).toBe('240px');

    toggle().click();
    fixture.detectChanges();
    // Закриття ще анімується: ширина та сама, текст не перескладається.
    expect(title.style.width).toBe('240px');
    const slot = element.querySelector('.search-header__title-slot')!;
    slot.dispatchEvent(Object.assign(new Event('transitionend'), { propertyName: 'flex-grow' }));
    fixture.detectChanges();
    expect(title.style.width).toBe('');
  });

  describe('the title', () => {
    @Component({
      template: `
        <app-search-header title="Довга назва" [titleLines]="lines" />
      `,
      imports: [SearchHeaderComponent],
    })
    class TitleHostComponent {
      lines = 1;
    }

    function renderTitle(lines: number) {
      const fixture = TestBed.createComponent(TitleHostComponent);
      fixture.componentInstance.lines = lines;
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;
      return { element, title: element.querySelector<HTMLElement>('.search-header__title')! };
    }

    it('keeps one line by default and gives the full title in the hint', () => {
      const { title } = renderTitle(1);
      expect(title.classList).not.toContain('search-header__title--clamped');
      expect(title.getAttribute('title')).toBe('Довга назва');
    });

    it('wraps onto as many lines as asked', () => {
      const { title } = renderTitle(2);
      expect(title.classList).toContain('search-header__title--clamped');
      expect(title.style.getPropertyValue('--title-lines')).toBe('2');
    });
  });
});
