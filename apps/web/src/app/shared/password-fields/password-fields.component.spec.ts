import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { PasswordFieldsComponent } from './password-fields.component';
import { PasswordFieldsModule } from './password-fields.module';

// Тексти живуть в uk.json — беремо їх звідти, а не дублюємо. Напис полів і
// помилки — ті, що компонент отримує на формі адміністратора.
const requirements = uk.common.passwordRequirements;
const fieldLabels = { password: uk.login.admin.password, repeat: uk.login.admin.passwordRepeat };
const errorTexts = { weak: uk.login.admin.error.passwordWeak, mismatch: uk.login.admin.error.passwordMismatch };

@Component({
  template: `
    <app-password-fields
      [password]="password()"
      [repeat]="repeat()"
      [passwordLabel]="labels.password"
      [repeatLabel]="labels.repeat"
      [showLabels]="showLabels()"
      [required]="required()"
      [passwordError]="passwordError()"
      [repeatError]="repeatError()"
      (passwordChange)="password.set($event)"
      (repeatChange)="repeat.set($event)"
      (submitted)="submits = submits + 1"
    />
  `,
  imports: [PasswordFieldsModule],
})
class HostComponent {
  readonly labels = fieldLabels;
  readonly password = signal('');
  readonly repeat = signal('');
  readonly showLabels = signal(false);
  readonly required = signal(false);
  readonly passwordError = signal<string | null>(null);
  readonly repeatError = signal<string | null>(null);
  submits = 0;
}

