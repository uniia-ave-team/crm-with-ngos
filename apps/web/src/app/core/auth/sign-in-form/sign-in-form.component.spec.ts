import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { AccountsService } from '../../accounts/accounts.service';
import { ConnectionService } from '../../connection/connection.service';
import { AfterSignIn, SignInFormComponent } from './sign-in-form.component';

interface FormInternals {
  address: { set(value: string): void };
  email: { set(value: string): void };
  password: { (): string; set(value: string): void };
  submit(): Promise<void>;
}

interface FormInputs {
  withServer?: boolean;
  accountId?: string | null;
  server?: string;
  email?: string;
  afterSignIn?: AfterSignIn;
  error?: string | null;
}

/** Сховище браузера переповнене: записи акаунтів не пишуться (решта, як-от мова, — пишеться). */
function fillStorageForAccounts(): void {
  const setItem = Storage.prototype.setItem;
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(function (this: Storage, key: string, value: string) {
    if (key.startsWith('yavir.account.')) {
      throw new DOMException('The quota has been exceeded.', 'QuotaExceededError');
    }
    setItem.call(this, key, value);
  });
}

describe('SignInFormComponent', () => {
  const server = 'https://first.example:5065';
  let http: HttpTestingController;
  let accounts: AccountsService;
  let current: string;
  let servers: (string | null)[];
  let attempts: number;

  beforeEach(() => {
    vi.useFakeTimers();
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      imports: [SignInFormComponent],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }), provideHttpClient(), provideHttpClientTesting()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('uk', uk);
    translate.use('uk');
    http = TestBed.inject(HttpTestingController);
    accounts = TestBed.inject(AccountsService);
    // Перша перевірка підключення, яку ConnectionService запускає сам при створенні, до форми не належить.
    const connection = TestBed.inject(ConnectionService);
    http.match(connection.apiHealthUrl());
    current = connection.serverUrl();
    servers = [];
    attempts = 0;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const health = (origin: string) => `${origin}/health`;
  const loginUrl = (origin: string) => `${origin}/api/v1/auth/login`;

  function open(inputs: FormInputs = {}): ComponentFixture<SignInFormComponent> {
    const fixture = TestBed.createComponent(SignInFormComponent);
    fixture.componentRef.setInput('withServer', inputs.withServer ?? false);
    fixture.componentRef.setInput('accountId', inputs.accountId ?? null);
    fixture.componentRef.setInput('initialServer', inputs.server ?? server);
    fixture.componentRef.setInput('initialEmail', inputs.email ?? '');
    fixture.componentRef.setInput('afterSignIn', inputs.afterSignIn ?? null);
    fixture.componentRef.setInput('error', inputs.error ?? null);
    fixture.componentInstance.serverChange.subscribe((origin) => servers.push(origin));
    fixture.componentInstance.attempt.subscribe(() => attempts++);
    fixture.detectChanges();
    return fixture;
  }

  const internals = (fixture: ComponentFixture<SignInFormComponent>) => fixture.componentInstance as unknown as FormInternals;
  const element = (fixture: ComponentFixture<SignInFormComponent>) => fixture.nativeElement as HTMLElement;
  const submitButton = (fixture: ComponentFixture<SignInFormComponent>) => element(fixture).querySelector<HTMLButtonElement>('.sign-in-form__submit')!;
  const fieldErrors = (fixture: ComponentFixture<SignInFormComponent>) =>
    Array.from(element(fixture).querySelectorAll('.sign-in-form__field-error')).map((e) => e.textContent!.trim());
  const banner = (fixture: ComponentFixture<SignInFormComponent>) => element(fixture).querySelector('.auth-error')?.textContent?.trim() ?? null;

  /** Проганяє таймери й чекає, поки відповіді потраплять у шаблон. */
  async function settle(fixture: ComponentFixture<SignInFormComponent>, ms = 0): Promise<void> {
    await vi.advanceTimersByTimeAsync(ms);
    TestBed.tick();
    fixture.detectChanges();
  }

  /** Форма з полем адреси, сервер якої вже відповів, що здоровий. */
  async function withHealthyServer(inputs: FormInputs = {}): Promise<ComponentFixture<SignInFormComponent>> {
    const fixture = open({ ...inputs, withServer: true });
    await settle(fixture);
    http.expectOne(health(inputs.server ?? server)).flush({ status: 'Healthy' });
    await settle(fixture);
    return fixture;
  }

  describe('a Cyrillic domain (the .укр zone)', () => {
    const stored = 'https://xn--80aikifvh.xn--j1amh:5065';
    const shownAddress = (fixture: ComponentFixture<SignInFormComponent>) => (fixture.componentInstance as unknown as { address(): string }).address();

    it('is written in the address field in Cyrillic, while the server is checked at the record the browser keeps', async () => {
      const fixture = await withHealthyServer({ server: stored });
      expect(shownAddress(fixture)).toBe('https://приклад.укр:5065');
      expect(servers.at(-1)).toBe(stored);
    });

    it('is taken when it is typed in Cyrillic', async () => {
      const fixture = await withHealthyServer();
      internals(fixture).address.set('приклад.укр:5065');
      await settle(fixture, 1000);
      const request = http.expectOne((r) => r.url.endsWith('/health'));
      expect(new URL(request.request.url).hostname).toBe('xn--80aikifvh.xn--j1amh');
      request.flush({ status: 'Healthy' });
      await settle(fixture);
      expect(fieldErrors(fixture)).toEqual([]);
    });
  });

  describe('the fields', () => {
    it('has the e-mail, the password, the button and the forgot-password link, with the texts of the sign-in form', () => {
      const fixture = open();
      const text = element(fixture).textContent!;
      expect(text).toContain(uk.login.form.email);
      expect(text).toContain(uk.login.form.password);
      expect(submitButton(fixture).textContent!.trim()).toBe(uk.login.form.submit);
      expect(element(fixture).querySelector('.sign-in-form__forgot')!.textContent!.trim()).toBe(uk.login.form.forgot);
    });

    it('has no field of the server address unless it is asked for', async () => {
      expect(element(open()).querySelector('.sign-in-form__address')).toBeNull();
      const fixture = await withHealthyServer();
      expect(element(fixture).querySelector('.sign-in-form__address')).not.toBeNull();
      expect(element(fixture).textContent).toContain(uk.login.step1.label);
    });

    it('gives every form its own ids, so that two forms on a page do not clash', () => {
      const ids = (fixture: ComponentFixture<SignInFormComponent>) =>
        Array.from(element(fixture).querySelectorAll('ion-input')).map((input) => input.id);
      const first = ids(open());
      const second = ids(open());
      expect(first).toHaveLength(2);
      expect(first.filter((id) => second.includes(id))).toEqual([]);
    });
  });

  describe('without the field of the server address (the server is already known)', () => {
    it('does not check any server, and the button is ready at once', async () => {
      const fixture = open();
      await settle(fixture, 600);
      http.expectNone(() => true);
      expect(submitButton(fixture).disabled).toBe(false);
      expect(servers).toEqual([]);
    });

    it('signs in on the current server', async () => {
      const fixture = open({ email: 'first@organization.org' });
      internals(fixture).password.set('secret');
      const submitting = internals(fixture).submit();
      const request = http.expectOne(loginUrl(current));
      expect(request.request.body).toEqual({ email: 'first@organization.org', password: 'secret' });
      request.flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      await submitting;
      expect(accounts.active()).toMatchObject({ server: current, email: 'first@organization.org' });
    });
  });

  describe('the server of the form', () => {
    it('is checked at once on opening; a healthy one enables the button and is reported', async () => {
      const fixture = open({ withServer: true });
      expect(submitButton(fixture).disabled).toBe(true);
      await settle(fixture);
      http.expectOne(health(server)).flush({ status: 'Healthy' });
      await settle(fixture);

      expect(submitButton(fixture).disabled).toBe(false);
      expect(fieldErrors(fixture)).toEqual([]);
      expect(servers).toEqual([server]);
    });

    it('writes under the field that the server is unavailable, keeps the button disabled and reports no server', async () => {
      const fixture = open({ withServer: true });
      await settle(fixture);
      http.expectOne(health(server)).error(new ProgressEvent('error'));
      await settle(fixture);

      expect(fieldErrors(fixture)).toEqual([uk.login.step1.error.unreachable.replace('{{url}}', server)]);
      expect(submitButton(fixture).disabled).toBe(true);
      expect(servers).toEqual([null]);
    });

    it('is checked again after every change, once the typing pauses', async () => {
      const fixture = await withHealthyServer();
      servers.length = 0;

      internals(fixture).address.set('https://second.example:5065');
      await settle(fixture, 100);
      internals(fixture).address.set('https://third.example:5065');
      // Пауза ще не минула: запитів нема, а кнопка вже вимкнена.
      await settle(fixture, 100);
      http.expectNone(() => true);
      expect(submitButton(fixture).disabled).toBe(true);

      await settle(fixture, 600);
      http.expectNone(health('https://second.example:5065'));
      http.expectOne(health('https://third.example:5065')).flush({ status: 'Healthy' });
      await settle(fixture);

      expect(servers).toEqual(['https://third.example:5065']);
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('drops the answer for an address that was changed while the request was running', async () => {
      const fixture = open({ withServer: true });
      await settle(fixture);
      const stale = http.expectOne(health(server));

      internals(fixture).address.set('https://second.example:5065');
      await settle(fixture, 600);
      stale.flush({ status: 'Healthy' });
      await settle(fixture);
      // Відповідь для старої адреси ні на що не впливає.
      expect(servers).toEqual([]);
      expect(submitButton(fixture).disabled).toBe(true);

      http.expectOne(health('https://second.example:5065')).error(new ProgressEvent('error'));
      await settle(fixture);
      expect(servers).toEqual([null]);
    });

    it('is not asked at all when the address does not look like an address: the text under the field', async () => {
      const fixture = open({ withServer: true, server: 'not an address!' });
      await settle(fixture, 600);
      http.expectNone(() => true);
      expect(fieldErrors(fixture)).toEqual([uk.login.step1.error.invalid]);
      expect(submitButton(fixture).disabled).toBe(true);
      expect(servers).toEqual([null]);
    });

    it('stops checking when the form is gone', async () => {
      const fixture = await withHealthyServer();
      internals(fixture).address.set('https://second.example:5065');
      fixture.destroy();
      await vi.advanceTimersByTimeAsync(600);
      http.expectNone(() => true);
    });
  });

  describe('signing in', () => {
    it('asks for the e-mail and the password first, sends nothing and reports no attempt', async () => {
      const fixture = await withHealthyServer();
      await internals(fixture).submit();
      fixture.detectChanges();
      expect(fieldErrors(fixture)).toEqual([uk.login.form.error.emailRequired, uk.login.form.error.passwordRequired]);
      http.expectNone(loginUrl(server));
      expect(attempts).toBe(0);
    });

    it('sends nothing while the server is not known to be healthy', async () => {
      const fixture = open({ withServer: true, email: 'first@organization.org' });
      internals(fixture).password.set('secret');
      await internals(fixture).submit();
      await settle(fixture);
      http.expectNone(loginUrl(server));
    });

    it('logs in on the server of the form with the trimmed e-mail, makes the account active and forgets the password', async () => {
      const fixture = await withHealthyServer({ email: ' first@organization.org ' });
      internals(fixture).password.set('secret');

      const submitting = internals(fixture).submit();
      const request = http.expectOne(loginUrl(server));
      expect(request.request.body).toEqual({ email: 'first@organization.org', password: 'secret' });
      expect(attempts).toBe(1);
      request.flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      await submitting;

      expect(accounts.active()).toMatchObject({ server, email: 'first@organization.org' });
      expect(internals(fixture).password()).toBe('');
    });

    it('then does what the host asked for, staying busy until it is done', async () => {
      let finish: () => void = () => undefined;
      const afterSignIn = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)));
      const fixture = await withHealthyServer({ email: 'first@organization.org', afterSignIn });
      internals(fixture).password.set('secret');

      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl(server)).flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      await settle(fixture);
      expect(afterSignIn).toHaveBeenCalledTimes(1);
      expect(submitButton(fixture).disabled).toBe(true);
      expect(submitButton(fixture).textContent!.trim()).toBe(uk.login.form.saving);

      finish();
      await submitting;
      fixture.detectChanges();
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('is not left busy when what the host asked for fails', async () => {
      const afterSignIn = vi.fn(() => Promise.reject(new Error('broken')));
      const fixture = await withHealthyServer({ email: 'first@organization.org', afterSignIn });
      internals(fixture).password.set('secret');

      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl(server)).flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      await expect(submitting).rejects.toThrow('broken');
      fixture.detectChanges();
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('does not send the form twice while it is being sent', async () => {
      const fixture = await withHealthyServer({ email: 'first@organization.org' });
      internals(fixture).password.set('secret');
      const first = internals(fixture).submit();
      const second = internals(fixture).submit();
      http.expectOne(loginUrl(server)).flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      await Promise.all([first, second]);
    });

    it('overwrites the saved account when the e-mail is the same, and adds a new one when it is not', async () => {
      const saved = accounts.add({ server, accessToken: 'old', refreshToken: 'old', identity: 'user-1', email: 'first@organization.org' });
      accounts.update(saved.id, { outdated: true });
      accounts.setActive(null);

      const same = await withHealthyServer({ accountId: saved.id, email: 'First@Organization.org' });
      internals(same).password.set('secret');
      const first = internals(same).submit();
      http.expectOne(loginUrl(server)).flush({ accessToken: 'a.b.c', refreshToken: 'r1' });
      await first;
      expect(accounts.accounts()).toHaveLength(1);
      expect(accounts.accounts()[0]).toMatchObject({ id: saved.id, outdated: false, refreshToken: 'r1' });
      same.destroy();

      accounts.setActive(null);
      const other = await withHealthyServer({ accountId: saved.id, email: 'other@organization.org' });
      internals(other).password.set('secret');
      const second = internals(other).submit();
      http.expectOne(loginUrl(server)).flush({ accessToken: 'd.e.f', refreshToken: 'r2' });
      await second;
      expect(accounts.accounts()).toHaveLength(2);
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [500, 'error'],
    ])('keeps the form, tells about the error above the fields and calls nothing when the server answers HTTP %i', async (status, key) => {
      const afterSignIn = vi.fn(async () => undefined);
      const fixture = await withHealthyServer({ email: 'first@organization.org', afterSignIn });
      internals(fixture).password.set('wrong');

      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl(server)).flush({}, { status, statusText: 'x' });
      await submitting;
      fixture.detectChanges();

      expect(banner(fixture)).toBe(uk.login.form.error[key as 'invalid']);
      expect(afterSignIn).not.toHaveBeenCalled();
      expect(accounts.accounts()).toHaveLength(0);
      // Ще раз можна надіслати: кнопка знову активна.
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('tells that the server does not answer when the request fails on the network', async () => {
      const fixture = await withHealthyServer({ email: 'first@organization.org' });
      internals(fixture).password.set('secret');
      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl(server)).error(new ProgressEvent('error'));
      await submitting;
      fixture.detectChanges();
      expect(banner(fixture)).toBe(uk.login.form.error.unreachable);
    });

    it('takes its own error away when a new attempt starts', async () => {
      const fixture = await withHealthyServer({ email: 'first@organization.org' });
      internals(fixture).password.set('wrong');
      const failing = internals(fixture).submit();
      http.expectOne(loginUrl(server)).flush({}, { status: 401, statusText: 'x' });
      await failing;
      fixture.detectChanges();
      expect(banner(fixture)).not.toBeNull();

      void internals(fixture).submit();
      fixture.detectChanges();
      expect(banner(fixture)).toBeNull();
      http.expectOne(loginUrl(server));
    });
  });

  describe('the error of the host', () => {
    it('is shown above the fields when the form has no error of its own', () => {
      const fixture = open({ error: 'login.form.error.statusFailed' });
      expect(banner(fixture)).toBe(uk.login.form.error.statusFailed);
    });

    it('says the browser cannot store the account when its storage is full, and stays on the form', async () => {
      const afterSignIn = vi.fn(() => Promise.resolve());
      const fixture = open({ email: 'first@organization.org', afterSignIn });
      fillStorageForAccounts();
      internals(fixture).password.set('secret');
      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl(current)).flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      await submitting;
      fixture.detectChanges();

      expect(banner(fixture)).toBe(uk.login.form.error.storageFull);
      expect(afterSignIn).not.toHaveBeenCalled();
      expect(submitButton(fixture).disabled).toBe(false);
      http.match(() => true);
    });

    it('gives way to the error of the sign-in itself', async () => {
      const fixture = open({ email: 'first@organization.org', error: 'login.form.error.statusFailed' });
      internals(fixture).password.set('wrong');
      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl(current)).flush({}, { status: 401, statusText: 'x' });
      await submitting;
      fixture.detectChanges();
      expect(banner(fixture)).toBe(uk.login.form.error.unauthorized);
    });
  });
});
