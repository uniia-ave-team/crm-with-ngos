import { Component } from '@angular/core';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ColleaguesPageModule } from '../colleagues.module';
import { PERMISSION_GROUPS } from '../../core/roles/role-permissions';
import { Role } from '../../core/roles/roles.service';
import { NewRole } from './role-form.component';

@Component({
  template: `<app-role-form [role]="role" (save)="saved.push($event)" />`,
  imports: [ColleaguesPageModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly saved: NewRole[] = [];
  role: Role | null = null;
}

describe('RoleFormComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render(role: Role | null = null) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.role = role;
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = (id: string) => element.querySelector(`#${id}`) as HTMLInputElement;
    const type = (id: string, value: string) => {
      const field = input(id);
      field.value = value;
      field.dispatchEvent(new Event('input'));
    };
    const submit = () => {
      (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
      fixture.detectChanges();
    };
    return { fixture, host: fixture.componentInstance, element, input, type, submit };
  }

  it('has the title and the three text fields: name, feminitive and plural, with labels from the language file', () => {
    const { element, input } = render();
    expect(element.querySelector('h2')?.textContent?.trim()).toBe(uk.colleagues.roleForm.title);
    const labels = Array.from(element.querySelectorAll('label.settings-field__label')).map((l) => l.textContent?.replaceAll('*', '').trim());
    expect(labels).toEqual([uk.colleagues.roleForm.name, uk.colleagues.roleForm.feminitive, uk.colleagues.roleForm.plural]);
    for (const id of ['role-form-name', 'role-form-feminitive', 'role-form-plural']) {
      expect(input(id).type).toBe('text');
      expect(input(id).getAttribute('tabindex')).toBe('0');
      expect(input(id).getAttribute('maxlength')).toBe('256');
    }
  });

  it('marks only the name as required: an asterisk with a tooltip, and the required attribute', () => {
    const { element, input } = render();
    const marks = element.querySelectorAll('.settings-field__required');
    expect(marks).toHaveLength(1);
    expect(element.querySelector('label[for=role-form-name]')?.contains(marks[0])).toBe(true);
    expect(marks[0].getAttribute('aria-hidden')).toBe('true');
    expect(marks[0].getAttribute('title')).toBe(uk.common.required);
    expect(input('role-form-name').required).toBe(true);
    expect(input('role-form-feminitive').required).toBe(false);
    expect(input('role-form-plural').required).toBe(false);
  });

  it('has the button "Add" that submits the form, with the text from the language file', () => {
    const { element } = render();
    const button = element.querySelector('button') as HTMLButtonElement;
    expect(button.type).toBe('submit');
    expect(button.textContent?.trim()).toBe(uk.colleagues.roleForm.submit);
    expect(button.getAttribute('tabindex')).toBe('0');
    expect(element.querySelectorAll('button')).toHaveLength(1);
  });

  describe('editing a role (the form is given the role)', () => {
    const role: Role = { id: 'r1', name: 'Куратор', feminitiveName: 'Кураторка', pluralName: null };

    it('fills the fields with the names of the role, an empty field for a name that is not there', () => {
      const { input } = render(role);
      expect(input('role-form-name').value).toBe('Куратор');
      expect(input('role-form-feminitive').value).toBe('Кураторка');
      expect(input('role-form-plural').value).toBe('');
    });

    it('has the title and the button of editing, texts from the language file', () => {
      const { element } = render(role);
      expect(element.querySelector('h2')?.textContent?.trim()).toBe(uk.colleagues.roleForm.editTitle);
      expect((element.querySelector('button[type=submit]') as HTMLButtonElement).textContent?.trim()).toBe(uk.colleagues.roleForm.save);
    });

    it('gives the changed data, and the old one when nothing is changed', () => {
      const { host, type, submit } = render(role);
      submit();
      type('role-form-plural', 'Куратори');
      submit();
      expect(host.saved).toEqual([
        { name: 'Куратор', feminitiveName: 'Кураторка', pluralName: null },
        { name: 'Куратор', feminitiveName: 'Кураторка', pluralName: 'Куратори' },
      ]);
    });

    it('does not change the form of a new role: empty fields, the title and the button of adding', () => {
      const { element, input } = render();
      expect(input('role-form-name').value).toBe('');
      expect(element.querySelector('h2')?.textContent?.trim()).toBe(uk.colleagues.roleForm.title);
      expect((element.querySelector('button[type=submit]') as HTMLButtonElement).textContent?.trim()).toBe(uk.colleagues.roleForm.submit);
    });
  });

  describe('the permission switches', () => {
    const switches = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLInputElement>('input[role=switch]'));

    it('has a switch for every group of permissions, named from the language file, all off at first', () => {
      const { element } = render();
      const names = Array.from(element.querySelectorAll('.role-form__permission-name')).map((n) => n.textContent?.trim());
      expect(names).toEqual(PERMISSION_GROUPS.map((group) => (uk.colleagues.roleForm.permissions as Record<string, string>)[group.id]));
      expect(names).toHaveLength(10);
      expect(switches(element)).toHaveLength(10);
      expect(switches(element).every((s) => !s.checked)).toBe(true);
      expect(element.querySelector('legend')?.textContent?.trim()).toBe(uk.colleagues.roleForm.permissionsTitle);
    });

    it('does not offer the permissions that everybody has or that are given automatically', () => {
      const all = PERMISSION_GROUPS.flatMap((group) => group.permissions);
      for (const right of ['ViewNgo', 'CreateNgo', 'ViewTableTemplate']) {
        expect(all).not.toContain(`Permissions.${right}`);
      }
    });

    it('puts the viewing of the login page images together with the editing of the organization', () => {
      const group = PERMISSION_GROUPS.find((candidate) => candidate.id === 'editNgo');
      expect(group?.permissions).toContain('Permissions.ViewLoginPageImages');
    });

    it('is placed above the button "Add"', () => {
      const { element } = render();
      const fieldset = element.querySelector('fieldset') as HTMLElement;
      const button = element.querySelector('button') as HTMLElement;
      expect(fieldset.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('turns on and off, each one on its own, and does not change what the form gives', () => {
      const { fixture, host, element, type, submit } = render();
      const [first, second] = switches(element);
      first.click();
      fixture.detectChanges();
      expect(first.checked).toBe(true);
      expect(second.checked).toBe(false);

      first.click();
      fixture.detectChanges();
      expect(first.checked).toBe(false);

      second.click();
      type('role-form-name', 'Член');
      submit();
      expect(host.saved).toEqual([{ name: 'Член', feminitiveName: null, pluralName: null }]);
    });
  });

  describe('submitting', () => {
    it('gives the typed values, trimmed', () => {
      const { host, type, submit } = render();
      type('role-form-name', '  Куратор ');
      type('role-form-feminitive', ' Кураторка');
      type('role-form-plural', 'Куратори  ');
      submit();
      expect(host.saved).toEqual([{ name: 'Куратор', feminitiveName: 'Кураторка', pluralName: 'Куратори' }]);
    });

    it('gives null for the optional fields that are empty or only spaces', () => {
      const { host, type, submit } = render();
      type('role-form-name', 'Член');
      type('role-form-plural', '   ');
      submit();
      expect(host.saved).toEqual([{ name: 'Член', feminitiveName: null, pluralName: null }]);
    });

    it('does not give anything, and says so under the name, when the name is empty or only spaces', () => {
      const { host, element, type, submit } = render();
      submit();
      type('role-form-name', '   ');
      submit();

      expect(host.saved).toEqual([]);
      const error = element.querySelector('.settings-field__error');
      expect(error?.textContent?.trim()).toBe(uk.colleagues.roleForm.error.nameRequired);
      expect(error?.getAttribute('role')).toBe('alert');
      expect(element.querySelector('label[for=role-form-name]')?.parentElement?.contains(error)).toBe(true);
    });

    it('shows no error before the first try, and takes it away when the name is typed', () => {
      const { fixture, element, type, submit } = render();
      expect(element.querySelector('.settings-field__error')).toBeNull();
      submit();
      expect(element.querySelector('.settings-field__error')).not.toBeNull();

      type('role-form-name', 'Член');
      fixture.detectChanges();
      expect(element.querySelector('.settings-field__error')).toBeNull();
    });

    it('can give the data again after the error is fixed', () => {
      const { host, type, submit } = render();
      submit();
      type('role-form-name', 'Член');
      submit();
      expect(host.saved).toEqual([{ name: 'Член', feminitiveName: null, pluralName: null }]);
    });
  });
});
