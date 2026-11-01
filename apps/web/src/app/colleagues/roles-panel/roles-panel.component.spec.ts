import { Component, signal } from '@angular/core';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ColleaguesPageModule } from '../colleagues.module';
import { Role } from '../../core/roles/roles.service';
import { RolesView } from './roles-panel.component';

@Component({
  template: `<app-roles-panel [view]="view()" [canManage]="canManage()" (roleEdit)="edits.push($event)" (roleDelete)="deletes.push($event)" />`,
  imports: [ColleaguesPageModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly view = signal<RolesView>({ status: 'loading', roles: [], failure: null });
  readonly canManage = signal(true);
  readonly edits: Role[] = [];
  readonly deletes: Role[] = [];
}

describe('RolesPanelComponent', () => {
  const role = (id: string, name: string) => ({ id, name, feminitiveName: null, pluralName: null });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render(view: RolesView, canManage = true) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.view.set(view);
    fixture.componentInstance.canManage.set(canManage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const pills = () => Array.from(element.querySelectorAll('app-role-pill .pill')).map((pill) => pill.textContent?.trim());
    return { fixture, host: fixture.componentInstance, element, pills };
  }

  it('has the title from the language file', () => {
    expect(render({ status: 'ready', roles: [], failure: null }).element.querySelector('h2')?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.title);
  });

  it('shows the roles as pills, in the given order', () => {
    const { pills } = render({ status: 'ready', roles: [role('1', 'Куратор'), role('2', 'Член')], failure: null });
    expect(pills()).toEqual(['Куратор', 'Член']);
  });

  describe('the buttons "Edit" and "Delete" at the right of the role', () => {
    const view: RolesView = { status: 'ready', roles: [role('1', 'Куратор'), role('2', 'Член')], failure: null };

    it('are in every row, after the pill, the pencil first and then the trash bin, with labels from the language file and a tabindex', () => {
      const { element } = render(view);
      const items = Array.from(element.querySelectorAll('.roles__item'));
      expect(items).toHaveLength(2);
      for (const item of items) {
        const pill = item.querySelector('app-role-pill') as HTMLElement;
        const edit = item.querySelector('.roles__action--edit') as HTMLButtonElement;
        const remove = item.querySelector('.roles__action--delete') as HTMLButtonElement;
        expect(pill.compareDocumentPosition(edit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
        expect(edit.compareDocumentPosition(remove) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
        expect(edit.getAttribute('aria-label')).toBe(uk.colleagues.rolesPanel.edit);
        expect(remove.getAttribute('aria-label')).toBe(uk.colleagues.rolesPanel.delete);
        expect(edit.getAttribute('tabindex')).toBe('0');
        expect(remove.getAttribute('tabindex')).toBe('0');
        expect((edit.querySelector('ion-icon') as unknown as { name: string }).name).toBe('create-outline');
        expect((remove.querySelector('ion-icon') as unknown as { name: string }).name).toBe('trash-outline');
      }
    });

    it('emits the role of the row when "Edit" is pressed, and nothing else', () => {
      const { fixture, host, element } = render(view);
      (element.querySelectorAll('.roles__action--edit')[1] as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(host.edits).toEqual([view.roles[1]]);
      expect(host.deletes).toEqual([]);
    });

    it('emits the role of the row when "Delete" is pressed, and nothing else', () => {
      const { fixture, host, element } = render(view);
      (element.querySelectorAll('.roles__action--delete')[0] as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(host.deletes).toEqual([view.roles[0]]);
      expect(host.edits).toEqual([]);
    });

    it('are not drawn when the user may not manage the roles (the roles themselves are shown)', () => {
      const { element, pills } = render(view, false);
      expect(pills()).toEqual(['Куратор', 'Член']);
      expect(element.querySelector('.roles__actions')).toBeNull();
      expect(element.querySelector('.roles__action')).toBeNull();
    });

    it('appear and disappear together with the right', () => {
      const { fixture, host, element } = render(view, false);
      host.canManage.set(true);
      fixture.detectChanges();
      expect(element.querySelectorAll('.roles__action')).toHaveLength(4);
      host.canManage.set(false);
      fixture.detectChanges();
      expect(element.querySelectorAll('.roles__action')).toHaveLength(0);
    });

    it('are not drawn when there are no roles', () => {
      const { element } = render({ status: 'ready', roles: [], failure: null });
      expect(element.querySelector('.roles__action')).toBeNull();
    });
  });

  it('shows that the roles are loading, while there is no list yet', () => {
    const { element, pills } = render({ status: 'loading', roles: [], failure: null });
    const message = element.querySelector('.roles__message');
    expect(message?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.loading);
    expect(message?.getAttribute('role')).toBe('status');
    expect(pills()).toEqual([]);
  });

  it('keeps the roles that were already there while they are loaded again (no loading message over them)', () => {
    const { element, pills } = render({ status: 'loading', roles: [role('1', 'Куратор')], failure: null });
    expect(pills()).toEqual(['Куратор']);
    expect(element.querySelector('.roles__message')).toBeNull();
  });

  it('says that there are no roles when the list is empty', () => {
    const { element } = render({ status: 'ready', roles: [], failure: null });
    expect(element.querySelector('.roles__message')?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.empty);
  });

  it.each(['invalid', 'unauthorized', 'forbidden', 'unreachable', 'error'] as const)('shows the error text for %s from the language file, as an alert', (failure) => {
    const { element } = render({ status: 'failed', roles: [], failure });
    const message = element.querySelector('.roles__message--error');
    expect(message?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.error[failure]);
    expect(message?.getAttribute('role')).toBe('alert');
  });

  it('shows the roles that were there together with the error when a refresh failed', () => {
    const { element, pills } = render({ status: 'failed', roles: [role('1', 'Куратор')], failure: 'unreachable' });
    expect(pills()).toEqual(['Куратор']);
    expect(element.querySelector('.roles__message--error')).not.toBeNull();
  });

  it('does not show the empty text under an error', () => {
    const { element } = render({ status: 'failed', roles: [], failure: 'error' });
    expect(element.textContent).not.toContain(uk.colleagues.rolesPanel.empty);
  });
});
