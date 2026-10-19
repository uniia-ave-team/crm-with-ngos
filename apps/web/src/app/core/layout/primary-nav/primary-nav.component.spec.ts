import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { provideTranslateService } from '@ngx-translate/core';

import { AccountsService } from '../../accounts/accounts.service';
import { AuthService } from '../../auth/auth.service';
import { ConnectionService } from '../../connection/connection.service';
import { CurrentUserService } from '../../users/current-user.service';
import { LayoutModule } from '../layout.module';
import { PrimaryNavComponent } from './primary-nav.component';
import { PopoverComponent } from '../../../shared/popover/popover.component';

describe('PrimaryNavComponent avatar initials', () => {
  let http: HttpTestingController;
  let currentUser: CurrentUserService;
  let server: string;

  beforeEach(async () => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [IonicModule.forRoot(), LayoutModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    currentUser = TestBed.inject(CurrentUserService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  /** Підтверджує вхід профілем із заданими імʼям і прізвищем. */
  async function signInAs(firstName: string, lastName: string, avatarUrl: string | null = null): Promise<void> {
    TestBed.inject(AccountsService).add({ server, accessToken: 'access', refreshToken: 'refresh', identity: 'u1' });
    const result = currentUser.ensure();
    http.expectOne(`${server}/api/v1/users/me`).flush({ id: 'u1', email: 'a@b.org', firstName, lastName, avatarUrl, roles: [], customFields: [] });
    await result;
  }

  const avatars = (element: HTMLElement) =>
    Array.from(element.querySelectorAll('.rail__avatar .avatar, .mobile-topbar__avatar .avatar')).map((s) => s.textContent?.trim());

  it('shows the first letters of the first and last name in both avatars', async () => {
    await signInAs('Олена', 'Іваненко');
    const fixture = TestBed.createComponent(PrimaryNavComponent);
    fixture.detectChanges();
    expect(avatars(fixture.nativeElement)).toEqual(['ОІ', 'ОІ']);
  });

  it('upper-cases the letters and ignores surrounding spaces', async () => {
    await signInAs('  євген', ' вишневський');
    const fixture = TestBed.createComponent(PrimaryNavComponent);
    fixture.detectChanges();
    expect(avatars(fixture.nativeElement)).toEqual(['ЄВ', 'ЄВ']);
  });

  it('is empty until the profile is loaded, then fills in', async () => {
    const fixture = TestBed.createComponent(PrimaryNavComponent);
    fixture.detectChanges();
    expect(avatars(fixture.nativeElement)).toEqual(['', '']);

    await signInAs('Олена', 'Іваненко');
    fixture.detectChanges();
    expect(avatars(fixture.nativeElement)).toEqual(['ОІ', 'ОІ']);
  });

  it('copes with a missing name', async () => {
    await signInAs('Олена', '');
    const fixture = TestBed.createComponent(PrimaryNavComponent);
    fixture.detectChanges();
    expect(avatars(fixture.nativeElement)).toEqual(['О', 'О']);
  });

  describe('account list', () => {
    const tiles = (fixture: ReturnType<typeof TestBed.createComponent<PrimaryNavComponent>>) =>
      (fixture.componentInstance as unknown as { accounts(): Record<string, unknown>[] }).accounts();

    it('shows the stored accounts instead of a placeholder, in the order they were added', () => {
      const store = TestBed.inject(AccountsService);
      store.add({ server: 'http://one.example:5065', accessToken: 'a', refreshToken: 'r', identity: 'u1', firstName: 'Олена', lastName: 'Іваненко', ngoName: 'Добра справа' });
      store.add({ server: 'http://two.example:5065', accessToken: 'a2', refreshToken: 'r2', identity: 'u2', firstName: 'Петро', lastName: 'Коваль' });
      const fixture = TestBed.createComponent(PrimaryNavComponent);
      fixture.detectChanges();

      expect(tiles(fixture).map((tile) => [tile['firstName'], tile['lastName'], tile['ngoName']])).toEqual([
        ['Олена', 'Іваненко', 'Добра справа'],
        ['Петро', 'Коваль', null],
      ]);
    });

    it('marks the active account and the outdated one', () => {
      const store = TestBed.inject(AccountsService);
      const first = store.add({ server: 'http://one.example:5065', accessToken: 'a', refreshToken: 'r', identity: 'u1' });
      const second = store.add({ server: 'http://two.example:5065', accessToken: 'a2', refreshToken: 'r2', identity: 'u2' });
      store.update(first.id, { outdated: true, unavailable: true });
      const fixture = TestBed.createComponent(PrimaryNavComponent);
      fixture.detectChanges();

      const byId = Object.fromEntries(tiles(fixture).map((tile) => [tile['id'], tile]));
      expect(byId[second.id]).toMatchObject({ active: true, outdated: false, unavailable: false });
      expect(byId[first.id]).toMatchObject({ active: false, outdated: true, unavailable: true });
    });

    it('resolves relative avatar and NGO logo links against the server of that very account', () => {
      const store = TestBed.inject(AccountsService);
      store.add({
        server: 'http://one.example:5065',
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'u1',
        avatarUrl: '/files/a.png',
        ngoLogoUrl: 'files/logo.png',
      });
      store.add({
        server: 'http://two.example:5065',
        accessToken: 'a2',
        refreshToken: 'r2',
        identity: 'u2',
        avatarUrl: 'https://cdn.example/b.png',
        ngoLogoUrl: null,
      });
      const fixture = TestBed.createComponent(PrimaryNavComponent);
      fixture.detectChanges();

      expect(tiles(fixture).map((tile) => [tile['avatarUrl'], tile['logoUrl']])).toEqual([
        ['http://one.example:5065/files/a.png', 'http://one.example:5065/files/logo.png'],
        ['https://cdn.example/b.png', null],
      ]);
    });

    it('is empty when no accounts are stored', () => {
      const fixture = TestBed.createComponent(PrimaryNavComponent);
      fixture.detectChanges();
      expect(tiles(fixture)).toEqual([]);
    });

    it('follows accounts added or removed while the menu exists', () => {
      const store = TestBed.inject(AccountsService);
      const fixture = TestBed.createComponent(PrimaryNavComponent);
      fixture.detectChanges();
      expect(tiles(fixture)).toHaveLength(0);

      const added = store.add({ server: 'http://one.example:5065', accessToken: 'a', refreshToken: 'r', identity: 'u1' });
      expect(tiles(fixture)).toHaveLength(1);
      store.remove(added.id);
      expect(tiles(fixture)).toHaveLength(0);
    });
  });

  it('signs the user out from the account popover: closes it first, then calls the logout', async () => {
    const logout = vi.spyOn(TestBed.inject(AuthService), 'logout').mockResolvedValue('ok');
    const fixture = TestBed.createComponent(PrimaryNavComponent);
    fixture.detectChanges();
    let finishClosing!: () => void;
    const popover = {
      close: vi.fn(() => new Promise<void>((resolve) => (finishClosing = resolve))),
    } as unknown as PopoverComponent;

    const signingOut = (fixture.componentInstance as unknown as { signOut(p: PopoverComponent): Promise<void> }).signOut(popover);

    // Поки вікно закривається, виходу ще нема: інакше перехід на /login лишив би його висіти.
    expect(popover.close).toHaveBeenCalled();
    expect(logout).not.toHaveBeenCalled();
    finishClosing();
    await signingOut;
    expect(logout).toHaveBeenCalledTimes(1);
  });
});

describe('PrimaryNavComponent admin item', () => {
  let http: HttpTestingController;
  let server: string;
  const ALL = ['Permissions.UpdateNgo', 'Permissions.CreateLoginPageImages', 'Permissions.DeleteLoginPageImages'];

  beforeEach(async () => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [IonicModule.forRoot(), LayoutModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1' });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const urls = (element: HTMLElement, selector: string) =>
    Array.from(element.querySelectorAll(selector)).map((a) => a.getAttribute('href'));

  function render(permissions: string[]) {
    TestBed.inject(AccountsService).update(TestBed.inject(AccountsService).activeId()!, { permissions });
    const fixture = TestBed.createComponent(PrimaryNavComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows the item in the rail and in the bottom tabs when the user has all three rights', () => {
    const { element } = render(ALL);
    expect(urls(element, '.rail__item')).toEqual(['/calendar', '/planner', '/knowledge-base', '/colleagues', '/admin-panel']);
    expect(urls(element, '.bottom-tabs__item')).toEqual(['/calendar', '/planner', '/knowledge-base', '/colleagues', '/admin-panel']);
  });

  it('hides the item when there are no rights at all (also until they are received)', () => {
    const { element } = render([]);
    expect(urls(element, '.rail__item')).toEqual(['/calendar', '/planner', '/knowledge-base', '/colleagues']);
    expect(urls(element, '.bottom-tabs__item')).toEqual(['/calendar', '/planner', '/knowledge-base', '/colleagues']);
  });

  it.each(ALL)('shows the item when the user has only the right %s', (right) => {
    const { element } = render([right]);
    expect(urls(element, '.rail__item')).toContain('/admin-panel');
    expect(urls(element, '.bottom-tabs__item')).toContain('/admin-panel');
  });

  it('hides the item when the user has only other rights', () => {
    const { element } = render(['Permissions.ViewUser', 'Permissions.UpdateUser', 'Permissions.ViewNgo']);
    expect(urls(element, '.rail__item')).not.toContain('/admin-panel');
    expect(urls(element, '.bottom-tabs__item')).not.toContain('/admin-panel');
  });

  it('shows the item when the rights arrive later, and hides it again when they are taken away', () => {
    const accounts = TestBed.inject(AccountsService);
    const { fixture, element } = render([]);
    expect(urls(element, '.rail__item')).not.toContain('/admin-panel');

    accounts.update(accounts.activeId()!, { permissions: ALL });
    fixture.detectChanges();
    expect(urls(element, '.rail__item')).toContain('/admin-panel');

    // Лишилось одне з прав — пункт ще є.
    accounts.update(accounts.activeId()!, { permissions: ['Permissions.UpdateNgo'] });
    fixture.detectChanges();
    expect(urls(element, '.rail__item')).toContain('/admin-panel');

    accounts.update(accounts.activeId()!, { permissions: ['Permissions.ViewUser'] });
    fixture.detectChanges();
    expect(urls(element, '.rail__item')).not.toContain('/admin-panel');
  });
});
