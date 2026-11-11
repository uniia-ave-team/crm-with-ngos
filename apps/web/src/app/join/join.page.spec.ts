import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { AccountsService } from '../core/accounts/accounts.service';
import { REDIRECT_URL } from '../core/auth/auth.service';
import { JoinPage } from './join.page';

// Приватне/protected API компонента, яке в шаблоні викликають події.
interface JoinPageInternals {
  onFieldChange(field: unknown, value: string): void;
  lastName: unknown;
  firstName: unknown;
  patronymic: unknown;
  password: unknown;
  passwordRepeat: unknown;
  submit(): Promise<void>;
}

describe('JoinPage', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  const server = `${location.protocol}//${location.hostname}:5065`;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    });
    // Тексти сторінки живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function create(token: string | null = 'tok') {
    const fixture = TestBed.createComponent(JoinPage);
    fixture.componentRef.setInput('token', token ?? undefined);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const page = fixture.componentInstance as unknown as JoinPageInternals;
    return { fixture, element, page };
  }

  /** Відповідь на перевірку сервера за посиланням (разом із першою перевіркою самого сервісу підключення). */
  function answerHealth(fixture: { detectChanges(): void }, body: unknown = { status: 'Healthy' }): void {
    for (const request of http.match(`${server}/health`)) {
      if (body === null) {
        request.error(new ProgressEvent('error'));
      } else {
        request.flush(body as never);
      }
    }
    fixture.detectChanges();
  }

  /** Чекає, поки сторінка закінчить перевірку: «завантаження» змінюється на результат. */
  async function settled(fixture: { detectChanges(): void }, element: HTMLElement): Promise<void> {
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(element.textContent).not.toContain(uk.join.loading);
    });
  }

  async function answerValidate(
    fixture: { detectChanges(): void },
    element: HTMLElement,
    body: unknown,
    status = 200,
  ): Promise<void> {
    const request = await vi.waitFor(() => http.expectOne((r) => r.url === `${server}/api/v1/invitations/validate`));
    request.flush(body as never, { status, statusText: 'x' });
    await settled(fixture, element);
  }

  /** Сервер знайдено, токен дійсний — показано форму. */
  async function createForm() {
    const created = create();
    answerHealth(created.fixture);
    await answerValidate(created.fixture, created.element, { email: 'a@b.org', ngoId: 'n', roleId: 'r' });
    return created;
  }

  function fill(page: JoinPageInternals, values: Partial<Record<'lastName' | 'firstName' | 'patronymic' | 'password' | 'passwordRepeat', string>>) {
    for (const [field, value] of Object.entries(values)) {
      page.onFieldChange((page as unknown as Record<string, unknown>)[field], value);
    }
  }

  const acceptRequest = () => http.expectOne(`${server}/api/v1/invitations/accept`);

  it('shows a loading state while the server is being checked', () => {
    const { element } = create();
    expect(element.textContent).toContain(uk.join.loading);
    expect(element.querySelector('.join__error')).toBeNull();
    expect(element.querySelector('ion-input')).toBeNull();
  });

  it('asks the server of the link, not the server of a saved account', async () => {
    TestBed.inject(AccountsService).add({
      server: 'https://other.example:5065',
      accessToken: 'a',
      refreshToken: 'r',
      identity: 'someone',
    });
    create();
    http.expectOne(`${server}/health`).flush({ status: 'Healthy' });
    const request = await vi.waitFor(() => http.expectOne((r) => r.url === `${server}/api/v1/invitations/validate`));
    expect(request.request.params.get('token')).toBe('tok');
  });

  it.each([
    ['is unreachable', null],
    ['answers not as our API', { status: 'Degraded' }],
  ])('says the server is not found, with a link to the login page, when it %s', async (_name, body) => {
    const { fixture, element } = create();
    answerHealth(fixture, body);
    await settled(fixture, element);

    const error = element.querySelector('.join__error');
    expect(error?.getAttribute('role')).toBe('alert');
    expect(error?.textContent).toContain(uk.join.noServer);
    const link = error?.querySelector('a');
    expect(link?.textContent?.trim()).toBe(uk.join.home);
    expect(link?.getAttribute('href')).toBe('/login');
    expect(element.querySelector('ion-input')).toBeNull();
    // Токен не перевіряємо, поки сервера нема.
    http.expectNone((r) => r.url.includes('/invitations/validate'));
  });

  it.each([
    ['the server rejects the token', 400],
    ['the server fails', 500],
  ])('says the link is inactive, with a link to the login page, when %s', async (_name, status) => {
    const { fixture, element } = create();
    answerHealth(fixture);
    await answerValidate(fixture, element, {}, status);

    const error = element.querySelector('.join__error');
    expect(error?.textContent).toContain(uk.join.invalid);
    expect(error?.textContent).not.toContain(uk.join.noServer);
    expect(error?.querySelector('a')?.getAttribute('href')).toBe('/login');
    expect(element.querySelector('ion-input')).toBeNull();
  });

  it('says the link is inactive without asking the server when there is no token', async () => {
    const { fixture, element } = create(null);
    answerHealth(fixture);
    await settled(fixture, element);

    http.expectNone((r) => r.url.includes('/invitations/validate'));
    expect(element.querySelector('.join__error')?.textContent).toContain(uk.join.invalid);
  });

  it('shows the form with the e-mail of the invitation as the first, disabled field', async () => {
    const { element } = await createForm();

    const inputs = Array.from(element.querySelectorAll<HTMLElement & { disabled: boolean; value: string; placeholder: string }>('ion-input'));
    expect(inputs[0].disabled).toBe(true);
    expect(inputs[0].value).toBe('a@b.org');
    expect(inputs.slice(1).every((input) => !input.disabled)).toBe(true);
    // Пошта, прізвище, імʼя, по батькові (далі — поля пароля).
    expect(inputs[2].placeholder).toBe(`${uk.join.firstName} *`);
    expect(inputs[1].placeholder).toBe(`${uk.join.lastName} *`);
    expect(inputs[3].placeholder).toBe(uk.join.patronymic);
    expect(element.querySelector('app-password-fields')).not.toBeNull();
    expect(element.querySelector('.join__error')).toBeNull();
    expect(element.textContent).toContain(uk.join.title);
  });

  it('does not send an incomplete form and shows what is missing', async () => {
    const { fixture, element, page } = await createForm();
    await page.submit();
    fixture.detectChanges();

    http.expectNone(`${server}/api/v1/invitations/accept`);
    const banner = element.querySelector('.join__error')?.textContent;
    expect(banner).toContain(uk.join.error.lastNameRequired);
    expect(element.querySelector('.join__card')?.textContent).toContain(uk.join.error.firstNameRequired);
    expect(element.textContent).toContain(uk.join.error.passwordRequired);
  });

  it('does not send a form with a weak or a mismatching password', async () => {
    const { fixture, element, page } = await createForm();
    fill(page, { lastName: 'Другий', firstName: 'Перший', password: 'abc', passwordRepeat: 'abd' });
    await page.submit();
    fixture.detectChanges();

    http.expectNone(`${server}/api/v1/invitations/accept`);
    expect(element.textContent).toContain(uk.join.error.passwordWeak);
    expect(element.textContent).toContain(uk.join.error.passwordMismatch);
  });

  it('accepts the invitation, adds the account as the active one and goes to the first page', async () => {
    const { fixture, page } = await createForm();
    fill(page, { lastName: ' Другий ', firstName: ' Перший ', password: 'Abcde1!x', passwordRepeat: 'Abcde1!x' });

    const done = page.submit();
    const request = acceptRequest();
    expect(request.request.body).toEqual({
      token: 'tok',
      firstName: 'Перший',
      lastName: 'Другий',
      patronymic: null,
      password: 'Abcde1!x',
      confirmPassword: 'Abcde1!x',
    });
    request.flush({ accessToken: 'access', refreshToken: 'refresh' });
    await done;
    fixture.detectChanges();

    const accounts = TestBed.inject(AccountsService);
    expect(accounts.accounts()).toHaveLength(1);
    expect(accounts.active()).toMatchObject({ server, accessToken: 'access', refreshToken: 'refresh', email: 'a@b.org' });
    expect(navigate).toHaveBeenCalledWith(REDIRECT_URL);
  });

  it('adds the account next to the saved ones and makes the new one active', async () => {
    const accounts = TestBed.inject(AccountsService);
    accounts.add({ server: 'https://other.example:5065', accessToken: 'a0', refreshToken: 'r0', identity: 'someone' });
    const { fixture, page } = await createForm();
    fill(page, { lastName: 'Другий', firstName: 'Перший', password: 'Abcde1!x', passwordRepeat: 'Abcde1!x' });

    const done = page.submit();
    acceptRequest().flush({ accessToken: 'access', refreshToken: 'refresh' });
    await done;
    fixture.detectChanges();

    expect(accounts.accounts()).toHaveLength(2);
    expect(accounts.active()?.server).toBe(server);
  });

  it('sends the patronymic when it is given', async () => {
    const { page } = await createForm();
    fill(page, { lastName: 'Другий', firstName: 'Перший', patronymic: ' Третій ', password: 'Abcde1!x', passwordRepeat: 'Abcde1!x' });

    void page.submit();
    expect(acceptRequest().request.body.patronymic).toBe('Третій');
  });

  it('does not send the form twice while the request is going', async () => {
    const { page } = await createForm();
    fill(page, { lastName: 'Другий', firstName: 'Перший', password: 'Abcde1!x', passwordRepeat: 'Abcde1!x' });

    void page.submit();
    void page.submit();
    expect(acceptRequest()).toBeTruthy();
  });

  it.each([
    [400, uk.join.saveError.invalid],
    [500, uk.join.saveError.error],
    [0, uk.join.saveError.unreachable],
  ])('keeps the form and shows the error above it when the server answers %i, so it can be sent again', async (status, text) => {
    const { fixture, element, page } = await createForm();
    fill(page, { lastName: 'Другий', firstName: 'Перший', password: 'Abcde1!x', passwordRepeat: 'Abcde1!x' });

    const first = page.submit();
    const request = acceptRequest();
    if (status === 0) {
      request.error(new ProgressEvent('error'));
    } else {
      request.flush({}, { status, statusText: 'x' });
    }
    await first;
    fixture.detectChanges();

    const error = element.querySelector('.join__error');
    expect(error?.textContent).toContain(text);
    expect(error?.nextElementSibling?.classList.contains('join__card')).toBe(true);
    expect(element.querySelector('ion-input')).not.toBeNull();
    expect(TestBed.inject(AccountsService).accounts()).toHaveLength(0);
    expect(navigate).not.toHaveBeenCalled();

    // Другу спробу прийнято, а помилка зникає.
    const second = page.submit();
    acceptRequest().flush({ accessToken: 'access', refreshToken: 'refresh' });
    await second;
    fixture.detectChanges();
    expect(element.querySelector('.join__error')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(REDIRECT_URL);
  });

  it('does not keep an answer for an old token when the token changes', async () => {
    const { fixture, element } = create('first');
    answerHealth(fixture);
    const stale = await vi.waitFor(() => http.expectOne((r) => r.params.get('token') === 'first'));

    fixture.componentRef.setInput('token', 'second');
    fixture.detectChanges();
    answerHealth(fixture);
    const current = await vi.waitFor(() => http.expectOne((r) => r.params.get('token') === 'second'));

    current.flush({ email: 'new@b.org' });
    await fixture.whenStable();
    stale.flush({}, { status: 400, statusText: 'x' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element.querySelector('.join__error')).toBeNull();
    expect((element.querySelector('ion-input') as unknown as { value: string }).value).toBe('new@b.org');
  });
});
