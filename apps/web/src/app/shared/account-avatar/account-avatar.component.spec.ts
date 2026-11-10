import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { ProtectedSrcDirective } from '../protected-src/protected-src.directive';
import { AccountAvatarComponent } from './account-avatar.component';
import { AccountAvatarModule } from './account-avatar.module';
import { AccountTile } from './account-tile';

describe('AccountAvatarComponent', () => {
  const base: AccountTile = { id: 'a', firstName: 'Перший', lastName: 'Другий', avatarUrl: null };

  beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      // Модуль, а не оголошення компонента напряму: інакше директиви шаблону в тесті не діють.
      imports: [AccountAvatarModule],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.restoreAllMocks());

  function render(tile: AccountTile) {
    const fixture = TestBed.createComponent(AccountAvatarComponent);
    fixture.componentRef.setInput('account', tile);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows no status icon and no logo badge for a valid account without a logo', () => {
    const { element } = render(base);
    expect(element.querySelector('.account-avatar__status')).toBeNull();
    expect(element.querySelector('.account-avatar__badge')).toBeNull();
    expect(element.querySelectorAll('img')).toHaveLength(0);
  });

  it.each([
    ['outdated (the tokens were rejected)', { outdated: true }, 'alert-circle', 'account-avatar__status--outdated'],
    ['unavailable (the server did not answer)', { unavailable: true }, 'sync-circle', 'account-avatar__status--unavailable'],
    ['outdated and unavailable, the red one of the outdated', { outdated: true, unavailable: true }, 'alert-circle', 'account-avatar__status--outdated'],
  ])('draws the icon for an account that is %s', (_state, flags, name, cssClass) => {
    const icon = render({ ...base, ...flags }).element.querySelector('.account-avatar__status')!;
    expect(icon.getAttribute('name')).toBe(name);
    expect(icon.classList).toContain(cssClass);
    // Значок лише візуальний: статус для допоміжних технологій у підписі плитки чи рядка.
    expect(icon.getAttribute('aria-hidden')).toBe('true');
  });

  it('marks the status icon and the NGO logo for the alpha-channel outline, with no box-based border around them', () => {
    // Сам контур малює AlphaOutlineDirective (перевіряється в її спеку й у браузері);
    // тут — що значки ним позначені.
    const { element } = render({ ...base, outdated: true, logoUrl: 'assets/custom.png' });
    const icon = element.querySelector('.account-avatar__status')!;
    const logo = element.querySelector('.account-avatar__badge img')!;
    for (const marked of [icon, logo]) {
      expect(marked.hasAttribute('appalphaoutline')).toBe(true);
      expect(marked.getAttribute('outlinecolor')).toBe('var(--nav-avatar-bg)');
    }
  });

  it.each([[null], [undefined], ['']])('shows the logo only when logoUrl is set (logoUrl: %s)', (empty) => {
    expect(render({ ...base, logoUrl: empty }).element.querySelector('.account-avatar__badge')).toBeNull();
  });

  it('gives the address of the logo to appProtectedSrc (the directive puts it into src, loading the file with the token)', () => {
    const { fixture } = render({ ...base, logoUrl: 'assets/custom.png' });
    const directive = fixture.debugElement.query(By.directive(ProtectedSrcDirective)).injector.get(ProtectedSrcDirective);
    expect(directive.appProtectedSrc()).toBe('assets/custom.png');
  });

  it('greys the avatar of an outdated account only', () => {
    const grey = (tile: AccountTile) => render(tile).element.querySelector('.avatar')!.classList.contains('avatar--outdated');
    expect(grey({ ...base, outdated: true })).toBe(true);
    expect(grey({ ...base, unavailable: true })).toBe(false);
    expect(grey(base)).toBe(false);
  });
});
