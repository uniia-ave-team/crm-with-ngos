import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
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
  address: { (): string };
  email: { (): string };
  password: { (): string };
  saving: { (): boolean };
  banner: { (): { key: string; params?: { url: string } } | null };
  addressError: { (): string | null };
  emailError: { (): string | null };
  passwordError: { (): string | null };
  accountId: { (): string | undefined };
  titleKey: { (): string };
  onFieldChange(field: unknown, value: string): void;
  submit(): Promise<void>;
  remove(): Promise<void>;
}

describe('AddAccountComponent', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let openModal: ReturnType<typeof vi.spyOn>;
  let closeModal: ReturnType<typeof vi.spyOn>;
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
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    current = connection.serverUrl();
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

  function fill(page: AddAccountInternals, values: { address?: string; email?: string; password?: string }) {
    const fields = page as unknown as Record<'address' | 'email' | 'password', unknown>;
    for (const [field, value] of Object.entries(values)) {
      page.onFieldChange(fields[field as 'address'], value);
    }
  }

  const waitForRequest = (url: string) => vi.waitFor(() => http.expectOne(url));

  it('opens the modal with the current server in the address, and empty e-mail and password', () => {
    const { component, page } = create();
    component.open();

    expect(openModal).toHaveBeenCalledTimes(1);
    expect(page.address()).toBe(current);
    expect(page.email()).toBe('');
    expect(page.password()).toBe('');
  });

  it('takes the server of the active account as the current one', () => {
    TestBed.inject(AccountsService).add({ server: other, accessToken: 'a', refreshToken: 'r', identity: 'someone' });
    const { component, page } = create();
    component.open();
    expect(page.address()).toBe(other);
  });

  it('puts the given server into the address instead of the current one, and leaves the e-mail alone', () => {
    const { component, page } = create();
    component.open(undefined, undefined, other);

    expect(page.address()).toBe(other);
    expect(page.email()).toBe('');
  });

  it('takes the given server together with the e-mail and the key', () => {
    const { component, page } = create();
    component.open('a@b.org', 'some-key', other);

    expect(page.address()).toBe(other);
    expect(page.email()).toBe('a@b.org');
  });

  it('returns to the current server when it is opened again without a server', () => {
    const { component, page } = create();
    component.open(undefined, undefined, other);
    component.open();

    expect(page.address()).toBe(current);
  });

  it('signs in on the given server, when it is not changed', async () => {
    const { component, page } = create();
    component.open('a@b.org', undefined, other);
    fill(page, { password: 'secret' });

    const done = page.submit();
    http.expectOne(`${other}/health`).flush({ status: 'Healthy' });
    const login = await waitForRequest(`${other}/api/v1/auth/login`);
    login.flush({}, { status: 401, statusText: 'x' });
    await done;

    expect(page.banner()).toEqual({ key: 'login.form.error.unauthorized' });
  });

  it('puts the given e-mail into the field', () => {
    const { component, page } = create();
    component.open('a@b.org');
    expect(page.email()).toBe('a@b.org');
  });

  describe('with the key of an account', () => {
    // Збережений акаунт на поточному сервері (`current` відомий лише після beforeEach).
    const savedAccount = () => ({ server: current, accessToken: 'old', refreshToken: 'old-refresh', identity: 'user-1', email: 'Saved@b.org' });
    const jwtOf = (sub: string) => `h.${btoa(JSON.stringify({ sub })).replace(/=+$/, '')}.s`;

    /** Входить у вікні під `email` на поточному сервері й віддає відповідь сервера. */
    async function signIn(email: string, id: string | undefined, sub = 'user-1') {
      const { component, page } = create();
      component.open(email, id);
      fill(page, { password: 'secret' });
      const done = page.submit();
      http.expectOne(`${current}/health`).flush({ status: 'Healthy' });
      const login = await waitForRequest(`${current}/api/v1/auth/login`);
      login.flush({ accessToken: jwtOf(sub), refreshToken: 'fresh' });
      (await waitForRequest(`${current}/health`)).flush({ status: 'Healthy' });
      await done;
    }

    it('puts nothing into the form by itself', () => {
      const accounts = TestBed.inject(AccountsService);
      const id = accounts.add({ ...savedAccount(), server: other }).id;
      // Активний акаунт — на поточному сервері; ключ вказує на акаунт з іншого.
      accounts.add({ server: current, accessToken: 'a', refreshToken: 'r', identity: 'active' });
      const { component, page } = create();
      component.open(undefined, id);

      expect(page.address()).toBe(current);
      expect(page.email()).toBe('');
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
        const outdated = accounts.add({ ...savedAccount(), server: other });
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

    it('overwrites the account of the key, instead of adding one, when the e-mail is the same', async () => {
      const accounts = TestBed.inject(AccountsService);
      // Користувач у збереженому акаунті інший, ніж у токена, що прийде: ключ і пошта — єдине, що їх повʼязує.
      const id = accounts.add({ ...savedAccount(), identity: 'old-user' }).id;
      await signIn(' saved@B.org ', id, 'new-user');

      expect(accounts.accounts()).toHaveLength(1);
      expect(accounts.active()).toMatchObject({
        id,
        identity: 'new-user',
        email: 'saved@B.org',
        accessToken: jwtOf('new-user'),
        refreshToken: 'fresh',
      });
    });

    it('adds a new account when the e-mail is not the one of the account of the key', async () => {
      const accounts = TestBed.inject(AccountsService);
      const id = accounts.add(savedAccount()).id;
      await signIn('other@b.org', id, 'user-2');

      expect(accounts.accounts()).toHaveLength(2);
      expect(accounts.accounts().find((a) => a.id === id)).toMatchObject({ accessToken: 'old', identity: 'user-1' });
      expect(accounts.active()).toMatchObject({ identity: 'user-2', email: 'other@b.org' });
    });

    it('adds an account as usual when no account has the key', async () => {
      const accounts = TestBed.inject(AccountsService);
      accounts.add(savedAccount());
      await signIn('saved@b.org', 'unknown-key', 'user-2');

      expect(accounts.accounts()).toHaveLength(2);
    });

    it('does not check the key when the sign-in failed', async () => {
      const accounts = TestBed.inject(AccountsService);
      const id = accounts.add(savedAccount()).id;
      const { component, page } = create();
      component.open('saved@b.org', id);
      fill(page, { password: 'secret' });

      const done = page.submit();
      http.expectOne(`${current}/health`).flush({ status: 'Healthy' });
      (await waitForRequest(`${current}/api/v1/auth/login`)).flush({}, { status: 401, statusText: 'x' });
      await done;

      expect(accounts.accounts()).toHaveLength(1);
      expect(accounts.accounts()[0]).toMatchObject({ accessToken: 'old', refreshToken: 'old-refresh' });
    });
  });

  it('forgets what was typed and shown the last time when it is opened again', async () => {
    const { component, page } = create();
    component.open('a@b.org');
    fill(page, { address: other, password: 'secret' });
    const done = page.submit();
    http.expectOne(`${other}/health`).error(new ProgressEvent('error'));
    await done;
    expect(page.banner()).not.toBeNull();

    component.open();
    expect(page.address()).toBe(current);
    expect(page.email()).toBe('');
    expect(page.password()).toBe('');
    expect(page.banner()).toBeNull();
    expect(page.addressError()).toBeNull();
    expect(page.emailError()).toBeNull();
  });

  it('closes by close()', async () => {
    const { component } = create();
    await component.close();
    expect(closeModal).toHaveBeenCalled();
  });

  it('shows no error before the first attempt, then says what is missing and sends nothing', async () => {
    const { component, page } = create();
    component.open();
    expect(page.emailError()).toBeNull();
    expect(page.passwordError()).toBeNull();

    await page.submit();

    expect(page.emailError()).toBe('login.form.error.emailRequired');
    expect(page.passwordError()).toBe('login.form.error.passwordRequired');
    expect(page.addressError()).toBeNull();
    http.expectNone((r) => r.url.includes('/health') || r.url.includes('/auth/login'));
  });

  it('does not accept something that is not an address of a server', async () => {
    const { component, page } = create();
    component.open('a@b.org');
    fill(page, { address: 'не адреса', password: 'secret' });
    await page.submit();

    expect(page.addressError()).toBe('login.step1.error.invalid');
    expect(uk.login.step1.error.invalid).toBeTruthy();
    http.expectNone((r) => r.url.includes('/health') || r.url.includes('/auth/login'));
  });

  it('says the server is not found, and does not try to sign in, when it does not answer', async () => {
    const { component, page } = create();
    component.open('a@b.org');
    fill(page, { address: 'other.example', password: 'secret' });

    const done = page.submit();
    http.expectOne(`${location.protocol}//other.example:5065/health`).error(new ProgressEvent('error'));
    await done;

    expect(page.banner()).toEqual({
      key: 'login.step1.error.unreachable',
      params: { url: `${location.protocol}//other.example:5065` },
    });
    expect(page.saving()).toBe(false);
    http.expectNone((r) => r.url.includes('/auth/login'));
  });

  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [500, 'error'],
    [0, 'unreachable'],
  ])('keeps the window and shows the error above the fields when the server answers %i, so the form can be sent again', async (status, key) => {
    const { component, page } = create();
    component.open('a@b.org');
    fill(page, { password: 'secret' });

    const done = page.submit();
    http.expectOne(`${current}/health`).flush({ status: 'Healthy' });
    const login = await waitForRequest(`${current}/api/v1/auth/login`);
    if (status === 0) {
      login.error(new ProgressEvent('error'));
    } else {
      login.flush({}, { status, statusText: 'x' });
    }
    await done;

    expect(page.banner()).toEqual({ key: `login.form.error.${key}` });
    expect(page.saving()).toBe(false);
    expect(page.password()).toBe('secret');
    expect(TestBed.inject(AccountsService).accounts()).toHaveLength(0);
    expect(closeModal).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('signs in on the given server, adds the account as the active one, closes the window and opens the first page', async () => {
    const accounts = TestBed.inject(AccountsService);
    accounts.add({ server: current, accessToken: 'a0', refreshToken: 'r0', identity: 'someone' });
    const { component, page } = create();
    component.open();
    fill(page, { address: 'other.example:5065', email: ' New@B.org ', password: 'secret' });
    const server = `${location.protocol}//other.example:5065`;

    const done = page.submit();
    expect(page.saving()).toBe(true);
    http.expectOne(`${server}/health`).flush({ status: 'Healthy' });
    const login = await waitForRequest(`${server}/api/v1/auth/login`);
    expect(login.request.body).toEqual({ email: 'New@B.org', password: 'secret' });
    login.flush({ accessToken: 'access', refreshToken: 'refresh' });
    // Підключення перевіряється вже на сервері нового акаунта.
    const check = await waitForRequest(`${server}/health`);
    check.flush({ status: 'Healthy' });
    await done;

    expect(accounts.accounts()).toHaveLength(2);
    expect(accounts.active()).toMatchObject({ server, email: 'New@B.org', accessToken: 'access' });
    expect(closeModal).toHaveBeenCalled();
    expect(page.password()).toBe('');
    expect(page.saving()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
  });

  it('does not send the form twice while it is being sent', async () => {
    const { component, page } = create();
    component.open('a@b.org');
    fill(page, { password: 'secret' });

    void page.submit();
    void page.submit();
    expect(http.match(`${current}/health`)).toHaveLength(1);
  });
});
