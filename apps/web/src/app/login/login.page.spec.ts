import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { AuthService } from '../core/auth/auth.service';
import { ConnectionService } from '../core/connection/connection.service';
import { LanguageService } from '../core/i18n/language.service';
import { LoginPage } from './login.page';

// Приватне/protected API компонента, яке в шаблоні викликають події.
interface LoginPageInternals {
  onNgoNameChange(value: string): void;
  onAddressChange(value: string): void;
  submit(): void;
}

describe('LoginPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    });
    // Тексти сторінки живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  // Сторінка інжектить ConnectionService, а той одразу шле перший запит.
  function create() {
    const fixture = TestBed.createComponent(LoginPage);
    const connection = TestBed.inject(ConnectionService);
    fixture.detectChanges();
    const request = http.expectOne(connection.apiHealthUrl());
    const element = fixture.nativeElement as HTMLElement;
    const page = fixture.componentInstance as unknown as LoginPageInternals;
    return { fixture, connection, element, page, request };
  }

  /** Підключення є — відповідаємо на запит статусу системи. */
  function answerStatus(
    connection: ConnectionService,
    fixture: { detectChanges(): void },
    status = { hasAdmin: true, hasNgo: true, isSetupComplete: true },
  ): void {
    fixture.detectChanges();
    http.expectOne(`${connection.serverUrl()}/api/v1/system/status`).flush(status);
    fixture.detectChanges();
  }

  /** Токени, збережені для сервера за доменом сторінки (як після попереднього входу). */
  function storeTokens(): void {
    const server = `${location.protocol}//${location.hostname}:5065`;
    localStorage.setItem(`yavir.accessToken.${server}`, 'old-access');
    localStorage.setItem(`yavir.refreshToken.${server}`, 'old-refresh');
  }

  function submitAddress(page: LoginPageInternals, address: string): void {
    page.onAddressChange(address);
    page.submit();
  }

  it('should create', () => {
    const { fixture } = create();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows a loading state while the connection is being checked', () => {
    const { element } = create();
    expect(element.textContent).toContain('Перевіряємо підключення');
    expect(element.textContent).not.toContain('Введіть адресу серверу');
  });

  it('shows step 1 without an error when no API is found at the page domain', () => {
    const { fixture, element, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    expect(element.textContent).toContain('Введіть адресу серверу');
    expect(element.querySelector('.login__error')).toBeNull();
  });

  it('shows the login stub when the API is up', () => {
    const { fixture, connection, element, request } = create();
    request.flush({ status: 'Healthy' });
    answerStatus(connection, fixture);

    expect(element.textContent).toContain('Вхід в систему');
    expect(element.textContent).not.toContain('Введіть адресу серверу');
  });

  it('re-runs the check with the provided address and shows an error above the form if no API is found', () => {
    const { fixture, element, page, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    submitAddress(page, '192.168.0.9');
    const retry = http.expectOne(`${location.protocol}//192.168.0.9:5065/health`);
    retry.error(new ProgressEvent('error'));
    fixture.detectChanges();

    const error = element.querySelector('.login__error');
    const card = element.querySelector('.login__card');
    expect(error?.textContent).toContain('не знайдено');
    expect(error?.textContent).toContain(`${location.protocol}//192.168.0.9:5065`);
    expect(card?.textContent).toContain('Введіть адресу серверу');
    // Помилка стоїть саме над формою.
    expect(error!.compareDocumentPosition(card!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('tells apart an API that answers but is not healthy', () => {
    const { fixture, element, page, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    submitAddress(page, '192.168.0.9');
    http
      .expectOne(`${location.protocol}//192.168.0.9:5065/health`)
      .flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(element.querySelector('.login__error')?.textContent).toContain('невідома нам система');
  });

  it('rejects an address that does not look like a server without any request', () => {
    const { fixture, element, page, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    submitAddress(page, 'http://');
    fixture.detectChanges();

    expect(element.querySelector('.login__error')?.textContent).toContain('Це не схоже на адресу сервера');
    http.expectNone(() => true);
  });

  it('moves on to the login stub once the provided address answers', () => {
    const { fixture, connection, element, page, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    submitAddress(page, '192.168.0.9');
    http.expectOne(`${location.protocol}//192.168.0.9:5065/health`).flush({ status: 'Healthy' });
    answerStatus(connection, fixture);

    expect(element.querySelector('.login__error')).toBeNull();
  });

  it('requests the system status once the login form is shown, and not before', () => {
    const { fixture, connection, request } = create();
    // Поки йде перевірка — статус системи не запитуємо.
    http.expectNone(`${connection.serverUrl()}/api/v1/system/status`);

    request.flush({ status: 'Healthy' });
    fixture.detectChanges();

    const status = http.expectOne(`${connection.serverUrl()}/api/v1/system/status`);
    status.flush({ hasAdmin: true, hasNgo: true, isSetupComplete: true });
    expect(connection.systemStatus()).toEqual({ hasAdmin: true, hasNgo: true, isSetupComplete: true });
  });

  it('does not request the system status while step 1 is shown', () => {
    const { fixture, connection, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    http.expectNone(`${connection.serverUrl()}/api/v1/system/status`);
  });

  it('explains a redirect caused by a missing connection even though no address was provided yet', () => {
    const { fixture, element, request } = create();
    fixture.componentRef.setInput('error', 'no-connection');
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();

    const error = element.querySelector('.login__error');
    expect(error?.textContent).toContain('Немає підключення до сервера');
    expect(element.querySelector('.login__card')?.textContent).toContain('Введіть адресу серверу');
  });

  it('shows no redirect error when /login was simply opened and no API was found', () => {
    const { fixture, element, request } = create();
    request.error(new ProgressEvent('error'));
    fixture.detectChanges();
    expect(element.querySelector('.login__error')).toBeNull();
  });

  describe('login form visual', () => {
    function createLoginForm() {
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture);
      return ctx;
    }

    const srcs = (element: HTMLElement) =>
      Array.from(element.querySelectorAll('.login-form img')).map((i) => i.getAttribute('src'));

    it('shows the side image, the title, the fields, the button and the forgot-password link', () => {
      const { element } = createLoginForm();
      expect(srcs(element)).toContain('assets/login_image.jpg');
      expect(element.querySelector('.login-form__title')?.textContent).toContain('Вхід в систему');
      expect(element.textContent).toContain('Електронна пошта');
      expect(element.querySelectorAll('.login-form ion-input')).toHaveLength(2);
      expect(element.querySelector('.login-form__submit')?.textContent).toContain('Ввійти');
      expect(element.querySelector('.login-form__forgot')?.textContent).toContain('Забули пароль?');
    });

    it('uses the mobile menu logo of the language for both themes', () => {
      TestBed.inject(LanguageService).setLanguage('uk');
      const { element } = createLoginForm();
      expect(srcs(element)).toContain('assets/logo_mobile_ukrainian.svg');
      expect(srcs(element)).toContain('assets/logo_mobile_ukrainian_dark.svg');
    });

    it.each([
      ['uk', 'Uniia'],
      ['crh', 'Uniia'],
      ['be', 'Uniia'],
      ['en', 'Uniia_en'],
    ] as const)('uses the %s Uniia logo file %s', (language, file) => {
      TestBed.inject(LanguageService).setLanguage(language);
      const { element } = createLoginForm();
      expect(srcs(element)).toContain(`assets/logos/${file}.svg`);
      expect(srcs(element)).toContain(`assets/logos_white/${file}.svg`);
    });

    it('shows the partner logos in both themes', () => {
      const { element } = createLoginForm();
      for (const logo of ['Ave_Team.svg', 'NED.png', 'lets_NGO.png']) {
        expect(srcs(element)).toContain(`assets/logos/${logo}`);
        expect(srcs(element)).toContain(`assets/logos_white/${logo}`);
      }
      expect(srcs(element)).toContain('assets/logos/pd.png');
      expect(srcs(element)).toContain('assets/logos_white/PD.png');
    });
  });

  describe('change server button', () => {
    const button = (element: HTMLElement) => element.querySelector<HTMLButtonElement>('.login__server-button');

    function createWithStatus(status: { hasAdmin: boolean; hasNgo: boolean; isSetupComplete: boolean }, tokens: boolean) {
      if (tokens) {
        storeTokens();
      }
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, status);
      return ctx;
    }

    it.each([
      ['the login form', { hasAdmin: true, hasNgo: true, isSetupComplete: true }, false],
      ['the NGO form', { hasAdmin: true, hasNgo: false, isSetupComplete: false }, true],
      ['the admin form', { hasAdmin: false, hasNgo: false, isSetupComplete: false }, false],
    ])('is in the top-right corner of %s', (_name, status, tokens) => {
      const { element } = createWithStatus(status, tokens);
      const server = button(element)!;
      expect(server).not.toBeNull();
      expect(server.querySelector('ion-icon')?.getAttribute('name')).toBe('server-outline');
      expect(server.getAttribute('aria-label')).toBe('Змінити адресу сервера');
      expect(server.title).toBe('Змінити адресу сервера');
      expect(server.getAttribute('tabindex')).toBe('0');
    });

    it('is not shown on step 1 itself', () => {
      const { fixture, element, request } = create();
      request.error(new ProgressEvent('error'));
      fixture.detectChanges();
      expect(button(element)).toBeNull();
    });

    const healthUrl = () => `${location.protocol}//${location.hostname}:5065/health`;

    /** Натискає кнопку й відповідає на одноразову перевірку поточного сервера. */
    async function clickAndAnswerProbe(ctx: ReturnType<typeof createWithStatus>, answer: 'healthy' | 'down') {
      button(ctx.element)!.click();
      ctx.fixture.detectChanges();
      const probe = http.expectOne(healthUrl());
      if (answer === 'healthy') {
        probe.flush({ status: 'Healthy' });
      } else {
        probe.error(new ProgressEvent('error'));
      }
      await vi.waitFor(() => {
        ctx.fixture.detectChanges();
        expect(ctx.element.querySelector('.login__card')?.textContent).toContain('Введіть адресу серверу');
      });
    }

    it('checks the current server first and leaves everything untouched while it waits', () => {
      localStorage.setItem('yavir.unrelated', 'value');
      // Форма ГО (є токени), а не вхід: з токенами й повним статусом сторінка йде на оновлення сесії.
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: false, isSetupComplete: false }, true);
      button(ctx.element)!.click();
      ctx.fixture.detectChanges();

      const probe = http.expectOne(healthUrl());
      expect(probe.request.method).toBe('GET');
      expect(localStorage.getItem('yavir.unrelated')).toBe('value');
      expect(localStorage.length).toBeGreaterThan(1);
      expect(button(ctx.element)!.disabled).toBe(true);
      // Сторінка лишається такою, як була, доки йде перевірка.
      expect(ctx.element.querySelector('.login__card')?.textContent).toContain('Створіть свою організацію');
      probe.flush({ status: 'Healthy' });
    });

    it('wipes the whole localStorage and goes back to the server address step', async () => {
      localStorage.setItem('yavir.unrelated', 'value');
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: false, isSetupComplete: false }, true);

      await clickAndAnswerProbe(ctx, 'healthy');

      expect(localStorage.length).toBe(0);
      expect(TestBed.inject(AuthService).hasTokens()).toBe(false);
      expect(ctx.element.querySelector('.login__card')?.textContent).toContain('Введіть адресу серверу');
      expect(button(ctx.element)).toBeNull();
    });

    it('puts the current page domain into the address field when the server is healthy', async () => {
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: true, isSetupComplete: true }, false);
      await clickAndAnswerProbe(ctx, 'healthy');
      const input = ctx.element.querySelector('.login__card ion-input') as HTMLIonInputElement;
      expect(input.value).toBe(location.hostname);
    });

    it('puts the saved server address into the field when the server is healthy', async () => {
      localStorage.setItem('yavir.serverAddress', '192.168.0.9');
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, { hasAdmin: true, hasNgo: true, isSetupComplete: true });

      button(ctx.element)!.click();
      http.expectOne(`${location.protocol}//192.168.0.9:5065/health`).flush({ status: 'Healthy' });
      await vi.waitFor(() => {
        ctx.fixture.detectChanges();
        expect(ctx.element.querySelector('.login-form')).toBeNull();
      });
      const input = ctx.element.querySelector('.login__card ion-input') as HTMLIonInputElement;
      expect(input.value).toBe('192.168.0.9');
    });

    it('leaves the address field empty when the server does not answer', async () => {
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: true, isSetupComplete: true }, false);
      await clickAndAnswerProbe(ctx, 'down');
      expect(localStorage.length).toBe(0);
      const input = ctx.element.querySelector('.login__card ion-input') as HTMLIonInputElement;
      expect(input.value).toBe('');
    });

    it('forgets what was typed into the forms', async () => {
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: true, isSetupComplete: true }, false);
      const form = ctx.fixture.componentInstance as unknown as {
        loginEmail: () => string;
        onFieldChange(field: unknown, value: string): void;
      };
      form.onFieldChange(form.loginEmail, 'a@b.org');
      expect(form.loginEmail()).toBe('a@b.org');

      await clickAndAnswerProbe(ctx, 'healthy');
      expect(form.loginEmail()).toBe('');
    });

    it('stays on step 1 while the server is fine, and moves on once a new address is submitted', async () => {
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: true, isSetupComplete: true }, false);
      await clickAndAnswerProbe(ctx, 'healthy');
      // Скидання адреси запускає нову перевірку за доменом сторінки.
      http.expectOne(healthUrl()).flush({ status: 'Healthy' });
      ctx.fixture.detectChanges();
      expect(ctx.element.querySelector('.login__card')?.textContent).toContain('Введіть адресу серверу');

      const page = ctx.fixture.componentInstance as unknown as LoginPageInternals;
      page.onAddressChange('192.168.0.9');
      page.submit();
      http.expectOne(`${location.protocol}//192.168.0.9:5065/health`).flush({ status: 'Healthy' });
      ctx.fixture.detectChanges();
      http.expectOne(`${location.protocol}//192.168.0.9:5065/api/v1/system/status`).flush({
        hasAdmin: true,
        hasNgo: true,
        isSetupComplete: true,
      });
      ctx.fixture.detectChanges();
      expect(ctx.element.querySelector('.login-form')).not.toBeNull();
    });

    it('rejects an invalid address without leaving step 1', async () => {
      const ctx = createWithStatus({ hasAdmin: true, hasNgo: true, isSetupComplete: true }, false);
      await clickAndAnswerProbe(ctx, 'healthy');
      http.expectOne(healthUrl()).flush({ status: 'Healthy' });
      const page = ctx.fixture.componentInstance as unknown as LoginPageInternals;
      page.onAddressChange('http://');
      page.submit();
      ctx.fixture.detectChanges();
      expect(ctx.element.querySelector('.login__error')?.textContent).toContain('Це не схоже на адресу сервера');
      expect(ctx.element.querySelector('.login-form')).toBeNull();
    });
  });

  describe('login form submit', () => {
    interface LoginFormInternals {
      onFieldChange(field: unknown, value: string): void;
      submitLogin(): Promise<void>;
      loginEmail: unknown;
      loginPassword: unknown;
    }

    function createLoginForm() {
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture);
      const form = ctx.fixture.componentInstance as unknown as LoginFormInternals;
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      const fill = (email: string, password: string) => {
        form.onFieldChange(form.loginEmail, email);
        form.onFieldChange(form.loginPassword, password);
      };
      return { ...ctx, form, navigate, fill };
    }

    const loginUrl = (connection: ConnectionService) => `${connection.serverUrl()}/api/v1/auth/login`;
    const statusUrl = (connection: ConnectionService) => `${connection.serverUrl()}/api/v1/system/status`;
    const tokens = { accessToken: 'access', refreshToken: 'refresh' };

    it('requires both fields and sends no request without them', () => {
      const { fixture, element, form } = createLoginForm();
      void form.submitLogin();
      fixture.detectChanges();
      const errors = Array.from(element.querySelectorAll('.login-form__field-error')).map((e) => e.textContent?.trim());
      expect(errors).toEqual(['Введіть пошту.', 'Введіть пароль.']);
      http.expectNone(() => true);
    });

    it('logs in, keeps the tokens and goes to the planner when the NGO exists', async () => {
      const { connection, form, navigate, fill } = createLoginForm();
      fill(' a@b.org ', 'Abcde1!x');
      const done = form.submitLogin();

      const login = http.expectOne(loginUrl(connection));
      expect(login.request.body).toEqual({ email: 'a@b.org', password: 'Abcde1!x' });
      login.flush(tokens);
      await vi.waitFor(() =>
        http.expectOne(statusUrl(connection)).flush({ hasAdmin: true, hasNgo: true, isSetupComplete: true }),
      );
      await done;

      expect(TestBed.inject(AuthService).accessToken()).toBe('access');
      expect(localStorage.getItem(`yavir.refreshToken.${connection.serverUrl()}`)).toBe('refresh');
      expect(navigate).toHaveBeenCalledWith('/planner');
      // Токени щойно видані — окремого оновлення нема.
      http.expectNone(`${connection.serverUrl()}/api/v1/auth/refresh-token`);
    });

    it('shows the NGO form when the NGO has not been created yet', async () => {
      const { fixture, connection, element, form, navigate, fill } = createLoginForm();
      fill('a@b.org', 'Abcde1!x');
      const done = form.submitLogin();
      http.expectOne(loginUrl(connection)).flush(tokens);
      await vi.waitFor(() =>
        http.expectOne(statusUrl(connection)).flush({ hasAdmin: true, hasNgo: false, isSetupComplete: false }),
      );
      await done;
      fixture.detectChanges();

      expect(element.textContent).toContain('Створіть свою організацію');
      expect(navigate).not.toHaveBeenCalled();
    });

    it.each([
      [401, 'Невірна пошта або пароль.'],
      [400, 'Сервер не прийняв дані для входу'],
      [500, 'Не вдалося увійти'],
    ])('shows a block above the form for HTTP %i and requests no status', async (status, text) => {
      const { fixture, connection, element, form, navigate, fill } = createLoginForm();
      fill('a@b.org', 'wrong');
      const done = form.submitLogin();
      http.expectOne(loginUrl(connection)).flush({}, { status, statusText: 'x' });
      await done;
      fixture.detectChanges();

      const banner = element.querySelector('.login__error')!;
      expect(banner.textContent).toContain(text);
      expect(
        banner.compareDocumentPosition(element.querySelector('.login-form')!) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(navigate).not.toHaveBeenCalled();
      http.expectNone(statusUrl(connection));
      expect(localStorage.getItem(`yavir.refreshToken.${connection.serverUrl()}`)).toBeNull();
    });

    it('shows an error when the system status cannot be loaded after a successful login', async () => {
      const { fixture, connection, element, form, navigate, fill } = createLoginForm();
      fill('a@b.org', 'Abcde1!x');
      const done = form.submitLogin();
      http.expectOne(loginUrl(connection)).flush(tokens);
      await vi.waitFor(() => http.expectOne(statusUrl(connection)).error(new ProgressEvent('error')));
      await done;
      fixture.detectChanges();

      expect(navigate).not.toHaveBeenCalled();
      expect(element.querySelector('.login__error')?.textContent).toContain('Не вдалося отримати стан системи');
    });
  });

  describe('by system setup status', () => {
    type Status = { hasAdmin: boolean; hasNgo: boolean; isSetupComplete: boolean };

    function createWith(status: Status, options: { tokens: boolean }) {
      if (options.tokens) {
        storeTokens();
      }
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, status);
      return ctx;
    }

    const complete: Status = { hasAdmin: true, hasNgo: true, isSetupComplete: true };
    const noNgo: Status = { hasAdmin: true, hasNgo: false, isSetupComplete: false };
    const noAdmin: Status = { hasAdmin: false, hasNgo: false, isSetupComplete: false };

    it('shows the login form when the setup is complete and there are no tokens', () => {
      const { element } = createWith(complete, { tokens: false });
      expect(element.textContent).toContain('Вхід в систему');
    });

    it('shows the login form when the setup is complete even if flags disagree and there are no tokens', () => {
      const { element } = createWith({ hasAdmin: false, hasNgo: false, isSetupComplete: true }, { tokens: false });
      expect(element.textContent).toContain('Вхід в систему');
    });

    it.each([
      ['without tokens', false],
      ['with tokens', true],
    ])('shows the admin form when there is no admin, %s', (_name, tokens) => {
      const { element } = createWith(noAdmin, { tokens });
      expect(element.textContent).toContain('Налаштуйте адміністратора');
    });

    it('shows the admin form when the NGO exists but the admin does not', () => {
      const { element } = createWith({ hasAdmin: false, hasNgo: true, isSetupComplete: false }, { tokens: false });
      expect(element.textContent).toContain('Налаштуйте адміністратора');
    });

    it('shows the NGO form when there is an admin but no NGO and there are tokens', () => {
      const { element } = createWith(noNgo, { tokens: true });
      expect(element.textContent).toContain('Створіть свою організацію');
    });

    it('shows the login form when there is an admin but no NGO and there are no tokens', () => {
      const { element } = createWith(noNgo, { tokens: false });
      expect(element.textContent).toContain('Вхід в систему');
      expect(element.textContent).not.toContain('Створіть свою організацію');
    });

    it('shows the setup error when both exist but the setup is not complete', () => {
      const { element } = createWith({ hasAdmin: true, hasNgo: true, isSetupComplete: false }, { tokens: true });
      expect(element.textContent).toContain('Сталася помилка при налаштуванні системи.');
      expect(element.querySelector('.login__card')).toBeNull();
    });

    it('shows the login form when the system status cannot be loaded, even with tokens', () => {
      storeTokens();
      const { fixture, connection, element, request } = create();
      request.flush({ status: 'Healthy' });
      fixture.detectChanges();
      http.expectOne(`${connection.serverUrl()}/api/v1/system/status`).error(new ProgressEvent('error'));
      fixture.detectChanges();
      expect(element.textContent).toContain('Вхід в систему');
      http.expectNone(`${connection.serverUrl()}/api/v1/auth/refresh-token`);
    });
  });

  describe('session refresh when the setup is complete and there are tokens', () => {
    const complete = { hasAdmin: true, hasNgo: true, isSetupComplete: true };

    function createWithTokens() {
      storeTokens();
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      ctx.fixture.detectChanges();
      http.expectOne(`${ctx.connection.serverUrl()}/api/v1/system/status`).flush(complete);
      ctx.fixture.detectChanges();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      const refresh = http.expectOne(`${ctx.connection.serverUrl()}/api/v1/auth/refresh-token`);
      return { ...ctx, navigate, refresh };
    }

    it('sends both stored tokens, keeps the new pair and redirects to the planner', async () => {
      const { fixture, connection, element, navigate, refresh } = createWithTokens();
      expect(refresh.request.body).toEqual({ accessToken: 'old-access', refreshToken: 'old-refresh' });
      // Поки йде оновлення — ні форми входу, ні помилки.
      expect(element.textContent).not.toContain('Вхід в систему');

      refresh.flush({ accessToken: 'new-access', refreshToken: 'new-refresh' });
      await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/planner'));
      fixture.detectChanges();

      const server = connection.serverUrl();
      expect(localStorage.getItem(`yavir.accessToken.${server}`)).toBe('new-access');
      expect(localStorage.getItem(`yavir.refreshToken.${server}`)).toBe('new-refresh');
      // Токени змінилися, але повторного оновлення нема.
      http.expectNone(`${server}/api/v1/auth/refresh-token`);
    });

    it('signs out and shows the login form when the server rejects the tokens', async () => {
      const { fixture, connection, element, navigate, refresh } = createWithTokens();
      refresh.flush({}, { status: 401, statusText: 'Unauthorized' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('.login-form')).not.toBeNull();
      });
      expect(element.querySelector('.login__error')).toBeNull();
      expect(navigate).not.toHaveBeenCalled();
      expect(localStorage.getItem(`yavir.refreshToken.${connection.serverUrl()}`)).toBeNull();
      expect(TestBed.inject(AuthService).hasTokens()).toBe(false);
    });

    it('signs out and shows the login form when the server does not answer', async () => {
      const { fixture, connection, element, navigate, refresh } = createWithTokens();
      refresh.error(new ProgressEvent('error'));
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('.login-form')).not.toBeNull();
      });
      expect(navigate).not.toHaveBeenCalled();
      expect(localStorage.getItem(`yavir.refreshToken.${connection.serverUrl()}`)).toBeNull();
    });

    it('does not try to refresh without tokens', () => {
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, complete);
      http.expectNone(`${ctx.connection.serverUrl()}/api/v1/auth/refresh-token`);
    });
  });

  describe('admin setup form', () => {
    interface AdminForm {
      onFieldChange(field: unknown, value: string): void;
      submitAdmin(): void;
      adminEmail: unknown;
      adminLastName: unknown;
      adminFirstName: unknown;
      adminPassword: unknown;
      adminPasswordRepeat: unknown;
    }

    function createAdmin() {
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, { hasAdmin: false, hasNgo: true, isSetupComplete: false });
      const form = ctx.fixture.componentInstance as unknown as AdminForm;
      const fill = (values: Partial<Record<'email' | 'last' | 'first' | 'pw' | 'repeat', string>>) => {
        const map = {
          email: form.adminEmail,
          last: form.adminLastName,
          first: form.adminFirstName,
          pw: form.adminPassword,
          repeat: form.adminPasswordRepeat,
        };
        for (const [key, value] of Object.entries(values)) {
          form.onFieldChange(map[key as keyof typeof map], value);
        }
      };
      const submit = () => {
        form.submitAdmin();
        ctx.fixture.detectChanges();
        return Array.from(ctx.element.querySelectorAll('.login__error-line, .login__field-error')).map((e) => e.textContent?.trim());
      };
      return { ...ctx, fill, submit };
    }

    const valid = { email: 'a@b.org', last: 'Іваненко', first: 'Олена', pw: 'Abcde1!x', repeat: 'Abcde1!x' };

    it('shows the five fields', () => {
      const { element } = createAdmin();
      expect(element.querySelectorAll('ion-input')).toHaveLength(5);
      const placeholders = Array.from(element.querySelectorAll('ion-input')).map((i) => (i as HTMLIonInputElement).placeholder);
      expect(placeholders).toEqual(['Пошта', 'Прізвище', 'Імʼя', 'Пароль', 'Повторіть пароль']);
    });

    it('shows no errors before the first submit', () => {
      const { element } = createAdmin();
      expect(element.querySelector('.login__field-error')).toBeNull();
    });

    it('requires every field', () => {
      const { submit } = createAdmin();
      // Прізвище — блоком над формою, решта — під полями.
      expect(submit()).toEqual([
        'Введіть прізвище.',
        'Введіть пошту.',
        'Введіть імʼя.',
        'Введіть пароль.',
        'Повторіть пароль.',
      ]);
    });

    it('shows the last name error in a block above the form and the other errors under their fields', () => {
      const { element, submit } = createAdmin();
      submit();
      const card = element.querySelector('.login__card')!;
      const banner = element.querySelector('.login__error')!;
      expect(banner.textContent).toContain('Введіть прізвище.');
      expect(banner.textContent).not.toContain('імʼя');
      expect(banner.textContent).not.toContain('пошту');
      expect(banner.compareDocumentPosition(card) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(card.querySelectorAll('.login__field-error')).toHaveLength(4);
      expect(card.textContent).toContain('Введіть імʼя.');
      expect(card.textContent).not.toContain('Введіть прізвище.');
    });

    it('accepts a valid form without validation errors', () => {
      const { fill, submit } = createAdmin();
      fill(valid);
      expect(submit()).toEqual([]);
    });

    it('sends no request while the form is invalid', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, repeat: 'other' });
      submit();
      http.expectNone(() => true);
    });

    it('creates the admin, logs in with the same data, then moves on to the NGO form', async () => {
      const { fixture, connection, element, fill, submit } = createAdmin();
      fill(valid);
      submit();

      const create = http.expectOne(`${connection.serverUrl()}/api/v1/users`);
      expect(create.request.body).toEqual({
        firstName: 'Олена',
        lastName: 'Іваненко',
        email: 'a@b.org',
        password: 'Abcde1!x',
        confirmPassword: 'Abcde1!x',
      });
      // Вхід — лише після відповіді на створення.
      http.expectNone(`${connection.serverUrl()}/api/v1/auth/login`);
      create.flush('123e4567-e89b-12d3-a456-426614174000', { status: 201, statusText: 'Created' });
      await vi.waitFor(() => http.expectOne(`${connection.serverUrl()}/api/v1/auth/login`));
    });

    it('shows the NGO form after a successful login', async () => {
      const { fixture, connection, element, fill, submit } = createAdmin();
      fill(valid);
      submit();
      http.expectOne(`${connection.serverUrl()}/api/v1/users`).flush('id', { status: 201, statusText: 'Created' });
      const login = await vi.waitFor(() => http.expectOne(`${connection.serverUrl()}/api/v1/auth/login`));
      expect(login.request.body).toEqual({ email: 'a@b.org', password: 'Abcde1!x' });
      login.flush({ accessToken: 'access', refreshToken: 'refresh' });

      await vi.waitFor(() => http.expectOne(`${connection.serverUrl()}/api/v1/system/status`)).then((status) =>
        status.flush({ hasAdmin: true, hasNgo: false, isSetupComplete: false }),
      );
      fixture.detectChanges();
      expect(element.textContent).toContain('Створіть свою організацію');
      expect(TestBed.inject(AuthService).accessToken()).toBe('access');
    });

    it('shows the server error above the form and does not log in when creating fails', async () => {
      const { fixture, connection, element, fill, submit } = createAdmin();
      fill(valid);
      submit();
      http.expectOne(`${connection.serverUrl()}/api/v1/users`).flush({}, { status: 400, statusText: 'Bad Request' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('.login__error')?.textContent).toContain('Сервер не прийняв дані адміністратора');
      });
      http.expectNone(`${connection.serverUrl()}/api/v1/auth/login`);
      expect(element.textContent).toContain('Налаштуйте адміністратора');
    });

    it('does not create the admin twice when only the login failed', async () => {
      const { fixture, connection, element, fill, submit } = createAdmin();
      fill(valid);
      submit();
      http.expectOne(`${connection.serverUrl()}/api/v1/users`).flush('id', { status: 201, statusText: 'Created' });
      const login = await vi.waitFor(() => http.expectOne(`${connection.serverUrl()}/api/v1/auth/login`));
      login.flush({}, { status: 401, statusText: 'Unauthorized' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('.login__error')?.textContent).toContain('Адміністратора створено, але увійти не вдалося');
      });

      submit();
      http.expectNone(`${connection.serverUrl()}/api/v1/users`);
      await vi.waitFor(() => http.expectOne(`${connection.serverUrl()}/api/v1/auth/login`));
    });

    it('rejects a malformed email', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, email: 'not-an-email' });
      expect(submit()).toEqual(['Це не схоже на адресу пошти.']);
    });

    it.each([
      ['too short', 'Ab1!def'],
      ['no uppercase letter', 'abcde1!x'],
      ['no lowercase letter', 'ABCDE1!X'],
      ['no digit', 'Abcdef!x'],
      ['no special character', 'Abcdef1x'],
      ['only non-Latin letters (the server counts Latin letters only)', 'Пароль1!'],
    ])('rejects a password with %s', (_name, password) => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, pw: password, repeat: password });
      expect(submit()).toEqual([expect.stringContaining('Пароль занадто простий')]);
    });

    it('accepts a password of exactly eight characters with all required kinds', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, pw: 'Abcdef1!', repeat: 'Abcdef1!' });
      expect(submit()).toEqual([]);
    });

    it('lists the password requirements and ticks them off while typing', () => {
      const { fixture, element, fill } = createAdmin();
      const items = () => Array.from(element.querySelectorAll('.login__requirements')[0].querySelectorAll('.login__requirement'));
      const met = () => items().filter((i) => i.classList.contains('login__requirement--met')).length;

      expect(items().map((i) => i.querySelector('.login__requirement-icon')?.textContent?.trim())).toEqual([
        '8',
        'a',
        'A',
        '123',
        '#',
      ]);
      expect(items().map((i) => i.querySelector('span:not(.login__requirement-icon)')?.textContent?.trim())).toEqual([
        'Мінімум 8 символів',
        'Принаймні одна мала латинська літера',
        'Принаймні одна велика латинська літера',
        'Принаймні одна цифра',
        'Принаймні один спецсимвол (не літера й не цифра)',
      ]);
      expect(met()).toBe(0);

      fill({ pw: 'abc' });
      fixture.detectChanges();
      expect(met()).toBe(1);

      fill({ pw: 'Abcdef1!' });
      fixture.detectChanges();
      expect(met()).toBe(5);
    });

    it('ticks off the match item under the repeat field only when the passwords are equal', () => {
      const { fixture, element, fill } = createAdmin();
      const item = () => {
        const lists = element.querySelectorAll('.login__requirements');
        return lists[lists.length - 1].querySelector('.login__requirement')!;
      };
      expect(item().textContent).toContain('Паролі збігаються');
      expect(item().querySelector('.login__requirement-icon')?.textContent?.trim()).toBe('=');
      expect(item().classList).not.toContain('login__requirement--met');

      fill({ pw: 'Abcdef1!', repeat: 'Abcdef1' });
      fixture.detectChanges();
      expect(item().classList).not.toContain('login__requirement--met');

      fill({ repeat: 'Abcdef1!' });
      fixture.detectChanges();
      expect(item().classList).toContain('login__requirement--met');
    });

    it('rejects passwords that do not match', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, repeat: 'Abcde2!x' });
      expect(submit()).toEqual(['Паролі не збігаються.']);
    });
  });

  describe('when the system has no NGO yet', () => {
    function createWithoutNgo() {
      storeTokens();
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, { hasAdmin: true, hasNgo: false, isSetupComplete: false });
      return ctx;
    }

    function pickLogo(element: HTMLElement, file: File): void {
      const input = element.querySelector<HTMLInputElement>('input[type=file]')!;
      Object.defineProperty(input, 'files', { value: [file], configurable: true });
      input.dispatchEvent(new Event('change'));
    }

    it('shows the organization form instead of the login stub', () => {
      const { element } = createWithoutNgo();
      expect(element.textContent).toContain('Створіть свою організацію');
      expect(element.textContent).not.toContain('Вхід в систему');
      expect(element.querySelector('ion-input')).not.toBeNull();
      expect(element.querySelector('input[type=file]')).not.toBeNull();
    });

    it('keeps showing the loading state until the system status arrives', () => {
      const { fixture, connection, element, request } = create();
      request.flush({ status: 'Healthy' });
      fixture.detectChanges();
      expect(element.textContent).toContain('Перевіряємо підключення');
      expect(element.textContent).not.toContain('Вхід в систему');
      http.expectOne(`${connection.serverUrl()}/api/v1/system/status`);
    });

    it('falls back to the login stub when the system status cannot be loaded', () => {
      const { fixture, connection, element, request } = create();
      request.flush({ status: 'Healthy' });
      fixture.detectChanges();
      http.expectOne(`${connection.serverUrl()}/api/v1/system/status`).error(new ProgressEvent('error'));
      fixture.detectChanges();
      expect(element.textContent).toContain('Вхід в систему');
    });

    it('creates the organization with a null logoUrl, then redirects to the planner', async () => {
      const { connection, page } = createWithoutNgo();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      page.onNgoNameChange('Моя ГО');
      const done = (page as unknown as { submitNgo(): Promise<void> }).submitNgo();

      const create = http.expectOne(`${connection.serverUrl()}/api/v1/ngo`);
      expect(create.request.body).toEqual({ name: 'Моя ГО', logoUrl: null });
      create.flush(null, { status: 201, statusText: 'Created' });
      await done;

      expect(navigate).toHaveBeenCalledWith('/planner');
      http.expectNone(`${connection.serverUrl()}/api/v1/system/status`);
    });

    it('does not redirect when creating the organization fails', async () => {
      const { connection, page } = createWithoutNgo();
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      page.onNgoNameChange('Моя ГО');
      const done = (page as unknown as { submitNgo(): Promise<void> }).submitNgo();
      http.expectOne(`${connection.serverUrl()}/api/v1/ngo`).flush({}, { status: 401, statusText: 'Unauthorized' });
      await done;
      expect(navigate).not.toHaveBeenCalled();
    });

    it('shows the server error and stays on the form when creation fails', async () => {
      const { fixture, connection, element, page } = createWithoutNgo();
      page.onNgoNameChange('Моя ГО');
      const done = (page as unknown as { submitNgo(): Promise<void> }).submitNgo();
      http.expectOne(`${connection.serverUrl()}/api/v1/ngo`).flush({}, { status: 401, statusText: 'Unauthorized' });
      await done;
      fixture.detectChanges();

      expect(element.textContent).toContain('потрібна авторизація');
      expect(element.textContent).toContain('Створіть свою організацію');
      http.expectNone(`${connection.serverUrl()}/api/v1/system/status`);
    });

    it('does not send a request without a name', () => {
      const { page } = createWithoutNgo();
      (page as unknown as { submitNgo(): Promise<void> }).submitNgo();
      http.expectNone(() => true);
    });

    it('shows name and logo errors under their fields and the server error in a block above the card', async () => {
      const { fixture, connection, element, page } = createWithoutNgo();
      const form = page as unknown as { submitNgo(): Promise<void>; onNgoNameChange(v: string): void };

      void form.submitNgo();
      fixture.detectChanges();
      const nameError = element.querySelector('.login__card > .login__field-error');
      expect(nameError?.textContent).toContain('Введіть назву організації');
      expect(element.querySelector('.login__error')).toBeNull();

      pickLogo(element, new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
      fixture.detectChanges();
      const logoError = element.querySelector('.login__logo .login__field-error');
      expect(logoError?.textContent).toContain('Підтримуються лише зображення');
      expect(element.querySelector('.login__error')).toBeNull();

      form.onNgoNameChange('Моя ГО');
      pickLogo(element, new File(['x'], 'logo.png', { type: 'image/png' }));
      const done = form.submitNgo();
      http.expectOne(`${connection.serverUrl()}/api/v1/ngo`).flush({}, { status: 403, statusText: 'Forbidden' });
      await done;
      fixture.detectChanges();
      const banner = element.querySelector('.login__error')!;
      const card = element.querySelector('.login__card')!;
      expect(banner.textContent).toContain('Недостатньо прав');
      expect(banner.compareDocumentPosition(card) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('requires a name', () => {
      const { fixture, element, page } = createWithoutNgo();
      (page as unknown as { submitNgo(): void }).submitNgo();
      fixture.detectChanges();
      expect(element.textContent).toContain('Введіть назву організації');
    });

    it('rejects a logo that is not an image', () => {
      const { fixture, element } = createWithoutNgo();
      pickLogo(element, new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
      fixture.detectChanges();
      expect(element.textContent).toContain('Підтримуються лише зображення');
      expect(element.querySelector('.login__logo-preview')).toBeNull();
    });

    it('rejects a logo that is too large', () => {
      const { fixture, element } = createWithoutNgo();
      pickLogo(element, new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'big.png', { type: 'image/png' }));
      fixture.detectChanges();
      expect(element.textContent).toContain('Файл завеликий');
    });

    it('accepts a logo dropped onto the drop zone and highlights it while dragging', () => {
      vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: () => 'blob:dropped', revokeObjectURL: () => undefined }));
      const { fixture, element } = createWithoutNgo();
      const zone = element.querySelector<HTMLElement>('.login__dropzone')!;

      zone.dispatchEvent(new Event('dragover', { cancelable: true }));
      fixture.detectChanges();
      expect(zone.classList).toContain('login__dropzone--active');

      const file = new File(['x'], 'dropped.png', { type: 'image/png' });
      const drop = new Event('drop', { cancelable: true });
      Object.defineProperty(drop, 'dataTransfer', { value: { files: [file] } });
      zone.dispatchEvent(drop);
      fixture.detectChanges();

      expect(zone.classList).not.toContain('login__dropzone--active');
      expect(zone.querySelector('.login__logo-preview')?.getAttribute('src')).toBe('blob:dropped');
      expect(zone.textContent).toContain('dropped.png');
    });

    it('rejects a dropped file that is not an image', () => {
      const { fixture, element } = createWithoutNgo();
      const zone = element.querySelector<HTMLElement>('.login__dropzone')!;
      const drop = new Event('drop', { cancelable: true });
      Object.defineProperty(drop, 'dataTransfer', {
        value: { files: [new File(['x'], 'doc.pdf', { type: 'application/pdf' })] },
      });
      zone.dispatchEvent(drop);
      fixture.detectChanges();
      expect(element.textContent).toContain('Підтримуються лише зображення');
    });

    it('previews a valid logo and lets it be removed', () => {
      const create_ = vi.fn(() => 'blob:logo');
      const revoke = vi.fn();
      vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: create_, revokeObjectURL: revoke }));
      const { fixture, element } = createWithoutNgo();

      pickLogo(element, new File(['x'], 'logo.png', { type: 'image/png' }));
      fixture.detectChanges();
      expect(element.querySelector('.login__logo-preview')?.getAttribute('src')).toBe('blob:logo');

      const remove = element.querySelector<HTMLButtonElement>('.login__logo-remove')!;
      expect(remove.getAttribute('aria-label')).toBe('Видалити логотип');
      expect(remove.title).toBe('Видалити логотип');
      remove.click();
      fixture.detectChanges();
      expect(element.querySelector('.login__logo-preview')).toBeNull();
      expect(element.querySelector('.login__logo-remove')).toBeNull();
      expect(revoke).toHaveBeenCalledWith('blob:logo');
    });
  });

  it('does not show the redirect error when the connection is fine', () => {
    const { fixture, element, request } = create();
    fixture.componentRef.setInput('error', 'no-connection');
    request.flush({ status: 'Healthy' });
    fixture.detectChanges();
    expect(element.querySelector('.login__error')).toBeNull();
  });
});
