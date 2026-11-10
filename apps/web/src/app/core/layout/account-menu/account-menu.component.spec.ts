import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslatePipe, provideTranslateService } from '@ngx-translate/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { AlphaOutlineModule } from '../../../shared/alpha-outline/alpha-outline.module';
import { AvatarModule } from '../../../shared/avatar/avatar.module';
import { AccountMenuComponent, AccountTile } from './account-menu.component';

function accounts(count: number): AccountTile[] {
  return Array.from({ length: count }, (_, id) => ({ id, firstName: 'Перший', lastName: 'Другий', avatarUrl: null }));
}

describe('AccountMenuComponent', () => {
  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      declarations: [AccountMenuComponent],
      imports: [AvatarModule, AlphaOutlineModule, TranslatePipe],
      // Компонент тут оголошено напряму в TestBed, і його директиви в області видимості тесту
      // не діють: `[appProtectedSrc]` лягає звичайною властивістю елемента (сама директива —
      // у власному спеку); без NO_ERRORS_SCHEMA привʼязка до `img` була б помилкою.
      schemas: [NO_ERRORS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.restoreAllMocks());

  function render(count: number) {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', accounts(count));
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element };
  }

  it.each([0, 1, 3, 5, 6, 9, 10, 11])(
    'puts %i accounts and then the add button into one flex container, with no row wrappers',
    (count) => {
      const { element } = render(count);
      const container = element.querySelector('.account-menu__accounts')!;
      const kinds = Array.from(container.children).map((child) => child.className.split(' ')[0]);
      expect(kinds).toEqual([...Array(count).fill('account-tile'), 'account-add']);
    },
  );

  it('shows no logo badge at all when an account has no logoUrl', () => {
    const { element } = render(3);
    expect(element.querySelectorAll('.account-tile__badge')).toHaveLength(0);
    expect(element.querySelectorAll('.account-tile img')).toHaveLength(0);
  });

  function statusOf(tile: AccountTile): { tile: HTMLElement; icon: Element | null } {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [tile]);
    fixture.detectChanges();
    const element = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.account-tile')!;
    return { tile: element, icon: element.querySelector('.account-tile__status') };
  }

  const base: AccountTile = { id: 'a', firstName: 'Перший', lastName: 'Другий', avatarUrl: null };

  it('shows no status icon and no extra label for a valid account', () => {
    const { tile, icon } = statusOf(base);
    expect(icon).toBeNull();
    expect(tile.classList).not.toContain('outdated');
    expect(tile.classList).not.toContain('unavailable');
    expect(tile.getAttribute('title')).toBeNull();
  });

  it.each([
    ['outdated (the tokens were rejected)', { outdated: true }, 'alert-circle', 'account-tile__status--outdated'],
    ['unavailable (the server did not answer)', { unavailable: true }, 'sync-circle', 'account-tile__status--unavailable'],
    ['outdated and unavailable', { outdated: true, unavailable: true }, 'alert-circle', 'account-tile__status--both'],
  ])('draws the icon for an account that is %s', (_state, flags, name, cssClass) => {
    const { tile, icon } = statusOf({ ...base, ...flags });
    expect(icon!.getAttribute('name')).toBe(name);
    expect(icon!.classList).toContain(cssClass);
    // Значок — лише візуальний: статус для допоміжних технологій у підписі плитки.
    expect(icon!.getAttribute('aria-hidden')).toBe('true');
    expect(tile.getAttribute('title')).toBe(tile.getAttribute('aria-label'));
  });

  it('labels an account whose session ended differently from one whose server is down, and from a valid one', () => {
    const valid = statusOf(base).tile.getAttribute('aria-label');
    const outdated = statusOf({ ...base, outdated: true }).tile.getAttribute('aria-label');
    const unavailable = statusOf({ ...base, unavailable: true }).tile.getAttribute('aria-label');
    expect(new Set([valid, outdated, unavailable]).size).toBe(3);
    // Сервер не відповів — той самий підпис, чи токени відхилено, чи ні.
    expect(statusOf({ ...base, outdated: true, unavailable: true }).tile.getAttribute('aria-label')).toBe(unavailable);
  });

  it('has the settings item, a divider and the sign-out item, in that order', () => {
    const { element } = render(1);
    const parts = Array.from(element.querySelector('.account-menu')!.children).map((c) => c.className);
    expect(parts).toEqual(['account-menu__accounts', 'account-menu__item', 'account-menu__divider', 'account-menu__item']);
    const icons = Array.from(element.querySelectorAll('.account-menu__item ion-icon')).map((i) => i.getAttribute('name'));
    expect(icons).toEqual(['cog-outline', 'log-out-outline']);
  });

  it('reports the clicks to the parent', () => {
    const { fixture, element } = render(2);
    const events: string[] = [];
    const component = fixture.componentInstance;
    component.settings.subscribe(() => events.push('settings'));
    component.signOut.subscribe(() => events.push('signOut'));
    component.addAccount.subscribe(() => events.push('add'));
    component.selectAccount.subscribe((a) => events.push(`select:${a.id}`));

    element.querySelectorAll<HTMLElement>('.account-tile')[1].click();
    element.querySelector<HTMLElement>('.account-add')!.click();
    element.querySelectorAll<HTMLElement>('.account-menu__item')[0].click();
    element.querySelectorAll<HTMLElement>('.account-menu__item')[1].click();
    expect(events).toEqual(['select:1', 'add', 'settings', 'signOut']);
  });

  it('marks the NGO logo for the alpha-channel outline, with no box-based border around it', () => {
    // Сам контур малює AlphaOutlineDirective (перевіряється в її спеку й у браузері);
    // тут — що логотип ним позначений.
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [{ ...accounts(1)[0], logoUrl: 'assets/custom.png' }]);
    fixture.detectChanges();
    const logos = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLImageElement>('.account-tile__badge img'));
    expect(logos).toHaveLength(1);
    expect(logos[0].hasAttribute('appalphaoutline')).toBe(true);
    expect(logos[0].getAttribute('outlinecolor')).toBe('var(--nav-avatar-bg)');
  });

  it('gives the active account the active class (the white ring is drawn by it) and no other tile', () => {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', accounts(3).map((a, i) => ({ ...a, active: i === 0 })));
    fixture.detectChanges();
    const tiles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.account-tile'));
    expect(tiles.map((t) => t.classList.contains('active'))).toEqual([true, false, false]);
  });

  it.each([[null], [undefined], ['']])('shows the logo of the account only when logoUrl is set (logoUrl: %s)', (empty) => {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [
      { id: 0, firstName: 'Перший', lastName: 'Другий', avatarUrl: null, logoUrl: empty },
      { id: 1, firstName: 'Перший', lastName: 'Другий', avatarUrl: null, logoUrl: 'assets/custom.png' },
    ]);
    fixture.detectChanges();
    const tiles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.account-tile'));
    expect(tiles[0].querySelector('.account-tile__badge')).toBeNull();
    // Адреса логотипа лягає в `appProtectedSrc` (у `src` її кладе директива, яка бере файл із токеном).
    expect(
      Array.from(tiles[1].querySelectorAll('.account-tile__badge img')).map((i) => (i as unknown as { appProtectedSrc: string }).appProtectedSrc),
    ).toEqual(['assets/custom.png']);
  });
});
