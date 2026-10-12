import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ConnectionService } from '../../core/connection/connection.service';
import { SettingsPageModule } from '../settings.module';
import { PasswordPage } from './password.page';

// Тексти сторінки живуть в uk.json — беремо їх звідти, а не дублюємо.
const text = uk.settings.password;
const requirements = uk.common.passwordRequirements;

interface PasswordPageInternals {
  currentPassword: { set(value: string): void };
  newPassword: { set(value: string): void };
  repeatPassword: { set(value: string): void };
  submit(): Promise<void>;
}

describe('PasswordPage', () => {
  let http: HttpTestingController;
  let passwordUrl: string;
  let fixture: ComponentFixture<PasswordPage>;
  let page: PasswordPageInternals;
  let element: HTMLElement;

  beforeEach(async () => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [SettingsPageModule],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    passwordUrl = `${connection.serverUrl()}/api/v1/users/me/password`;

    fixture = TestBed.createComponent(PasswordPage);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
    page = fixture.componentInstance as unknown as PasswordPageInternals;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const fill = (values: { current?: string; password?: string; repeat?: string }) => {
    if (values.current !== undefined) page.currentPassword.set(values.current);
    if (values.password !== undefined) page.newPassword.set(values.password);
    if (values.repeat !== undefined) page.repeatPassword.set(values.repeat);
  };
  const valid = { current: 'Old1!aaa', password: 'New1!bbb', repeat: 'New1!bbb' };
  const errors = () => Array.from(element.querySelectorAll('.settings-field__error, .password-fields__error')).map((e) => e.textContent?.trim());
  const result = () => element.querySelector('.settings-result');
  const submit = async () => {
    const done = page.submit();
    fixture.detectChanges();
    return done;
  };

  describe('look', () => {
    it('shows the title, the current password field, the shared new password fields and the button from the language file', () => {
      expect(element.querySelector('.settings-section__title')?.textContent).toContain(text.title);
      expect(
        Array.from(element.querySelectorAll('.settings-field__label, .password-fields__label')).map((l) =>
          l.textContent?.replace('*', '').trim(),
        ),
      ).toEqual([text.current, text.new, text.repeat]);
      expect((element.querySelector('#settings-current-password') as HTMLInputElement).type).toBe('password');
      expect(element.querySelectorAll('app-password-fields ion-input')).toHaveLength(2);
      expect(element.querySelector('.settings-submit')?.textContent?.trim()).toBe(text.submit);
    });

    it('marks all three fields as required: an asterisk after every label and the required attribute', () => {
      const labels = Array.from(element.querySelectorAll('.settings-field__label, .password-fields__label'));
      expect(labels).toHaveLength(3);
      for (const label of labels) {
        expect(label.querySelector('.settings-field__required, .password-fields__required')?.textContent?.trim()).toBe('*');
      }
      expect((element.querySelector('#settings-current-password') as HTMLInputElement).required).toBe(true);
      const shared = Array.from(element.querySelectorAll('app-password-fields ion-input')) as HTMLIonInputElement[];
      expect(shared.every((input) => input.required)).toBe(true);
    });

    it('lists the requirements of the new password and the match item, with texts from the language file', () => {
      const texts = Array.from(element.querySelectorAll('.password-fields__requirement span:not(.password-fields__requirement-icon)')).map(
        (s) => s.textContent?.trim(),
      );
      expect(texts).toEqual([
        requirements.length,
        requirements.lowercase,
        requirements.uppercase,
        requirements.digit,
        requirements.special,
        requirements.match,
      ]);
    });

    it('gives the controls a tabindex (Safari does not tab to the fields without it)', () => {
      for (const control of Array.from(element.querySelectorAll('input, ion-input, button'))) {
        expect(control.getAttribute('tabindex')).toBe('0');
      }
    });

    it('shows no errors and no result block before the first attempt', () => {
      expect(errors()).toEqual([]);
      expect(result()).toBeNull();
    });
  });

  describe('validation before the request', () => {
    it('requires every field, with the errors under their fields, and sends nothing', async () => {
      await submit();
      expect(errors()).toEqual([text.error.currentRequired, text.error.passwordRequired, text.error.passwordRepeatRequired]);
      http.expectNone(passwordUrl);
      expect(result()).toBeNull();
    });

    it.each([
      ['too short', 'Abc1!'],
      ['with no uppercase letter', 'abcde1!x'],
      ['with no lowercase letter', 'ABCDE1!X'],
      ['with no digit', 'Abcdef!x'],
      ['with no special character', 'Abcdef1x'],
    ])('rejects a new password %s', async (_name, password) => {
      fill({ current: 'Old1!aaa', password, repeat: password });
      await submit();
      expect(errors()).toEqual([text.error.passwordWeak]);
      http.expectNone(passwordUrl);
    });

    it('rejects a new password equal to the current one, under the new password field', async () => {
      fill({ current: 'Same1!aaa', password: 'Same1!aaa', repeat: 'Same1!aaa' });
      await submit();
      expect(errors()).toEqual([text.error.sameAsCurrent]);
      expect(element.querySelector('.password-fields__error')?.textContent).toContain(text.error.sameAsCurrent);
      http.expectNone(passwordUrl);
    });

    it('rejects a repeat that does not match', async () => {
      fill({ ...valid, repeat: 'New1!bbc' });
      await submit();
      expect(errors()).toEqual([text.error.passwordMismatch]);
      http.expectNone(passwordUrl);
    });

    it('does not trim the passwords: a space is a character', async () => {
      fill({ current: ' Old1!aaa', password: 'New1!bbb ', repeat: 'New1!bbb ' });
      const done = submit();
      const request = http.expectOne(passwordUrl);
      expect(request.request.body).toEqual({ currentPassword: ' Old1!aaa', newPassword: 'New1!bbb ' });
      request.flush(null, { status: 204, statusText: 'No Content' });
      await done;
    });

    it('updates the errors while typing after the first attempt', async () => {
      await submit();
      expect(errors()).toHaveLength(3);
      fill(valid);
      fixture.detectChanges();
      expect(errors()).toEqual([]);
    });
  });

  describe('the request', () => {
    it('sends the current and the new password once the form is valid', async () => {
      fill(valid);
      const done = submit();
      const request = http.expectOne(passwordUrl);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ currentPassword: 'Old1!aaa', newPassword: 'New1!bbb' });
      request.flush(null, { status: 204, statusText: 'No Content' });
      await done;
    });

    it('disables the button and shows the saving text while the request is running, and does not send twice', async () => {
      fill(valid);
      const done = submit();
      fixture.detectChanges();
      const button = element.querySelector('.settings-submit') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(button.textContent?.trim()).toBe(text.saving);

      void page.submit();
      http.expectOne(passwordUrl).flush(null, { status: 204, statusText: 'No Content' });
      await done;
      fixture.detectChanges();
      expect(button.disabled).toBe(false);
      expect(button.textContent?.trim()).toBe(text.submit);
    });

    it('shows a green success block above the first field and clears the fields', async () => {
      fill(valid);
      const done = submit();
      http.expectOne(passwordUrl).flush(null, { status: 204, statusText: 'No Content' });
      await done;
      fixture.detectChanges();

      const block = result()!;
      expect(block.textContent?.trim()).toBe(text.result.success);
      expect(block.classList).toContain('settings-result--success');
      expect(block.getAttribute('role')).toBe('status');
      // Блок — під заголовком і над полем «поточний пароль».
      const title = element.querySelector('.settings-section__title')!;
      const field = element.querySelector('.settings-field')!;
      expect(title.compareDocumentPosition(block) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(block.compareDocumentPosition(field) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

      expect((element.querySelector('#settings-current-password') as HTMLInputElement).value).toBe('');
      expect(errors()).toEqual([]);
    });

    it('does not leave the cleared new password fields marked as touched (they would turn red)', async () => {
      const inputs = () => Array.from(element.querySelectorAll('app-password-fields ion-input'));
      fill(valid);
      fixture.detectChanges();
      // Користувач побував у полях і пішов з них.
      for (const input of inputs()) {
        input.dispatchEvent(new CustomEvent('ionBlur'));
      }
      fixture.detectChanges();
      expect(inputs().every((i) => i.classList.contains('ng-touched'))).toBe(true);

      const done = submit();
      http.expectOne(passwordUrl).flush(null, { status: 204, statusText: 'No Content' });
      await done;
      fixture.detectChanges();
      await fixture.whenStable();

      expect(inputs().map((i) => i.classList.contains('ng-touched'))).toEqual([false, false]);
      expect(inputs().map((i) => i.classList.contains('ng-pristine'))).toEqual([true, true]);
    });

    it.each([
      [400, text.result.failed],
      [401, text.result.failed],
      [403, text.result.failed],
      [500, text.result.error],
    ])('shows a red block for HTTP %i and keeps what was typed', async (status, expected) => {
      fill(valid);
      const done = submit();
      http.expectOne(passwordUrl).flush({}, { status, statusText: 'x' });
      await done;
      fixture.detectChanges();

      const block = result()!;
      expect(block.textContent?.trim()).toBe(expected);
      expect(block.classList).not.toContain('settings-result--success');
      expect(block.getAttribute('role')).toBe('alert');
      expect((element.querySelector('#settings-current-password') as HTMLInputElement).value).toBe('Old1!aaa');
    });

    it('says that the server does not answer', async () => {
      fill(valid);
      const done = submit();
      http.expectOne(passwordUrl).error(new ProgressEvent('error'));
      await done;
      fixture.detectChanges();
      expect(result()?.textContent?.trim()).toBe(text.result.unreachable);
    });

    it('hides the previous result as soon as a field is edited', async () => {
      fill(valid);
      const done = submit();
      http.expectOne(passwordUrl).flush({}, { status: 400, statusText: 'Bad Request' });
      await done;
      fixture.detectChanges();
      expect(result()).not.toBeNull();

      const input = element.querySelector('#settings-current-password') as HTMLInputElement;
      input.value = 'Other1!aa';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(result()).toBeNull();
    });

    it('does not submit the form natively', () => {
      const event = new Event('submit', { cancelable: true });
      (element.querySelector('form') as HTMLFormElement).dispatchEvent(event);
      expect(event.defaultPrevented).toBe(true);
    });
  });
});
