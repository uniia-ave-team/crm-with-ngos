import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslatePipe, provideTranslateService } from '@ngx-translate/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { AlphaOutlineModule } from '../../../shared/alpha-outline/alpha-outline.module';
import { AvatarModule } from '../../../shared/avatar/avatar.module';
import { AccountMenuComponent, AccountTile } from './account-menu.component';

function accounts(count: number): AccountTile[] {
  return Array.from({ length: count }, (_, id) => ({ id, firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null }));
}

describe('AccountMenuComponent', () => {
  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      declarations: [AccountMenuComponent],
      imports: [AvatarModule, AlphaOutlineModule, TranslatePipe],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
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

  it('hides the accounts block with the global planned class while the feature is not ready', () => {
    const { element } = render(2);
    expect(element.querySelector('.account-menu__accounts')!.classList).toContain('planned');
  });

  it('shows no logo badge at all when an account has no logoUrl', () => {
    const { element } = render(3);
    expect(element.querySelectorAll('.account-tile__badge')).toHaveLength(0);
    expect(element.querySelectorAll('.account-tile img')).toHaveLength(0);
  });

  it('marks an outdated account: yellow sync icon on the left and its own label', () => {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [
      { id: 'a', firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null },
      { id: 'b', firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null, outdated: true },
    ]);
    fixture.detectChanges();
    const tiles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.account-tile'));

    // Сірий аватар (клас на самому аватарі) перевіряє avatar.component.spec.ts.
    expect(tiles[0].querySelector('.account-tile__status')).toBeNull();
    expect(tiles[0].classList).not.toContain('outdated');

    expect(tiles[1].classList).toContain('outdated');
    expect(tiles[1].classList).not.toContain('unavailable');
    const icon = tiles[1].querySelector('.account-tile__status')!;
    expect(icon.classList).toContain('account-tile__status--outdated');
    expect(icon.getAttribute('name')).toBe('sync-circle');
    // Значок — лише візуальний: статус для допоміжних технологій у підписі плитки.
    expect(icon.getAttribute('aria-hidden')).toBe('true');
    expect(tiles[1].getAttribute('aria-label')).not.toBe(tiles[0].getAttribute('aria-label'));
  });

  it('shows a red alert icon, not the sync one, for an account whose server did not answer', () => {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [
      { id: 'a', firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null, outdated: true },
      { id: 'b', firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null, outdated: true, unavailable: true },
    ]);
    fixture.detectChanges();
    const tiles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.account-tile'));

    const rejected = tiles[0].querySelector('.account-tile__status')!;
    expect(rejected.getAttribute('name')).toBe('sync-circle');

    const down = tiles[1].querySelector('.account-tile__status')!;
    expect(down.getAttribute('name')).toBe('alert-circle');
    expect(down.classList).toContain('account-tile__status--unavailable');
    expect(down.getAttribute('aria-hidden')).toBe('true');
    expect(tiles[1].classList).toContain('unavailable');
    // Свій підпис і підказка, відмінні від «сесію завершено».
    expect(tiles[1].getAttribute('aria-label')).not.toBe(tiles[0].getAttribute('aria-label'));
    expect(tiles[1].getAttribute('title')).toBe(tiles[1].getAttribute('aria-label'));
  });

  it('shows no unavailable mark for a valid account, whatever its flag says', () => {
    const fixture = TestBed.createComponent(AccountMenuComponent);
    fixture.componentRef.setInput('accounts', [
      { id: 'a', firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null, unavailable: true },
    ]);
    fixture.detectChanges();
    const tile = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('.account-tile')!;
    expect(tile.querySelector('.account-tile__status')).toBeNull();
    expect(tile.classList).not.toContain('unavailable');
  });

  it('has the settings item, a divider and the sign-out item, in that order', () => {
    const { element } = render(1);
    const parts = Array.from(element.querySelector('.account-menu')!.children).map((c) => c.className);
    // Блок акаунтів поки прихований класом `planned`.
    expect(parts).toEqual(['account-menu__accounts planned', 'account-menu__item', 'account-menu__divider', 'account-menu__item']);
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
      { id: 0, firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null, logoUrl: empty },
      { id: 1, firstName: 'Євген', lastName: 'Вишневський', avatarUrl: null, logoUrl: 'assets/custom.png' },
    ]);
    fixture.detectChanges();
    const tiles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.account-tile'));
    expect(tiles[0].querySelector('.account-tile__badge')).toBeNull();
    expect(Array.from(tiles[1].querySelectorAll('.account-tile__badge img')).map((i) => i.getAttribute('src'))).toEqual([
      'assets/custom.png',
    ]);
  });
});
