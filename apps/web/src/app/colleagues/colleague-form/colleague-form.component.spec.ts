import { Component, signal, viewChild } from '@angular/core';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ColleaguesPageModule } from '../colleagues.module';
import { RolesView } from '../roles-panel/roles-panel.component';
import { ColleagueFormComponent, NewColleague } from './colleague-form.component';

@Component({
  template: `<app-colleague-form #form [roles]="roles()" [saving]="saving()" [error]="error()" (save)="saved.push($event)" />`,
  imports: [ColleaguesPageModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly roles = signal<RolesView>({
    status: 'ready',
    roles: [
      { id: 'r1', name: 'Перша', feminitiveName: null, pluralName: null },
      { id: 'r2', name: 'Друга', feminitiveName: null, pluralName: null },
      { id: 'r3', name: 'Третя', feminitiveName: null, pluralName: null },
    ],
    failure: null,
  });
  readonly saved: NewColleague[] = [];
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = viewChild.required<ColleagueFormComponent>('form');
}

describe('ColleagueFormComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const email = () => element.querySelector('#colleague-form-email') as HTMLInputElement;
    const type = (value: string) => {
      email().value = value;
      email().dispatchEvent(new Event('input'));
    };
    const choices = () => Array.from(element.querySelectorAll<HTMLButtonElement>('.colleague-form__choice'));
    const submit = () => {
      (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
      fixture.detectChanges();
    };
    return { fixture, host: fixture.componentInstance, element, email, type, choices, submit };
  }

  it('has the title, the email field (required, with the mark) and the roles, texts from the language file', () => {
    const { element, email } = render();
    expect(element.querySelector('h2')?.textContent?.trim()).toBe(uk.colleagues.colleagueForm.title);
    const label = element.querySelector('label[for=colleague-form-email]') as HTMLElement;
    expect(label.textContent?.replaceAll('*', '').trim()).toBe(uk.colleagues.colleagueForm.email);
    expect(label.querySelector('.settings-field__required')?.getAttribute('title')).toBe(uk.common.required);
    expect(email().type).toBe('email');
    expect(email().required).toBe(true);
    expect(element.querySelector('legend')?.textContent?.replaceAll('*', '').trim()).toBe(uk.colleagues.colleagueForm.roles);
    expect((element.querySelector('button[type=submit]') as HTMLButtonElement).textContent?.trim()).toBe(uk.colleagues.colleagueForm.submit);
  });

  describe('the choice of the roles', () => {
    it('shows every role as a pill on a checkbox button, none chosen at first', () => {
      const { choices } = render();
      expect(choices().map((c) => c.textContent?.trim())).toEqual(['Перша', 'Друга', 'Третя']);
      expect(choices().every((c) => c.getAttribute('role') === 'checkbox' && c.getAttribute('aria-checked') === 'false')).toBe(true);
      expect(choices().every((c) => c.querySelector('.pill--active') === null)).toBe(true);
    });

    it('fills the pill of a chosen role (active) and empties it again when it is pressed once more', () => {
      const { fixture, choices } = render();
      choices()[1].click();
      fixture.detectChanges();
      expect(choices()[1].getAttribute('aria-checked')).toBe('true');
      expect(choices()[1].querySelector('.pill--active')).not.toBeNull();
      expect(choices()[0].querySelector('.pill--active')).toBeNull();

      choices()[1].click();
      fixture.detectChanges();
      expect(choices()[1].getAttribute('aria-checked')).toBe('false');
      expect(choices()[1].querySelector('.pill--active')).toBeNull();
    });

    it('shows an empty circle before the name of a role that is not chosen, and a checkmark circle when it is chosen', () => {
      const { fixture, choices } = render();
      const icon = (index: number) => (choices()[index].querySelector('ion-icon') as unknown as { name: string }).name;
      expect(icon(0)).toBe('ellipse-outline');

      choices()[0].click();
      fixture.detectChanges();
      expect(icon(0)).toBe('checkmark-circle');
      expect(icon(1)).toBe('ellipse-outline');

      choices()[0].click();
      fixture.detectChanges();
      expect(icon(0)).toBe('ellipse-outline');
    });

    it('lets choose several roles', () => {
      const { fixture, choices } = render();
      choices()[0].click();
      choices()[2].click();
      fixture.detectChanges();
      expect(choices().map((c) => c.getAttribute('aria-checked'))).toEqual(['true', 'false', 'true']);
    });

    it('has a tabindex on the buttons', () => {
      const { choices } = render();
      expect(choices().every((c) => c.getAttribute('tabindex') === '0')).toBe(true);
    });

    it('says that the roles are loading, or that there are none, or why they could not be loaded', () => {
      const { fixture, host, element } = render();
      host.roles.set({ status: 'loading', roles: [], failure: null });
      fixture.detectChanges();
      expect(element.querySelector('.colleague-form__message')?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.loading);

      host.roles.set({ status: 'ready', roles: [], failure: null });
      fixture.detectChanges();
      expect(element.querySelector('.colleague-form__message')?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.empty);

      host.roles.set({ status: 'failed', roles: [], failure: 'forbidden' });
      fixture.detectChanges();
      const error = element.querySelector('.colleague-form__message--error');
      expect(error?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.error.forbidden);
      expect(error?.getAttribute('role')).toBe('alert');
    });
  });

  describe('submitting', () => {
    it('gives the trimmed email and the chosen roles in the order of the list', () => {
      const { host, type, choices, submit } = render();
      type('  a@b.org ');
      choices()[2].click();
      choices()[0].click();
      submit();
      expect(host.saved).toEqual([{ email: 'a@b.org', roleIds: ['r1', 'r3'] }]);
    });

    it('does not give anything without a role (the server needs at least one), and says so under the roles', () => {
      const { host, element, type, submit } = render();
      type('a@b.org');
      submit();
      expect(host.saved).toEqual([]);
      const error = element.querySelector('.colleague-form__roles-error');
      expect(error?.textContent?.trim()).toBe(uk.colleagues.colleagueForm.error.rolesRequired);
      expect(error?.getAttribute('role')).toBe('alert');
      expect(element.querySelector('fieldset')?.contains(error)).toBe(true);
    });

    it('shows no roles error before the first try, and takes it away when a role is chosen', () => {
      const { fixture, element, choices, type, submit } = render();
      expect(element.querySelector('.colleague-form__roles-error')).toBeNull();
      type('a@b.org');
      submit();
      expect(element.querySelector('.colleague-form__roles-error')).not.toBeNull();
      choices()[0].click();
      fixture.detectChanges();
      expect(element.querySelector('.colleague-form__roles-error')).toBeNull();
    });

    it('marks the roles as required, like the email', () => {
      const { element } = render();
      const mark = element.querySelector('legend .settings-field__required') as HTMLElement;
      expect(mark.getAttribute('aria-hidden')).toBe('true');
      expect(mark.getAttribute('title')).toBe(uk.common.required);
    });

    it('lets the user choose and unchoose the role: with none left it does not give anything again', () => {
      const { fixture, host, choices, type, submit } = render();
      type('a@b.org');
      choices()[0].click();
      choices()[0].click();
      fixture.detectChanges();
      submit();
      expect(host.saved).toEqual([]);
    });

    it('does not give anything and says so under the field when the email is empty', () => {
      const { host, element, type, submit } = render();
      submit();
      type('   ');
      submit();
      expect(host.saved).toEqual([]);
      const error = element.querySelector('.settings-field__error');
      expect(error?.textContent?.trim()).toBe(uk.colleagues.colleagueForm.error.emailRequired);
      expect(error?.getAttribute('role')).toBe('alert');
    });

    it.each(['abc', 'a@b', 'a b@c.org', '@b.org'])('does not give "%s" as an email', (value) => {
      const { host, element, type, submit } = render();
      type(value);
      submit();
      expect(host.saved).toEqual([]);
      expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(uk.colleagues.colleagueForm.error.emailInvalid);
    });

    it('shows no error before the first try, and takes it away when the email and the role are fixed', () => {
      const { fixture, element, choices, type, submit } = render();
      expect(element.querySelector('.settings-field__error')).toBeNull();
      submit();
      expect(element.querySelector('.settings-field__error')).not.toBeNull();
      type('a@b.org');
      choices()[0].click();
      fixture.detectChanges();
      expect(element.querySelector('.settings-field__error')).toBeNull();
    });
  });

  describe('sending', () => {
    const button = (element: HTMLElement) => element.querySelector('button[type=submit]') as HTMLButtonElement;

    it('has the button free, and no error, at first', () => {
      const { element } = render();
      expect(button(element).disabled).toBe(false);
      expect(element.querySelector('form > .settings-field__error')).toBeNull();
    });

    it('locks the button while it is sending, and a second submit gives nothing', () => {
      const { fixture, host, element, choices, type, submit } = render();
      type('a@b.org');
      choices()[0].click();
      host.saving.set(true);
      fixture.detectChanges();
      expect(button(element).disabled).toBe(true);

      submit();
      expect(host.saved).toEqual([]);

      host.saving.set(false);
      fixture.detectChanges();
      expect(button(element).disabled).toBe(false);
      submit();
      expect(host.saved).toHaveLength(1);
    });

    it('shows the error that it is given above the button, and takes it away when there is none', () => {
      const { fixture, host, element } = render();
      host.error.set('colleagues.colleagueForm.saveError.forbidden');
      fixture.detectChanges();
      const error = element.querySelector('form > .settings-field__error') as HTMLElement;
      expect(error.textContent?.trim()).toBe(uk.colleagues.colleagueForm.saveError.forbidden);
      expect(error.getAttribute('role')).toBe('alert');
      expect(error.compareDocumentPosition(button(element)) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

      host.error.set(null);
      fixture.detectChanges();
      expect(element.querySelector('form > .settings-field__error')).toBeNull();
    });

    it('clears the email, the chosen roles and the errors by reset()', () => {
      const { fixture, host, element, email, choices, type, submit } = render();
      type('a@b.org');
      choices()[1].click();
      submit();
      expect(host.saved).toHaveLength(1);

      host.form().reset();
      fixture.detectChanges();
      expect(email().value).toBe('');
      expect(choices().every((c) => c.getAttribute('aria-checked') === 'false')).toBe(true);
      submit();
      // Після очищення форма знову порожня: пошту й роль треба вводити заново.
      expect(host.saved).toHaveLength(1);
      expect(element.querySelector('.settings-field__error')).not.toBeNull();
    });

    it('shows no errors right after reset() until the next try', () => {
      const { fixture, host, element, submit } = render();
      submit();
      expect(element.querySelectorAll('.settings-field__error').length).toBeGreaterThan(0);
      host.form().reset();
      fixture.detectChanges();
      expect(element.querySelector('.settings-field__error')).toBeNull();
    });
  });
});
