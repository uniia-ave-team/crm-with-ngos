import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { AccountsService } from '../../accounts/accounts.service';
import { REDIRECT_URL } from '../../auth/auth.service';
import { ConnectionService } from '../../connection/connection.service';
import { LayoutModule } from '../layout.module';
import { AddAccountComponent } from './add-account.component';

// Приватне/protected API компонента, яке в шаблоні викликають події.
interface AddAccountInternals {
  /** Для чого малюється форма входу (`app-sign-in-form`): по одному елементу на відкрите вікно. */
  forms: { (): { accountId: string | undefined; server: string; email: string }[] };
  accountId: { (): string | undefined };
  titleKey: { (): string };
  remove(): Promise<void>;
  openApp(): Promise<void>;
}

describe('AddAccountComponent', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let openModal: ReturnType<typeof vi.spyOn>;
  let closeModal: ReturnType<typeof vi.spyOn>;
  let connection: ConnectionService;
  let current: string;
  const other = 'https://other.example:5065';

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
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    current = connection.serverUrl();
    // Сервер перевіряє сама форма входу (її спек); тут він «недоступний», щоб не було запитів.
    vi.spyOn(connection, 'probeApi').mockResolvedValue(false);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    // Анімацію закриття в jsdom ніхто не завершить: вікно «закривається» одразу.
    openModal = vi.spyOn(ModalComponent.prototype, 'open').mockImplementation(() => undefined);
    closeModal = vi.spyOn(ModalComponent.prototype, 'close').mockResolvedValue();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function create() {
    const fixture = TestBed.createComponent(AddAccountComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    return { fixture, component, page: component as unknown as AddAccountInternals };
  }

  // Зміст вікна в jsdom не монтується (його малює ion-modal при відкритті), тож форму входу
  // перевіряємо за тим, для чого вікно її малює (`forms`); саму форму — в її спеку.
  describe('the sign-in form of the window', () => {
    it('does not exist until the window is opened', () => {
      const { page } = create();
      expect(page.forms()).toEqual([]);
      expect(connection.probeApi).not.toHaveBeenCalled();
    });

    it('is drawn for the current server, with no e-mail and no key, when the window is opened', () => {
      const { component, page } = create();
      component.open();

      expect(openModal).toHaveBeenCalledTimes(1);
      expect(page.forms()).toEqual([{ accountId: undefined, server: current, email: '' }]);
    });

    it('takes the server of the active account as the current one', () => {
      TestBed.inject(AccountsService).add({ server: other, accessToken: 'a', refreshToken: 'r', identity: 'someone' });
      const { component, page } = create();
      component.open();
      expect(page.forms()[0].server).toBe(other);
    });

    it('gets the given e-mail, key and server', () => {
      const { component, page } = create();
      component.open('a@b.org', 'some-key', other);
      expect(page.forms()).toEqual([{ accountId: 'some-key', server: other, email: 'a@b.org' }]);
    });

    it('is a new form every time the window is opened, even with the same data, so nothing typed is kept', () => {
      const { component, page } = create();
      component.open('a@b.org', 'some-key', other);
      const [first] = page.forms();

      component.open('a@b.org', 'some-key', other);
      const reopened = page.forms();
      expect(reopened).toHaveLength(1);
      // Форми в шаблоні розрізняються за цим обʼєктом: новий обʼєкт — нова форма.
      expect(reopened[0]).not.toBe(first);

      component.open();
      expect(page.forms()[0].server).toBe(current);
    });

    it('goes away when the window has closed', () => {
      const { fixture, component, page } = create();
      component.open();
      expect(page.forms()).toHaveLength(1);

      (fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent).closed.emit();
      expect(page.forms()).toEqual([]);
    });

    it('after a sign-in closes the window, checks the connection of the new account and opens the first page, in this order', async () => {
      const { component, page } = create();
      component.open();
      const order: string[] = [];
      closeModal.mockImplementation(async () => void order.push('close'));
      vi.spyOn(connection, 'checkApi').mockImplementation(async () => {
        order.push('check');
        return 'healthy';
      });
      navigate.mockImplementation(async () => {
        order.push('navigate');
        return true;
      });

      await page.openApp();

      expect(order).toEqual(['close', 'check', 'navigate']);
      expect(navigate).toHaveBeenCalledWith(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
    });
  });

  describe('the title of the window', () => {
    it('is "Add account" for a new account and "Sign in to the account" when a key of a saved one is given', () => {
      const { component, page } = create();
      const translate = TestBed.inject(TranslateService);

      component.open();
      expect(translate.instant(page.titleKey())).toBe(uk.nav.account.add);
      component.open('a@b.org', 'some-key');
      expect(translate.instant(page.titleKey())).toBe(uk.nav.account.signIn);
      expect(uk.nav.account.signIn).toBe('Ввійти в акаунт');
      component.open();
      expect(translate.instant(page.titleKey())).toBe(uk.nav.account.add);
    });
  });

  describe('the trash button (it shows only when a key is given)', () => {
    const savedAccount = () => ({ server: other, accessToken: 'old', refreshToken: 'old-refresh', identity: 'user-1', email: 'saved@b.org' });

    it('knows the key after open(), and forgets it when the window is opened again without a key', () => {
      const { component, page } = create();
      component.open(undefined, 'some-key');
      expect(page.accountId()).toBe('some-key');
      component.open();
      expect(page.accountId()).toBeUndefined();
    });

    it('forgets the account of the key with its tokens, leaves the others and the rest of the memory, and closes the window', async () => {
      localStorage.setItem('yavir.language', 'uk');
      const accounts = TestBed.inject(AccountsService);
      const outdated = accounts.add(savedAccount());
      accounts.update(outdated.id, { outdated: true });
      const active = accounts.add({ server: current, accessToken: 'a', refreshToken: 'r', identity: 'active' });
      const { component, page } = create();
      component.open(outdated.email, outdated.id, outdated.server);

      await page.remove();

      expect(accounts.accounts().map((a) => a.id)).toEqual([active.id]);
      expect(localStorage.getItem(`yavir.account.${outdated.id}`)).toBeNull();
      expect(accounts.activeId()).toBe(active.id);
      expect(localStorage.getItem('yavir.language')).toBe('uk');
      expect(closeModal).toHaveBeenCalled();
    });

    it('only closes the window when there is no key, or no account has it', async () => {
      const accounts = TestBed.inject(AccountsService);
      accounts.add({ server: current, accessToken: 'a', refreshToken: 'r', identity: 'active' });
      const { component, page } = create();

      component.open();
      await page.remove();
      component.open(undefined, 'unknown');
      await page.remove();

      expect(accounts.accounts()).toHaveLength(1);
      expect(closeModal).toHaveBeenCalledTimes(2);
    });
  });

  it('closes by close()', async () => {
    const { component } = create();
    await component.close();
    expect(closeModal).toHaveBeenCalled();
  });
});
