import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { AccountRestoreService } from '../core/accounts/account-restore.service';
import { AccountsService } from '../core/accounts/accounts.service';
import { AuthService } from '../core/auth/auth.service';
import { ConnectionService } from '../core/connection/connection.service';
import { DEFAULT_LOGIN_IMAGE } from '../core/login-page-images/login-page-images.service';
import { ReloginFormComponent } from './relogin-form/relogin-form.component';
import { ReloginPage } from './relogin.page';
import { ReloginPageModule } from './relogin.module';

describe('ReloginPage', () => {
  let navigate: ReturnType<typeof vi.spyOn>;
  let accounts: AccountsService;
  const server = 'https://server.example:5065';

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      imports: [ReloginPageModule],
      providers: [provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }), provideHttpClient(), provideHttpClientTesting()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('uk', uk);
    translate.use('uk');
    accounts = TestBed.inject(AccountsService);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function open() {
    return openPage().nativeElement as HTMLElement;
  }

  function openPage() {
    const fixture = TestBed.createComponent(ReloginPage);
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
    return fixture;
  }

  /** Додає акаунт і знімає з нього активність (на сторінку потрапляють, коли активного нема). */
  function addAccount(identity: string, snapshot: Parameters<AccountsService['update']>[1] = {}): string {
    const account = accounts.add({ server, accessToken: 'a', refreshToken: 'r', identity });
    accounts.update(account.id, { firstName: 'Перший', lastName: 'Другий', patronymic: 'Третій', ngoName: 'Організація', ...snapshot });
    accounts.setActive(null);
    return account.id;
  }

  describe('the check at the entrance', () => {
    it('goes to /login when there is no account in the memory', () => {
      open();
      expect(navigate).toHaveBeenCalledWith('/login');
    });

    it.each([
      ['an available account', {}],
      ['an outdated account', { outdated: true }],
      ['an unavailable account', { unavailable: true }],
    ])('stays on the page when there is %s in the memory, whether it is active or not', (_name, flags) => {
      addAccount('u1', flags);
      open();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('goes to /login when the accounts disappear while the page is open', () => {
      addAccount('u1');
      open();
      expect(navigate).not.toHaveBeenCalled();

      localStorage.clear();
      window.dispatchEvent(new StorageEvent('storage', { key: null }));
      TestBed.tick();
      expect(navigate).toHaveBeenCalledWith('/login');
    });
  });

  describe('the list of accounts', () => {
    const rows = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLElement>('.relogin-account:not(.relogin-add)'));
    const name = (row: HTMLElement) => row.querySelector('.relogin-account__name')!.textContent!.trim();
    const status = (row: HTMLElement) => row.querySelector('.relogin-account__status')?.textContent?.trim() ?? null;

    it('has the title from the translations', () => {
      addAccount('u1');
      expect(open().querySelector('.relogin-card__title')!.textContent!.trim()).toBe(uk.relogin.title);
    });

    it('shows a row with an avatar for every stored account, in the order they were added, with the active one too', () => {
      const first = addAccount('u1', { ngoName: 'Організація 1' });
      addAccount('u2', { ngoName: 'Організація 2' });
      accounts.setActive(first);
      const element = open();

      expect(rows(element)).toHaveLength(2);
      expect(rows(element).map(name)).toEqual(['Другий Перший Третій — Організація 1', 'Другий Перший Третій — Організація 2']);
      expect(element.querySelectorAll('.relogin-account app-account-avatar')).toHaveLength(2);
    });

    it('writes the state under the name only for an account in trouble, with the texts of the account menu', () => {
      addAccount('u1');
      addAccount('u2', { outdated: true });
      addAccount('u3', { unavailable: true });
      addAccount('u4', { outdated: true, unavailable: true });
      const found = rows(open());

      expect(found.map(status)).toEqual([null, uk.nav.account.outdated, uk.nav.account.unavailable, uk.nav.account.unavailable]);
      // Колір підпису — як у значка: червоний для застарілого (також разом з недоступним), жовтий для лише недоступного.
      const red = (row: HTMLElement) => row.querySelector('.relogin-account__status--outdated') !== null;
      expect(found.slice(1).map(red)).toEqual([true, false, true]);
    });

    it('falls back to the plain "account" text when the account has neither a name nor an NGO', () => {
      addAccount('u1', { firstName: ' ', lastName: ' ', patronymic: '', ngoName: null });
      expect(name(rows(open())[0])).toBe(uk.nav.account.account);
    });

    it('has the button of adding an account under the list, with the same text and alt as in the account menu', () => {
      addAccount('u1');
      const add = open().querySelector<HTMLElement>('.relogin-add')!;
      expect(add.querySelector('.relogin-add__icon ion-icon')!.getAttribute('name')).toBe('add-outline');
      expect(add.textContent!.trim()).toBe(uk.nav.account.add);
      expect(add.getAttribute('title')).toBe(uk.nav.account.add);
      expect(add.getAttribute('aria-label')).toBe(uk.nav.account.add);
      // Кнопка — останній пункт списку.
      const items = Array.from(add.closest('ul')!.children);
      expect(items[items.length - 1].contains(add)).toBe(true);
    });
  });

  describe('on opening', () => {
    it('tries to refresh the accounts whose server did not answer, but only those that are not outdated', () => {
      const refresh = vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('ok');
      addAccount('u1');
      const unavailable = addAccount('u2', { unavailable: true });
      addAccount('u3', { outdated: true });
      addAccount('u4', { outdated: true, unavailable: true });
      open();
      expect(refresh.mock.calls.map(([id]) => id)).toEqual([unavailable]);
    });

    it('refreshes nothing when every account is available', () => {
      const refresh = vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('ok');
      addAccount('u1');
      open();
      expect(refresh).not.toHaveBeenCalled();
    });
  });

  describe('choosing in the list', () => {
    const rows = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLButtonElement>('.relogin-account:not(.relogin-add)'));
    const form = (element: HTMLElement) => element.querySelector('app-relogin-form');
    const flush = async (fixture: { detectChanges(): void }) => {
      await vi.waitFor(() => {
        TestBed.tick();
        fixture.detectChanges();
      });
    };
    let activate: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
      activate = vi.spyOn(TestBed.inject(AccountRestoreService), 'activate');
      // Форма сама перевіряє сервер: тут це не потрібно.
      vi.spyOn(TestBed.inject(ConnectionService), 'probeApi').mockResolvedValue(false);
    });

    it.each([
      ['an available account', {}],
      ['an unavailable account that is not outdated', { unavailable: true }],
    ])('switches to %s and opens the planner again', async (_name, flags) => {
      vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('unreachable');
      const id = addAccount('u1', flags);
      activate.mockResolvedValue('ok');
      const fixture = openPage();

      rows(fixture.nativeElement)[0].click();
      await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/planner', { onSameUrlNavigation: 'reload' }));
      expect(activate).toHaveBeenCalledWith(id);
      expect(form(fixture.nativeElement)).toBeNull();
    });

    it.each(['stale', 'unavailable'] as const)(
      'opens the form with the server and the e-mail of the account when the switch fails (%s)',
      async (failure) => {
        vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('unreachable');
        addAccount('u1', { email: 'first@organization.org' });
        activate.mockResolvedValue(failure);
        const fixture = openPage();

        rows(fixture.nativeElement)[0].click();
        await vi.waitFor(() => expect(activate).toHaveBeenCalled());
        await flush(fixture);

        expect(form(fixture.nativeElement)).not.toBeNull();
        const inputs = fixture.debugElement.query((d) => d.componentInstance instanceof ReloginFormComponent).componentInstance as ReloginFormComponent;
        expect(inputs.initialServer()).toBe(server);
        expect(inputs.initialEmail()).toBe('first@organization.org');
        expect(inputs.accountId()).toBeTruthy();
        expect(navigate).not.toHaveBeenCalledWith('/planner', expect.anything());
      },
    );

    it.each([
      ['outdated', { outdated: true }],
      ['outdated and unavailable', { outdated: true, unavailable: true }],
    ])('opens the form at once, without asking the server, for an account that is %s', async (_name, flags) => {
      vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('skipped');
      addAccount('u1', flags);
      const fixture = openPage();

      rows(fixture.nativeElement)[0].click();
      await flush(fixture);

      expect(activate).not.toHaveBeenCalled();
      expect(form(fixture.nativeElement)).not.toBeNull();
    });

    it('opens the form for a new account from the add button: the address of the page, no e-mail, no trash', async () => {
      addAccount('u1');
      const fixture = openPage();

      fixture.nativeElement.querySelector('.relogin-add').click();
      await flush(fixture);

      const inputs = fixture.debugElement.query((d) => d.componentInstance instanceof ReloginFormComponent).componentInstance as ReloginFormComponent;
      expect(inputs.accountId()).toBeNull();
      expect(inputs.initialServer()).toBe(TestBed.inject(ConnectionService).pageServerUrl);
      expect(inputs.initialEmail()).toBe('');
      expect(fixture.nativeElement.querySelector('.relogin-form__remove')).toBeNull();
      expect(activate).not.toHaveBeenCalled();
    });

    it('ignores a second click while the switch is running', async () => {
      vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('unreachable');
      addAccount('u1');
      let finish: (value: 'ok') => void = () => undefined;
      activate.mockReturnValue(new Promise((resolve) => (finish = resolve)));
      const fixture = openPage();

      const [row] = rows(fixture.nativeElement);
      row.click();
      row.click();
      expect(activate).toHaveBeenCalledTimes(1);
      finish('ok');
      await vi.waitFor(() => expect(navigate).toHaveBeenCalled());
    });
  });

  describe('the form', () => {
    const rowsOf = (element: HTMLElement) => element.querySelectorAll('.relogin-account:not(.relogin-add)');

    async function openFormFor(flags: { outdated?: boolean }) {
      vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('skipped');
      vi.spyOn(TestBed.inject(ConnectionService), 'probeApi').mockResolvedValue(false);
      const first = addAccount('u1', { email: 'first@organization.org', ...flags });
      addAccount('u2', { outdated: true });
      const fixture = openPage();
      (fixture.nativeElement.querySelectorAll('.relogin-account')[0] as HTMLElement).click();
      await vi.waitFor(() => {
        TestBed.tick();
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelector('app-relogin-form')).not.toBeNull();
      });
      return { fixture, first };
    }

    it('returns to the list with the default image on "Back", keeping the accounts', async () => {
      const { fixture } = await openFormFor({ outdated: true });
      (fixture.nativeElement.querySelector('.relogin-form__back') as HTMLElement).click();
      TestBed.tick();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-relogin-form')).toBeNull();
      expect(rowsOf(fixture.nativeElement)).toHaveLength(2);
      expect(fixture.nativeElement.querySelector('.relogin-card__image').getAttribute('src')).toBe(DEFAULT_LOGIN_IMAGE);
      expect(accounts.accounts()).toHaveLength(2);
    });

    it('forgets the account of the form with the trash button and returns to the list', async () => {
      const { fixture, first } = await openFormFor({ outdated: true });
      (fixture.nativeElement.querySelector('.relogin-form__remove') as HTMLElement).click();
      TestBed.tick();
      fixture.detectChanges();

      expect(accounts.accounts().map((a) => a.id)).not.toContain(first);
      expect(localStorage.getItem(`yavir.account.${first}`)).toBeNull();
      expect(fixture.nativeElement.querySelector('app-relogin-form')).toBeNull();
      expect(rowsOf(fixture.nativeElement)).toHaveLength(1);
    });

    it('goes to /login when the trash button forgets the last account', async () => {
      vi.spyOn(TestBed.inject(AuthService), 'refreshStored').mockResolvedValue('skipped');
      vi.spyOn(TestBed.inject(ConnectionService), 'probeApi').mockResolvedValue(false);
      addAccount('u1', { outdated: true });
      const fixture = openPage();
      (fixture.nativeElement.querySelector('.relogin-account') as HTMLElement).click();
      await vi.waitFor(() => {
        TestBed.tick();
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelector('.relogin-form__remove')).not.toBeNull();
      });

      (fixture.nativeElement.querySelector('.relogin-form__remove') as HTMLElement).click();
      TestBed.tick();
      expect(navigate).toHaveBeenCalledWith('/login');
    });

    it('shows the image that the form gives, and falls back to the default one when it does not load', async () => {
      const { fixture } = await openFormFor({ outdated: true });
      const form = fixture.debugElement.query((d) => d.componentInstance instanceof ReloginFormComponent).componentInstance as ReloginFormComponent;
      const image = fixture.nativeElement.querySelector('.relogin-card__image') as HTMLImageElement;

      form.imageChange.emit('https://server.example/files/login/abc.jpg');
      TestBed.tick();
      fixture.detectChanges();
      expect(image.getAttribute('src')).toBe('https://server.example/files/login/abc.jpg');

      image.dispatchEvent(new Event('error'));
      TestBed.tick();
      fixture.detectChanges();
      expect(image.getAttribute('src')).toBe(DEFAULT_LOGIN_IMAGE);
    });
  });
});
