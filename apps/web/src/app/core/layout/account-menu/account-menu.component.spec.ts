import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslatePipe, TranslateService, provideTranslateService } from '@ngx-translate/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import uk from '../../../../assets/i18n/uk.json';
import { AccountMenuComponent, AccountTile } from './account-menu.component';

function accounts(count: number): AccountTile[] {
  return Array.from({ length: count }, (_, id) => ({ id, firstName: 'Перший', lastName: 'Другий', avatarUrl: null }));
}

describe('AccountMenuComponent', () => {
  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      declarations: [AccountMenuComponent],
      imports: [TranslatePipe],
      // Компонент тут оголошено напряму в TestBed, і його директиви в області видимості тесту
      // не діють: `[appProtectedSrc]` лягає звичайною властивістю елемента (сама директива —
      // у власному спеку); без NO_ERRORS_SCHEMA привʼязка до `img` була б помилкою.
      schemas: [NO_ERRORS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }), provideHttpClient(), provideHttpClientTesting()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('uk', uk);
    translate.use('uk');
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

  function statusOf(tile: AccountTile): { tile: HTMLElement } {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [tile]);
    fixture.detectChanges();
    return { tile: (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.account-tile')! };
  }

  const base: AccountTile = { id: 'a', firstName: 'Перший', lastName: 'Другий', avatarUrl: null };

  describe('the tooltip of a tile (the title attribute that the browser shows on hover)', () => {
    const named: AccountTile = { ...base, patronymic: 'Третій', ngoName: 'Організація' };
    const titleOf = (tile: AccountTile) => statusOf(tile).tile.getAttribute('title');

    it('has the full name, a dash and the NGO name in the first line, and no second line for a valid account', () => {
      expect(titleOf(named)).toBe('Другий Перший Третій — Організація');
    });

    it.each([
      ['the session ended (outdated)', { outdated: true }, uk.nav.account.outdated],
      ['the server did not answer (unavailable)', { unavailable: true }, uk.nav.account.unavailable],
      ['both, where the server wins', { outdated: true, unavailable: true }, uk.nav.account.unavailable],
    ])('adds the second line when %s', (_state, flags, status) => {
      expect(titleOf({ ...named, ...flags })).toBe(`Другий Перший Третій — Організація\n${status}`);
    });

    it('uses the texts from the translations', () => {
      expect(uk.nav.account.outdated).toBe('Сеанс закінчився');
      expect(uk.nav.account.unavailable).toBe('Відсутнє підключення до серверу');
    });

    it('leaves out what the account does not have: the patronymic, the NGO, or the whole name', () => {
      expect(titleOf({ ...base, ngoName: 'Організація' })).toBe('Другий Перший — Організація');
      expect(titleOf({ ...base, patronymic: ' ', ngoName: null })).toBe('Другий Перший');
      expect(titleOf({ ...base, firstName: ' ', lastName: '', ngoName: 'Організація' })).toBe('Організація');
    });

    it('has only the status line when there is no name and no NGO, and no title at all for a valid account', () => {
      const nameless: AccountTile = { id: 'a', firstName: ' ', lastName: ' ', avatarUrl: null };
      expect(titleOf(nameless)).toBeNull();
      expect(titleOf({ ...nameless, outdated: true })).toBe(uk.nav.account.outdated);
    });

    it('gives the screen readers the same text, or the plain "account" label when there is none', () => {
      const withText = statusOf({ ...named, outdated: true }).tile;
      expect(withText.getAttribute('aria-label')).toBe(withText.getAttribute('title'));
      const nameless = statusOf({ id: 'a', firstName: ' ', lastName: ' ', avatarUrl: null }).tile;
      expect(nameless.getAttribute('aria-label')).toBe(uk.nav.account.account);
    });
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

  it('gives the active account the active class (the white ring is drawn by it) and no other tile', () => {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', accounts(3).map((a, i) => ({ ...a, active: i === 0 })));
    fixture.detectChanges();
    const tiles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.account-tile'));
    expect(tiles.map((t) => t.classList.contains('active'))).toEqual([true, false, false]);
  });

  it('draws the avatar of every tile with the shared account avatar, giving it the account', () => {
    const { element } = render(3);
    expect(element.querySelectorAll('.account-tile app-account-avatar')).toHaveLength(3);
  });
});
