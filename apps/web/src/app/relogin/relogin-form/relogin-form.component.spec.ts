import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../../core/accounts/accounts.service';
import { ConnectionService } from '../../core/connection/connection.service';
import { DEFAULT_LOGIN_IMAGE } from '../../core/login-page-images/login-page-images.service';
import { ReloginPageModule } from '../relogin.module';
import { ReloginFormComponent } from './relogin-form.component';

interface FormInternals {
  address: { set(value: string): void };
  email: { set(value: string): void };
  password: { set(value: string): void };
  submit(): Promise<void>;
}

describe('ReloginFormComponent', () => {
  const server = 'https://first.example:5065';
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let accounts: AccountsService;
  let images: string[];
  let outputs: string[];

  beforeEach(() => {
    vi.useFakeTimers();
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      imports: [ReloginPageModule],
      providers: [provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }), provideHttpClient(), provideHttpClientTesting()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('uk', uk);
    translate.use('uk');
    http = TestBed.inject(HttpTestingController);
    accounts = TestBed.inject(AccountsService);
    // Перша перевірка підключення, яку ConnectionService запускає сам при створенні, до форми не належить.
    http.match(TestBed.inject(ConnectionService).apiHealthUrl());
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    images = [];
    outputs = [];
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const health = (origin: string) => `${origin}/health`;
  const randomImage = (origin: string) => `${origin}/api/v1/loginpageimages/random`;

  function open(inputs: { accountId?: string | null; server?: string; email?: string } = {}): ComponentFixture<ReloginFormComponent> {
    const fixture = TestBed.createComponent(ReloginFormComponent);
    fixture.componentRef.setInput('accountId', inputs.accountId ?? null);
    fixture.componentRef.setInput('initialServer', inputs.server ?? server);
    fixture.componentRef.setInput('initialEmail', inputs.email ?? '');
    fixture.componentInstance.imageChange.subscribe((url) => images.push(url));
    fixture.componentInstance.back.subscribe(() => outputs.push('back'));
    fixture.componentInstance.remove.subscribe(() => outputs.push('remove'));
    fixture.detectChanges();
    return fixture;
  }

  const internals = (fixture: ComponentFixture<ReloginFormComponent>) => fixture.componentInstance as unknown as FormInternals;
  const element = (fixture: ComponentFixture<ReloginFormComponent>) => fixture.nativeElement as HTMLElement;
  const submitButton = (fixture: ComponentFixture<ReloginFormComponent>) => element(fixture).querySelector<HTMLButtonElement>('.relogin-form__submit')!;
  const fieldError = (fixture: ComponentFixture<ReloginFormComponent>) => element(fixture).querySelector('.relogin-form__field-error')?.textContent?.trim() ?? null;
  const banner = (fixture: ComponentFixture<ReloginFormComponent>) => element(fixture).querySelector('.relogin-form__error')?.textContent?.trim() ?? null;

  /** Проганяє таймери й чекає, поки відповіді потраплять у шаблон. */
  async function settle(fixture: ComponentFixture<ReloginFormComponent>, ms = 0): Promise<void> {
    await vi.advanceTimersByTimeAsync(ms);
    TestBed.tick();
    fixture.detectChanges();
  }

  /** Сервер здоровий, а зображення з нього — `image` (або його нема). */
  async function serverHealthy(fixture: ComponentFixture<ReloginFormComponent>, origin = server, image: object | null = null): Promise<void> {
    await settle(fixture);
    http.expectOne(health(origin)).flush({ status: 'Healthy' });
    await settle(fixture);
    const request = http.expectOne(randomImage(origin));
    if (image) {
      request.flush(image);
    } else {
      request.flush({}, { status: 404, statusText: 'Not Found' });
    }
    await settle(fixture);
  }

  describe('the title row', () => {
    it('has the title, the back button and the trash button for a saved account', async () => {
      const fixture = open({ accountId: 'a' });
      expect(element(fixture).querySelector('.relogin-form__title')!.textContent!.trim()).toBe(uk.relogin.form.title);
      expect(element(fixture).querySelector('.relogin-form__back')!.textContent!.trim()).toBe(uk.common.back);
      const trash = element(fixture).querySelector<HTMLElement>('.relogin-form__remove')!;
      expect(trash.querySelector('ion-icon')!.getAttribute('name')).toBe('trash-outline');
      expect(trash.getAttribute('title')).toBe(uk.nav.account.remove);
      expect(trash.getAttribute('aria-label')).toBe(uk.nav.account.remove);
      await serverHealthy(fixture);
    });

    it('has no trash button when an account is being added', async () => {
      const fixture = open({ accountId: null });
      expect(element(fixture).querySelector('.relogin-form__remove')).toBeNull();
      await serverHealthy(fixture);
    });

    it('reports the clicks on the back and the trash buttons', async () => {
      const fixture = open({ accountId: 'a' });
      element(fixture).querySelector<HTMLElement>('.relogin-form__back')!.click();
      element(fixture).querySelector<HTMLElement>('.relogin-form__remove')!.click();
      expect(outputs).toEqual(['back', 'remove']);
      await serverHealthy(fixture);
    });
  });

  describe('the server of the form', () => {
    it('is checked at once on opening, and the image of the server goes to the page when it is healthy', async () => {
      const fixture = open({ email: 'first@organization.org' });
      expect(submitButton(fixture).disabled).toBe(true);

      await serverHealthy(fixture, server, { id: 'abc', url: '/files/login/abc.jpg' });

      expect(images).toEqual([`${server}/files/login/abc.jpg`]);
      expect(submitButton(fixture).disabled).toBe(false);
      expect(fieldError(fixture)).toBeNull();
    });

    it('is the default image when the server has none', async () => {
      const fixture = open();
      await serverHealthy(fixture);
      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('writes under the field that the server is unavailable, takes the default image and keeps the button disabled', async () => {
      const fixture = open();
      await settle(fixture);
      http.expectOne(health(server)).error(new ProgressEvent('error'));
      await settle(fixture);

      expect(fieldError(fixture)).toBe(uk.login.step1.error.unreachable.replace('{{url}}', server));
      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
      expect(submitButton(fixture).disabled).toBe(true);
      http.expectNone(randomImage(server));
    });

    it('is checked again after every change, once the typing pauses, and the old answer is dropped', async () => {
      const fixture = open();
      await serverHealthy(fixture);
      images.length = 0;

      internals(fixture).address.set('https://second.example:5065');
      await settle(fixture, 100);
      internals(fixture).address.set('https://third.example:5065');
      // Пауза ще не минула: запитів нема.
      await settle(fixture, 100);
      http.expectNone(() => true);
      expect(submitButton(fixture).disabled).toBe(true);

      await settle(fixture, 600);
      http.expectNone(health('https://second.example:5065'));
      http.expectOne(health('https://third.example:5065')).flush({ status: 'Healthy' });
      await settle(fixture);
      http.expectOne(randomImage('https://third.example:5065')).flush({}, { status: 404, statusText: 'Not Found' });
      await settle(fixture);

      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('drops the answer for an address that was changed while the request was running', async () => {
      const fixture = open();
      await settle(fixture);
      const stale = http.expectOne(health(server));

      internals(fixture).address.set('https://second.example:5065');
      await settle(fixture, 600);
      stale.flush({ status: 'Healthy' });
      await settle(fixture);
      // Відповідь для старої адреси ні на що не впливає: зображення зі старого сервера не просять.
      http.expectNone(randomImage(server));

      http.expectOne(health('https://second.example:5065')).error(new ProgressEvent('error'));
      await settle(fixture);
      expect(submitButton(fixture).disabled).toBe(true);
    });

    it('is not asked at all when the address does not look like an address: the text under the field and the default image', async () => {
      const fixture = open({ server: 'not an address!' });
      await settle(fixture, 600);
      http.expectNone(() => true);
      expect(fieldError(fixture)).toBe(uk.login.step1.error.invalid);
      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
      expect(submitButton(fixture).disabled).toBe(true);
    });
  });

  describe('signing in', () => {
    const loginUrl = (origin = server) => `${origin}/api/v1/auth/login`;

    async function ready(inputs: { accountId?: string | null; email?: string } = {}) {
      const fixture = open(inputs);
      await serverHealthy(fixture);
      return fixture;
    }

    it('asks for the e-mail and the password first, and sends nothing', async () => {
      const fixture = await ready();
      await internals(fixture).submit();
      fixture.detectChanges();
      expect(Array.from(element(fixture).querySelectorAll('.relogin-form__field-error')).map((e) => e.textContent!.trim())).toEqual([
        uk.login.form.error.emailRequired,
        uk.login.form.error.passwordRequired,
      ]);
      http.expectNone(loginUrl());
    });

    it('sends nothing while the server is not known to be healthy', async () => {
      const fixture = open({ email: 'first@organization.org' });
      internals(fixture).password.set('secret');
      await internals(fixture).submit();
      await settle(fixture);
      http.expectNone(loginUrl());
    });

    it('logs in on the server of the form, makes the account active and opens the planner again', async () => {
      const fixture = await ready({ email: 'first@organization.org' });
      internals(fixture).password.set('secret');

      const submitting = internals(fixture).submit();
      const request = http.expectOne(loginUrl());
      expect(request.request.body).toEqual({ email: 'first@organization.org', password: 'secret' });
      request.flush({ accessToken: 'a.b.c', refreshToken: 'r' });
      // Далі підключення перевіряється вже на сервері нового акаунта.
      await settle(fixture);
      http.expectOne(health(server)).flush({ status: 'Healthy' });
      await submitting;

      expect(accounts.active()).toMatchObject({ server, email: 'first@organization.org' });
      expect(navigate).toHaveBeenCalledWith('/planner', { onSameUrlNavigation: 'reload' });
    });

    it('overwrites the saved account when the e-mail is the same, and adds a new one when it is not', async () => {
      const saved = accounts.add({ server, accessToken: 'old', refreshToken: 'old', identity: 'user-1', email: 'first@organization.org' });
      accounts.update(saved.id, { outdated: true });
      accounts.setActive(null);

      const same = await ready({ accountId: saved.id, email: 'First@Organization.org' });
      internals(same).password.set('secret');
      const first = internals(same).submit();
      http.expectOne(loginUrl()).flush({ accessToken: 'a.b.c', refreshToken: 'r1' });
      await settle(same);
      http.expectOne(health(server)).flush({ status: 'Healthy' });
      await first;
      expect(accounts.accounts()).toHaveLength(1);
      expect(accounts.accounts()[0]).toMatchObject({ id: saved.id, outdated: false, refreshToken: 'r1' });
      same.destroy();

      accounts.setActive(null);
      const other = await ready({ accountId: saved.id, email: 'other@organization.org' });
      internals(other).password.set('secret');
      const second = internals(other).submit();
      http.expectOne(loginUrl()).flush({ accessToken: 'd.e.f', refreshToken: 'r2' });
      await settle(other);
      http.expectOne(health(server)).flush({ status: 'Healthy' });
      await second;
      expect(accounts.accounts()).toHaveLength(2);
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [500, 'error'],
    ])('keeps the form open and tells about the error when the server answers HTTP %i', async (status, key) => {
      const fixture = await ready({ email: 'first@organization.org' });
      internals(fixture).password.set('wrong');

      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl()).flush({}, { status, statusText: 'x' });
      await submitting;
      fixture.detectChanges();

      expect(banner(fixture)).toBe(uk.login.form.error[key as 'invalid']);
      expect(navigate).not.toHaveBeenCalled();
      expect(accounts.accounts()).toHaveLength(0);
      // Ще раз можна надіслати: кнопка знову активна.
      expect(submitButton(fixture).disabled).toBe(false);
    });

    it('tells that the server does not answer when the request fails on the network', async () => {
      const fixture = await ready({ email: 'first@organization.org' });
      internals(fixture).password.set('secret');
      const submitting = internals(fixture).submit();
      http.expectOne(loginUrl()).error(new ProgressEvent('error'));
      await submitting;
      fixture.detectChanges();
      expect(banner(fixture)).toBe(uk.login.form.error.unreachable);
    });
  });
});
