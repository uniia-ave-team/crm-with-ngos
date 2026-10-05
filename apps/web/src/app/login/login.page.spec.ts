import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { ConnectionService } from '../core/connection/connection.service';
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

    expect(element.textContent).toContain('Форма входу з');
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

    expect(element.textContent).toContain(`Підключено до сервера ${location.protocol}//192.168.0.9:5065`);
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

  describe('by system setup status', () => {
    function createWith(status: { hasAdmin: boolean; hasNgo: boolean; isSetupComplete: boolean }) {
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, status);
      return ctx.element;
    }

    it('shows the login form when the setup is complete', () => {
      const element = createWith({ hasAdmin: true, hasNgo: true, isSetupComplete: true });
      expect(element.textContent).toContain('Форма входу з');
    });

    it('shows the login form when the setup is complete even if flags disagree', () => {
      const element = createWith({ hasAdmin: false, hasNgo: false, isSetupComplete: true });
      expect(element.textContent).toContain('Форма входу з');
    });

    it('shows the NGO form when there is no NGO', () => {
      const element = createWith({ hasAdmin: true, hasNgo: false, isSetupComplete: false });
      expect(element.textContent).toContain('Створіть свою організацію');
    });

    it('shows the admin setup when there is an NGO but no admin', () => {
      const element = createWith({ hasAdmin: false, hasNgo: true, isSetupComplete: false });
      expect(element.textContent).toContain('Налаштуйте адміністратора');
    });

    it('shows the setup error when both exist but the setup is not complete', () => {
      const element = createWith({ hasAdmin: true, hasNgo: true, isSetupComplete: false });
      expect(element.textContent).toContain('Сталася помилка при налаштуванні системи.');
      expect(element.querySelector('.login__card')).toBeNull();
    });
  });

  describe('admin setup form', () => {
    interface AdminForm {
      onAdminFieldChange(field: unknown, value: string): void;
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
          form.onAdminFieldChange(map[key as keyof typeof map], value);
        }
      };
      const submit = () => {
        form.submitAdmin();
        ctx.fixture.detectChanges();
        return Array.from(ctx.element.querySelectorAll('.login__error-line, .login__field-error')).map((e) => e.textContent?.trim());
      };
      return { ...ctx, fill, submit };
    }

    const valid = { email: 'a@b.org', last: 'Іваненко', first: 'Олена', pw: 'Abcde1', repeat: 'Abcde1' };

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

    it('accepts a valid form without errors and sends no request', () => {
      const { fill, submit } = createAdmin();
      fill(valid);
      expect(submit()).toEqual([]);
      http.expectNone(() => true);
    });

    it('rejects a malformed email', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, email: 'not-an-email' });
      expect(submit()).toEqual(['Це не схоже на адресу пошти.']);
    });

    it.each([
      ['too short', 'Ab1de'],
      ['no uppercase letter', 'abcde1'],
      ['no lowercase letter', 'ABCDE1'],
      ['no digit', 'Abcdef'],
    ])('rejects a password with %s', (_name, password) => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, pw: password, repeat: password });
      expect(submit()).toEqual([expect.stringContaining('Пароль занадто простий')]);
    });

    it('accepts a password of six characters with all required kinds', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, pw: 'Пароль1', repeat: 'Пароль1' });
      expect(submit()).toEqual([]);
    });

    it('rejects passwords that do not match', () => {
      const { fill, submit } = createAdmin();
      fill({ ...valid, repeat: 'Abcde2' });
      expect(submit()).toEqual(['Паролі не збігаються.']);
    });
  });

  describe('when the system has no NGO yet', () => {
    function createWithoutNgo() {
      const ctx = create();
      ctx.request.flush({ status: 'Healthy' });
      answerStatus(ctx.connection, ctx.fixture, { hasAdmin: false, hasNgo: false, isSetupComplete: false });
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
      expect(element.textContent).not.toContain('Форма входу з');
      expect(element.querySelector('ion-input')).not.toBeNull();
      expect(element.querySelector('input[type=file]')).not.toBeNull();
    });

    it('keeps showing the loading state until the system status arrives', () => {
      const { fixture, connection, element, request } = create();
      request.flush({ status: 'Healthy' });
      fixture.detectChanges();
      expect(element.textContent).toContain('Перевіряємо підключення');
      expect(element.textContent).not.toContain('Форма входу з');
      http.expectOne(`${connection.serverUrl()}/api/v1/system/status`);
    });

    it('falls back to the login stub when the system status cannot be loaded', () => {
      const { fixture, connection, element, request } = create();
      request.flush({ status: 'Healthy' });
      fixture.detectChanges();
      http.expectOne(`${connection.serverUrl()}/api/v1/system/status`).error(new ProgressEvent('error'));
      fixture.detectChanges();
      expect(element.textContent).toContain('Форма входу з');
    });

    it('creates the organization with a null logoUrl, then re-reads the system status', async () => {
      const { fixture, connection, element, page } = createWithoutNgo();
      page.onNgoNameChange('Моя ГО');
      const done = (page as unknown as { submitNgo(): Promise<void> }).submitNgo();

      const create = http.expectOne(`${connection.serverUrl()}/api/v1/ngo`);
      expect(create.request.body).toEqual({ name: 'Моя ГО', logoUrl: null });
      create.flush(null, { status: 201, statusText: 'Created' });
      await done;

      http
        .expectOne(`${connection.serverUrl()}/api/v1/system/status`)
        .flush({ hasAdmin: false, hasNgo: true, isSetupComplete: false });
      fixture.detectChanges();
      expect(element.textContent).not.toContain('Створіть свою організацію');
      expect(element.textContent).toContain('Налаштуйте адміністратора');
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