describe('PasswordFieldsComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const inputs = () => Array.from(element.querySelectorAll('ion-input')) as HTMLIonInputElement[];
    const lists = () => Array.from(element.querySelectorAll('.password-fields__requirements'));
    const met = (index: number) => Array.from(lists()[index].querySelectorAll('.password-fields__requirement--met')).length;
    return { fixture, element, host: fixture.componentInstance, inputs, lists, met };
  }

  it('renders two password inputs with the labels as placeholders and aria-labels', () => {
    const { inputs } = render();
    expect(inputs()).toHaveLength(2);
    expect(inputs().map((i) => i.type)).toEqual(['password', 'password']);
    expect(inputs().map((i) => i.placeholder)).toEqual([fieldLabels.password, fieldLabels.repeat]);
    expect(inputs().map((i) => i.getAttribute('aria-label'))).toEqual([fieldLabels.password, fieldLabels.repeat]);
    expect(inputs().map((i) => i.getAttribute('tabindex'))).toEqual(['0', '0']);
  });

  it('lists the five requirements, with icons and texts from the language file, and the match item under the repeat field', () => {
    const { lists } = render();
    const items = (index: number) => Array.from(lists()[index].querySelectorAll('.password-fields__requirement'));
    expect(items(0).map((i) => i.querySelector('.password-fields__requirement-icon')?.textContent?.trim())).toEqual(['8', 'a', 'A', '123', '#']);
    expect(items(0).map((i) => i.querySelector('span:not(.password-fields__requirement-icon)')?.textContent?.trim())).toEqual([
      requirements.length,
      requirements.lowercase,
      requirements.uppercase,
      requirements.digit,
      requirements.special,
    ]);
    expect(items(1)).toHaveLength(1);
    expect(items(1)[0].querySelector('.password-fields__requirement-icon')?.textContent?.trim()).toBe('=');
    expect(items(1)[0].textContent).toContain(requirements.match);
  });

  it('ticks off the requirements as the password changes', () => {
    const { fixture, host, met } = render();
    expect(met(0)).toBe(0);
    host.password.set('abc');
    fixture.detectChanges();
    expect(met(0)).toBe(1);
    host.password.set('Abcdef1!');
    fixture.detectChanges();
    expect(met(0)).toBe(5);
  });

  it('ticks off the match item only when the repeat equals a non-empty password', () => {
    const { fixture, host, met } = render();
    expect(met(1)).toBe(0);
    host.password.set('Abcdef1!');
    host.repeat.set('Abcdef1');
    fixture.detectChanges();
    expect(met(1)).toBe(0);
    host.repeat.set('Abcdef1!');
    fixture.detectChanges();
    expect(met(1)).toBe(1);
  });

  it('does not treat two empty fields as matching', () => {
    const { fixture, met } = render();
    fixture.detectChanges();
    expect(met(1)).toBe(0);
  });

  it('reports what is typed through the change events, turning empty values into an empty string', () => {
    const { fixture, host } = render();
    const instance = fixture.debugElement.query(By.directive(PasswordFieldsComponent)).componentInstance as {
      onPassword(value: string | number | null | undefined): void;
      onRepeat(value: string | number | null | undefined): void;
    };
    instance.onPassword('Abc');
    instance.onRepeat(123);
    expect([host.password(), host.repeat()]).toEqual(['Abc', '123']);
    instance.onPassword(null);
    instance.onRepeat(undefined);
    expect([host.password(), host.repeat()]).toEqual(['', '']);
  });

  it('reports Enter in a field', () => {
    const { host, inputs } = render();
    inputs()[0].dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter' }));
    inputs()[1].dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter' }));
    expect(host.submits).toBe(2);
  });

  it('shows the errors, given as language keys, under the fields', () => {
    const { fixture, element, host } = render();
    expect(element.querySelector('.password-fields__error')).toBeNull();
    host.passwordError.set('login.admin.error.passwordWeak');
    host.repeatError.set('login.admin.error.passwordMismatch');
    fixture.detectChanges();
    const errors = Array.from(element.querySelectorAll('.password-fields__error'));
    expect(errors.map((e) => e.textContent?.trim())).toEqual([errorTexts.weak, errorTexts.mismatch]);
    expect(errors.every((e) => e.getAttribute('role') === 'alert')).toBe(true);
  });

  it('shows visible labels above the fields (and no placeholders) when asked', () => {
    const { fixture, element, host, inputs } = render();
    expect(element.querySelector('.password-fields__label')).toBeNull();
    host.showLabels.set(true);
    fixture.detectChanges();
    const labels = Array.from(element.querySelectorAll('.password-fields__label'));
    expect(labels.map((l) => l.textContent?.trim())).toEqual([fieldLabels.password, fieldLabels.repeat]);
    // Напис вказує на своє поле.
    for (const label of labels) {
      expect(element.querySelector(`#${label.getAttribute('for')}`)).not.toBeNull();
    }
    expect(inputs().map((i) => i.placeholder)).toEqual(['', '']);
  });

  describe('required', () => {
    it('is off by default: no asterisks and no required attribute', () => {
      const { element, inputs } = render();
      expect(inputs().every((i) => !i.required)).toBe(true);
      expect(inputs().map((i) => i.placeholder)).toEqual([fieldLabels.password, fieldLabels.repeat]);
      expect(element.querySelector('.password-fields__required')).toBeNull();
    });

    it('adds an asterisk to the placeholders and the required attribute when the labels are in the fields', () => {
      const { fixture, host, inputs } = render();
      host.required.set(true);
      fixture.detectChanges();
      expect(inputs().every((i) => i.required)).toBe(true);
      expect(inputs().map((i) => i.placeholder)).toEqual([`${fieldLabels.password} *`, `${fieldLabels.repeat} *`]);
    });

    it('puts a visible asterisk after each label (hidden from screen readers) when the labels are above the fields', () => {
      const { fixture, element, host, inputs } = render();
      host.showLabels.set(true);
      host.required.set(true);
      fixture.detectChanges();
      const marks = Array.from(element.querySelectorAll('.password-fields__label .password-fields__required'));
      expect(marks).toHaveLength(2);
      expect(marks.every((m) => m.getAttribute('aria-hidden') === 'true' && m.textContent?.trim() === '*')).toBe(true);
      expect(inputs().every((i) => i.required)).toBe(true);
      // Підказок у полях нема, тож зірочка лише біля написів.
      expect(inputs().map((i) => i.placeholder)).toEqual(['', '']);
    });
  });

  it('gives every instance its own field ids', () => {
    const first = TestBed.createComponent(HostComponent);
    const second = TestBed.createComponent(HostComponent);
    first.detectChanges();
    second.detectChanges();
    const ids = [...Array.from((first.nativeElement as HTMLElement).querySelectorAll('ion-input')), ...Array.from((second.nativeElement as HTMLElement).querySelectorAll('ion-input'))].map(
      (i) => i.id,
    );
    expect(new Set(ids).size).toBe(4);
  });
});
