import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { RELOGIN_URL } from '../core/auth/auth.service';
import { SuccessNoticeService } from '../core/notice/success-notice.service';
import { ResetPasswordPage } from './reset-password.page';

// Protected API компонента, яке в шаблоні викликають події.
interface ResetPasswordPageInternals {
  onFieldChange(field: unknown, value: string): void;
  password: unknown;
  passwordRepeat: unknown;
  submit(): Promise<void>;
}

describe('ResetPasswordPage', () => {
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let fragment: BehaviorSubject<string | null>;
  const server = `${location.protocol}//${location.hostname}:5065`;
  const resetUrl = `${server}/api/v1/users/reset-password`;
  const PASSWORD = 'Abcde1!x';

  beforeEach(() => {
    localStorage.clear();
    fragment = new BehaviorSubject<string | null>(null);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        // Фрагмент адреси сторінки (`#token=…`) — керований тестом.
        { provide: ActivatedRoute, useValue: { fragment } },
      ],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function create(token: string | null = 'tok') {
    fragment.next(token === null ? null : `token=${encodeURIComponent(token)}`);
    const fixture = TestBed.createComponent(ResetPasswordPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const page = fixture.componentInstance as unknown as ResetPasswordPageInternals;
    const fill = (password: string, repeat = password) => {
      page.onFieldChange(page.password, password);
      page.onFieldChange(page.passwordRepeat, repeat);
    };
    return { fixture, element, page, fill };
  }

  const buttonByText = (element: HTMLElement, text: string) =>
    Array.from(element.querySelectorAll<HTMLElement>('button, ion-button')).find((button) => button.textContent?.trim() === text)!;

  it('shows the form like the sign-in one: the title, the password and its repeat, "Send" and "Cancel"', () => {
    const { element } = create();
    expect(element.querySelector('.join__card .join__title')?.textContent?.trim()).toBe(uk.resetPassword.title);
    expect(element.querySelector('app-password-fields')).not.toBeNull();
    expect(buttonByText(element, uk.resetPassword.submit)).toBeTruthy();
    expect(buttonByText(element, uk.common.cancel)).toBeTruthy();
    expect(element.querySelector('.auth-error')).toBeNull();
  });

  it('"Cancel" goes to /relogin and sends nothing', () => {
    const { element } = create();
    buttonByText(element, uk.common.cancel).click();
    expect(navigate).toHaveBeenCalledWith(RELOGIN_URL);
    http.expectNone(resetUrl);
  });

  it('does not send an empty, a weak, a too long or a mismatching password', async () => {
    const { fixture, element, page, fill } = create();
    await page.submit();
    fixture.detectChanges();
    expect(element.textContent).toContain(uk.resetPassword.error.passwordRequired);

    fill('abc', 'abd');
    await page.submit();
    fixture.detectChanges();
    expect(element.textContent).toContain(uk.resetPassword.error.passwordWeak);
    expect(element.textContent).toContain(uk.resetPassword.error.passwordMismatch);

    const long = 'Aa1!' + 'x'.repeat(125);
    fill(long);
    await page.submit();
    fixture.detectChanges();
    expect(element.textContent).toContain(uk.resetPassword.error.passwordTooLong);
    http.expectNone(resetUrl);
  });

  it('sends the token and the new password to the server of the link without a session, then goes to /relogin', async () => {
    const { fixture, element, page, fill } = create('tok 1');
    fill(PASSWORD);
    const done = page.submit();
    fixture.detectChanges();
    expect(buttonByText(element, uk.resetPassword.sending)).toBeTruthy();

    const request = http.expectOne(resetUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'tok 1', newPassword: PASSWORD });
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(null, { status: 204, statusText: 'No Content' });
    await done;
    // Без запису в історії: «Назад» не повертає форму з уже використаним посиланням.
    expect(navigate).toHaveBeenCalledWith(RELOGIN_URL, { replaceUrl: true });
    // «Пароль змінено…» показує плашка в корені застосунку вже на наступній сторінці.
    expect(TestBed.inject(SuccessNoticeService).key()).toBe('resetPassword.done');
  });

  it.each([
    ['invalid', 400],
    ['unauthorized', 401],
    ['error', 404],
  ] as const)('replaces the form by the reason (%s) and "Back" to /relogin when the server says no', async (failure, status) => {
    const { fixture, element, page, fill } = create();
    fill(PASSWORD);
    const done = page.submit();
    http.expectOne(resetUrl).flush({}, { status, statusText: 'x' });
    await done;
    fixture.detectChanges();

    expect(element.querySelector('.auth-error')?.textContent).toContain(uk.resetPassword.failure[failure]);
    expect(element.querySelector('app-password-fields')).toBeNull();
    expect(navigate).not.toHaveBeenCalled();
    expect(TestBed.inject(SuccessNoticeService).key()).toBeNull();
    buttonByText(element, uk.common.back).click();
    expect(navigate).toHaveBeenCalledWith(RELOGIN_URL);
  });

  it.each([
    ['the check of the field', { errors: { NewPassword: ['Password is too weak.'] } }],
    ['the rules of the password', { detail: "User operation 'ChangePassword' failed: Passwords must have at least one digit ('0'-'9')." }],
  ])('keeps the form with the error under the password when the server did not take the password (%s)', async (_, body) => {
    const { fixture, element, page, fill } = create();
    fill(PASSWORD);
    const done = page.submit();
    http.expectOne(resetUrl).flush(body, { status: 400, statusText: 'Bad Request' });
    await done;
    fixture.detectChanges();
    expect(element.querySelector('.auth-error')).toBeNull();
    expect(element.querySelector('app-password-fields')?.textContent).toContain(uk.resetPassword.error.passwordRejected);
    expect(navigate).not.toHaveBeenCalled();

    // Інший пароль — відмова сервера стосувалася попереднього.
    fill(`${PASSWORD}2`);
    fixture.detectChanges();
    expect(element.querySelector('app-password-fields')?.textContent).not.toContain(uk.resetPassword.error.passwordRejected);
  });

  it('says the link was already used when the server rejected its token', async () => {
    const { fixture, element, page, fill } = create();
    fill(PASSWORD);
    const done = page.submit();
    http.expectOne(resetUrl).flush({ detail: "User operation 'ChangePassword' failed: Invalid token." }, { status: 400, statusText: 'Bad Request' });
    await done;
    fixture.detectChanges();
    expect(element.querySelector('.auth-error')?.textContent).toContain(uk.resetPassword.failure.invalid);
  });

  it('says the link is inactive when it has no token', () => {
    const { element } = create(null);
    expect(element.querySelector('.auth-error')?.textContent).toContain(uk.resetPassword.failure.unauthorized);
    expect(element.querySelector('app-password-fields')).toBeNull();
  });
});
