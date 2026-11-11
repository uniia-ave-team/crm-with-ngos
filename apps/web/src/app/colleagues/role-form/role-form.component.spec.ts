import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../../core/accounts/accounts.service';
import { ConnectionService } from '../../core/connection/connection.service';
import { ColleaguesPageModule } from '../colleagues.module';
import { PERMISSION_GROUPS } from '../../core/roles/role-permissions';
import { Role } from '../../core/roles/roles.service';
import { NewRole } from './role-form.component';

@Component({
  template: `<app-role-form [role]="role" [saving]="saving()" [error]="error()" (save)="saved.push($event)" />`,
  imports: [ColleaguesPageModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly saved: NewRole[] = [];
  role: Role | null = null;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
}

/** Усі права, які пропонує форма: користувач із ними може надати будь-який тумблер. */
const ALL_RIGHTS = PERMISSION_GROUPS.flatMap((group) => group.permissions);

describe('RoleFormComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  afterEach(() => localStorage.clear());

  /** Форма користувача з такими правами (права активного акаунта); за замовчуванням є всі. */
  function render(role: Role | null = null, permissions: readonly string[] = ALL_RIGHTS) {
    const accounts = TestBed.inject(AccountsService);
    accounts.add({ server: TestBed.inject(ConnectionService).serverUrl(), accessToken: 'a', refreshToken: 'r', identity: 'u1' });
    accounts.update(accounts.activeId()!, { permissions });
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
        { name: 'Куратор', feminitiveName: 'Кураторка', pluralName: null, permissions: [] },
        { name: 'Куратор', feminitiveName: 'Кураторка', pluralName: 'Куратори', permissions: [] },
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

    it('turns on and off, each one on its own, and gives the permissions of the groups that are on', () => {
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
      expect(host.saved).toEqual([
        { name: 'Член', feminitiveName: null, pluralName: null, permissions: PERMISSION_GROUPS[1].permissions },
      ]);
    });
  });

  describe('the permissions the user can give (only those he has himself)', () => {
    const switches = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLInputElement>('input[role=switch]'));
    const idsOfLocked = (element: HTMLElement) =>
      switches(element).flatMap((input, index) => (input.disabled ? [PERMISSION_GROUPS[index].id] : []));

    it('lets switch on every group when the user has all the permissions', () => {
      const { element } = render();
      expect(idsOfLocked(element)).toEqual([]);
    });

    it('locks every switch for a user without permissions, with the hint, and the rows look dimmed', () => {
      const { element } = render(null, []);
      expect(switches(element).every((input) => input.disabled)).toBe(true);
      const rows = Array.from(element.querySelectorAll<HTMLElement>('.role-form__permission'));
      expect(rows.every((row) => row.classList.contains('role-form__permission--locked'))).toBe(true);
      expect(rows[0].getAttribute('title')).toBe(uk.colleagues.roleForm.permissionLocked);
    });

    it('locks only the groups whose permission the user does not have, the rest stay free and without the hint', () => {
      const { element } = render(null, ['Permissions.ViewUser', 'Permissions.CanResetPassword']);
      expect(idsOfLocked(element)).toEqual(PERMISSION_GROUPS.map((group) => group.id).filter((id) => id !== 'viewUsers' && id !== 'resetPasswords'));
      const rows = Array.from(element.querySelectorAll<HTMLElement>('.role-form__permission'));
      expect(rows[0].hasAttribute('title')).toBe(false);
      expect(rows[1].getAttribute('title')).toBe(uk.colleagues.roleForm.permissionLocked);
    });

    it('needs ALL the permissions of a merged group: one of them is not enough', () => {
      const { element } = render(null, ['Permissions.CreateUser']);
      expect(idsOfLocked(element)).toContain('editUsers');
    });

    it('lets give a merged group when the user has every permission of it', () => {
      const group = PERMISSION_GROUPS.find((candidate) => candidate.id === 'editNgo')!;
      const { element } = render(null, group.permissions);
      expect(idsOfLocked(element)).not.toContain('editNgo');
      expect(idsOfLocked(element)).toHaveLength(PERMISSION_GROUPS.length - 1);
    });

    it('does not let a merged group with only the last permission missing (the rights of the group are checked one by one)', () => {
      const group = PERMISSION_GROUPS.find((candidate) => candidate.id === 'manageTables')!;
      const { element } = render(null, group.permissions.slice(0, -1));
      expect(idsOfLocked(element)).toContain('manageTables');
    });

    it('does not turn on a locked switch by a click', () => {
      const { fixture, element } = render(null, []);
      const [first] = switches(element);
      first.click();
      fixture.detectChanges();
      expect(first.checked).toBe(false);
    });

    it('turns a locked switch back off when the change event comes anyway (the disabled attribute was bypassed)', () => {
      const { fixture, element } = render(null, []);
      const [first] = switches(element);
      first.checked = true;
      first.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      expect(first.checked).toBe(false);
    });

    it('follows the permissions of the user when they change: the switch gets free', () => {
      const { fixture, element } = render(null, []);
      expect(switches(element)[0].disabled).toBe(true);

      const accounts = TestBed.inject(AccountsService);
      accounts.update(accounts.activeId()!, { permissions: ['Permissions.ViewUser'] });
      fixture.detectChanges();
      expect(switches(element)[0].disabled).toBe(false);
    });
  });

  describe('the switches when a role is edited (they follow the claims of the role)', () => {
    const base: Role = { id: 'r1', name: 'Куратор', feminitiveName: null, pluralName: null };
    const withClaims = (...claims: string[]) => ({ ...base, claims }) as Role;
    const switches = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLInputElement>('input[role=switch]'));
    const idsOn = (element: HTMLElement) => switches(element).flatMap((input, index) => (input.checked ? [PERMISSION_GROUPS[index].id] : []));

    it('switches a group on when the role has every permission of it, and not when it has a part or none', () => {
      const { element } = render(
        withClaims('Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.DisableUser'),
      );
      // viewUsers — одне право є; editUsers — лише CreateUser (частина); disableUsers — лише DisableUser (частина).
      expect(idsOn(element)).toEqual(['viewUsers']);
    });

    it('switches on a merged group when the role has all of its permissions', () => {
      const { element } = render(withClaims('Permissions.CreateUser', 'Permissions.UpdateUser'));
      expect(idsOn(element)).toEqual(['editUsers']);
    });

    it('does not care about the claims that are not in the groups (the permissions without settings)', () => {
      const { element } = render(withClaims('Permissions.ViewNgo', 'Permissions.ViewTableTemplate', 'Permissions.Unknown'));
      expect(idsOn(element)).toEqual([]);
    });

    it('has all the switches off for a role given without claims (the form of the list, not of the request)', () => {
      const { element } = render(base);
      expect(idsOn(element)).toEqual([]);
    });

    it('lets the user turn off a group of the role that he has the rights for, and turn on another one', () => {
      const { fixture, element } = render(withClaims('Permissions.ViewUser'));
      switches(element)[0].click();
      switches(element)[3].click();
      fixture.detectChanges();
      expect(idsOn(element)).toEqual(['resetPasswords']);
    });

    it('locks by the rights of the user, but a locked group keeps the state of the role (on or off)', () => {
      const { element } = render(withClaims('Permissions.ViewUser', 'Permissions.CanResetPassword'), ['Permissions.CanResetPassword']);
      const [viewUsers, , , resetPasswords] = switches(element);
      expect(viewUsers.disabled).toBe(true);
      expect(viewUsers.checked).toBe(true);
      expect(resetPasswords.disabled).toBe(false);
      expect(resetPasswords.checked).toBe(true);
    });

    it('keeps a locked group on when a change comes anyway, and keeps a locked off group off', () => {
      const { fixture, element } = render(withClaims('Permissions.ViewUser'), []);
      const [on, off] = switches(element);
      on.checked = false;
      on.dispatchEvent(new Event('change'));
      off.checked = true;
      off.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      expect(on.checked).toBe(true);
      expect(off.checked).toBe(false);
    });

    it('gives the permissions of the groups that are on when it is saved, the locked ones included', () => {
      const { fixture, host, element, submit } = render(withClaims('Permissions.ViewUser'), ['Permissions.CanResetPassword']);
      switches(element)[3].click();
      fixture.detectChanges();
      submit();
      expect(host.saved[0].permissions).toEqual(['Permissions.ViewUser', 'Permissions.CanResetPassword']);
    });
  });

  describe('the warning for a group the role has only a part of', () => {
    const base: Role = { id: 'r1', name: 'Куратор', feminitiveName: null, pluralName: null };
    const withClaims = (...claims: string[]) => ({ ...base, claims }) as Role;
    const warnings = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLElement>('.role-form__partial'));
    const rowOf = (element: HTMLElement, id: string) =>
      element.querySelectorAll('.role-form__permission')[PERMISSION_GROUPS.findIndex((group) => group.id === id)] as HTMLElement;

    it('puts the icon alert-circle-outline in the row of a group that has only a part of its permissions', () => {
      const { element } = render(withClaims('Permissions.CreateUser'));
      expect(warnings(element)).toHaveLength(1);
      expect(rowOf(element, 'editUsers').contains(warnings(element)[0])).toBe(true);
      const icon = warnings(element)[0].querySelector('ion-icon') as unknown as HTMLElement & { name: string };
      expect(icon.name).toBe('alert-circle-outline');
    });

    it('has the explanation of the problem and the note that updating fixes it, from the language file, shown as a tooltip', () => {
      const { element } = render(withClaims('Permissions.CreateUser'));
      const tip = element.querySelector('.role-form__partial-tip') as HTMLElement;
      expect(tip.getAttribute('role')).toBe('tooltip');
      const lines = Array.from(tip.querySelectorAll('span')).map((line) => line.textContent?.trim());
      expect(lines).toEqual([uk.colleagues.roleForm.partialTitle, uk.colleagues.roleForm.partialNote]);
      expect(uk.colleagues.roleForm.partialNote).toContain('виправлено автоматично');
    });

    it('hides the tooltip until the icon is hovered or focused (only the styles show it)', () => {
      const { element } = render(withClaims('Permissions.CreateUser'));
      expect(getComputedStyle(element.querySelector('.role-form__partial-tip') as HTMLElement).display).toBe('none');
    });

    it('can be reached by the keyboard and is read by screen readers with both sentences', () => {
      const { element } = render(withClaims('Permissions.CreateUser'));
      const icon = warnings(element)[0];
      expect(icon.getAttribute('tabindex')).toBe('0');
      expect(icon.getAttribute('role')).toBe('img');
      expect(icon.getAttribute('aria-label')).toBe(`${uk.colleagues.roleForm.partialTitle} ${uk.colleagues.roleForm.partialNote}`);
    });

    it('marks every group with a part of its permissions, and only those', () => {
      const { element } = render(withClaims('Permissions.CreateUser', 'Permissions.DisableUser', 'Permissions.ViewCustomTable'));
      // editUsers і disableUsers — по одному з двох прав; viewTables — єдине право є, тож повна група.
      expect(warnings(element)).toHaveLength(2);
      expect(rowOf(element, 'editUsers').querySelector('.role-form__partial')).not.toBeNull();
      expect(rowOf(element, 'disableUsers').querySelector('.role-form__partial')).not.toBeNull();
      expect(rowOf(element, 'viewTables').querySelector('.role-form__partial')).toBeNull();
    });

    it('marks a merged group that has all permissions but one', () => {
      const group = PERMISSION_GROUPS.find((candidate) => candidate.id === 'manageTables')!;
      const { element } = render(withClaims(...group.permissions.slice(0, -1)));
      expect(rowOf(element, 'manageTables').querySelector('.role-form__partial')).not.toBeNull();
    });

    it('does not mark a group the role has completely, or has nothing of', () => {
      const { element } = render(withClaims('Permissions.CreateUser', 'Permissions.UpdateUser', 'Permissions.ViewNgo'));
      expect(warnings(element)).toHaveLength(0);
    });

    it('does not show it for a new role, nor for a role given without claims', () => {
      expect(warnings(render().element)).toHaveLength(0);
      expect(warnings(render(base).element)).toHaveLength(0);
    });

    it('keeps the switch of the group off, and a click on the icon does not turn it on', () => {
      const { fixture, element } = render(withClaims('Permissions.CreateUser'));
      const input = rowOf(element, 'editUsers').querySelector('input[role=switch]') as HTMLInputElement;
      expect(input.checked).toBe(false);
      warnings(element)[0].click();
      fixture.detectChanges();
      expect(input.checked).toBe(false);
    });

    it('shows the icon also in a group that is locked for the user', () => {
      const { element } = render(withClaims('Permissions.CreateUser'), []);
      expect(rowOf(element, 'editUsers').querySelector('.role-form__partial')).not.toBeNull();
      expect((rowOf(element, 'editUsers').querySelector('input[role=switch]') as HTMLInputElement).disabled).toBe(true);
    });

    it('gives no permission of the group on save while its switch is off', () => {
      const { host, submit } = render(withClaims('Permissions.CreateUser'));
      submit();
      expect(host.saved[0].permissions).toEqual([]);
    });
  });

  describe('what the form gives with the permissions', () => {
    const switches = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLInputElement>('input[role=switch]'));

    it('gives the permissions of every switched on group in the order of the groups, whatever the order of the clicks', () => {
      const { fixture, host, element, type, submit } = render();
      switches(element)[1].click();
      switches(element)[0].click();
      fixture.detectChanges();
      type('role-form-name', 'Член');
      submit();
      expect(host.saved[0].permissions).toEqual(['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.UpdateUser']);
    });

    it('gives every permission of a merged group', () => {
      const { fixture, host, element, type, submit } = render();
      const index = PERMISSION_GROUPS.findIndex((group) => group.id === 'editNgo');
      switches(element)[index].click();
      fixture.detectChanges();
      type('role-form-name', 'Член');
      submit();
      expect(host.saved[0].permissions).toEqual(PERMISSION_GROUPS[index].permissions);
    });

    it('gives no permissions when no switch is on, and none of a switch that was turned off again', () => {
      const { fixture, host, element, type, submit } = render();
      type('role-form-name', 'Член');
      switches(element)[0].click();
      switches(element)[0].click();
      fixture.detectChanges();
      submit();
      expect(host.saved[0].permissions).toEqual([]);
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
      const { fixture, host, element, type, submit } = render();
      type('role-form-name', 'Член');
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
      host.error.set('colleagues.roleForm.saveError.forbidden');
      fixture.detectChanges();
      const error = element.querySelector('form > .settings-field__error') as HTMLElement;
      expect(error.textContent?.trim()).toBe(uk.colleagues.roleForm.saveError.forbidden);
      expect(error.getAttribute('role')).toBe('alert');
      expect(error.compareDocumentPosition(button(element)) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

      host.error.set(null);
      fixture.detectChanges();
      expect(element.querySelector('form > .settings-field__error')).toBeNull();
    });
  });

  describe('submitting', () => {
    it('gives the typed values, trimmed', () => {
      const { host, type, submit } = render();
      type('role-form-name', '  Куратор ');
      type('role-form-feminitive', ' Кураторка');
      type('role-form-plural', 'Куратори  ');
      submit();
      expect(host.saved).toEqual([{ name: 'Куратор', feminitiveName: 'Кураторка', pluralName: 'Куратори', permissions: [] }]);
    });

    it('gives null for the optional fields that are empty or only spaces', () => {
      const { host, type, submit } = render();
      type('role-form-name', 'Член');
      type('role-form-plural', '   ');
      submit();
      expect(host.saved).toEqual([{ name: 'Член', feminitiveName: null, pluralName: null, permissions: [] }]);
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
      expect(host.saved).toEqual([{ name: 'Член', feminitiveName: null, pluralName: null, permissions: [] }]);
    });
  });
});
