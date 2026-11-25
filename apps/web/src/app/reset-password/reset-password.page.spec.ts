import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
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
  const server = `${location.protocol}//${location.hostname}:5065`;
  const resetUrl = `${server}/api/v1/users/reset-password`;
  const PASSWORD = 'Abcde1!x';

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
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
    const fixture = TestBed.createComponent(ResetPasswordPage);
    fixture.componentRef.setInput('token', token ?? undefined);
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
    expect(navigate).toHaveBeenCalledWith(RELOGIN_URL);
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

  it('says the link is inactive when it has no token', () => {
    const { element } = create(null);
    expect(element.querySelector('.auth-error')?.textContent).toContain(uk.resetPassword.failure.unauthorized);
    expect(element.querySelector('app-password-fields')).toBeNull();
  });
});
