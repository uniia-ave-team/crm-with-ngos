import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { AccountsService } from '../core/accounts/accounts.service';
import { ConnectionService } from '../core/connection/connection.service';
import { PopoverComponent } from '../shared/popover/popover.component';
import { SidePanelComponent } from '../shared/side-panel/side-panel.component';
import { ColleaguesPageModule } from './colleagues.module';
import { ColleaguesPage } from './colleagues.page';

/** Права, з якими видно всі кнопки й три крапки. */
const FULL = ['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.UpdateUser', 'Permissions.ViewCustomFields'];
/** Усі права, з яких складаються три крапки в рядках. */
const ROW_ACTIONS = [
  'Permissions.UpdateUser',
  'Permissions.DisableUser',
  'Permissions.DeleteUser',
  'Permissions.AssignRoleToUser',
  'Permissions.CanResetPassword',
  'Permissions.ManageRole',
];

describe('ColleaguesPage', () => {
  beforeEach(async () => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [ColleaguesPageModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  /** Сторінка користувача з такими правами (права активного акаунта). */
  function render(permissions: string[] = FULL): { fixture: ComponentFixture<ColleaguesPage>; element: HTMLElement } {
    const accounts = TestBed.inject(AccountsService);
    const server = TestBed.inject(ConnectionService).serverUrl();
    accounts.add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1' });
    accounts.update(accounts.activeId()!, { permissions });
    const fixture = TestBed.createComponent(ColleaguesPage);
    fixture.detectChanges();
    // Колонки з шаблонами комірок зʼявляються після першого прогону розмітки.
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  const headerButtons = (element: HTMLElement) => Array.from(element.querySelectorAll('ion-header button'));

  it('should create', () => {
    expect(render().fixture.componentInstance).toBeTruthy();
  });

  describe('the header', () => {
    it('has the title, then the buttons "Roles", "Add" and the search, texts from the language file', () => {
      const { element } = render();
      expect(element.querySelector('ion-title')?.textContent?.trim()).toBe(uk.page.colleagues);
      const buttons = headerButtons(element);
      expect(buttons).toHaveLength(3);
      expect(buttons[0].textContent?.trim()).toBe(uk.colleagues.roles);
      expect(buttons[1].textContent?.trim()).toBe(uk.colleagues.add);
      expect(buttons[2].textContent?.trim()).toBe('');
      expect(buttons[2].getAttribute('aria-label')).toBe(uk.colleagues.search);
    });

    it('puts the buttons on the right side of the toolbar', () => {
      const group = render().element.querySelector('ion-header ion-buttons') as HTMLElement;
      expect(group.getAttribute('slot')).toBe('end');
      expect(group.querySelectorAll('button')).toHaveLength(3);
    });

    it('draws the icons: people for the roles, plus for adding, magnifier for the search', () => {
      const icons = Array.from(render().element.querySelectorAll('ion-header button ion-icon')).map((i) => i.getAttribute('name'));
      expect(icons).toEqual(['people', 'add-outline', 'search']);
    });

    it('gives every button a tabindex (Safari does not tab to the buttons without it)', () => {
      for (const button of Array.from(render().element.querySelectorAll('button'))) {
        expect(button.getAttribute('tabindex')).toBe('0');
      }
    });

    describe('the "Add" button', () => {
      it('is shown with Permissions.CreateUser', () => {
        const { element } = render(['Permissions.CreateUser']);
        expect(headerButtons(element).map((b) => b.textContent?.trim())).toContain(uk.colleagues.add);
      });

      it.each([[[]], [['Permissions.ViewUser', 'Permissions.UpdateUser', 'Permissions.ViewCustomFields']]])(
        'is hidden without it (rights: %j), the other buttons stay',
        (rights) => {
          const { element } = render(rights);
          const texts = headerButtons(element).map((b) => b.textContent?.trim());
          expect(texts).not.toContain(uk.colleagues.add);
          expect(texts).toContain(uk.colleagues.roles);
          expect(headerButtons(element)).toHaveLength(2);
        },
      );

      it('appears and disappears together with the right', () => {
        const { fixture, element } = render([]);
        const accounts = TestBed.inject(AccountsService);
        expect(headerButtons(element)).toHaveLength(2);

        accounts.update(accounts.activeId()!, { permissions: ['Permissions.CreateUser'] });
        fixture.detectChanges();
        expect(headerButtons(element)).toHaveLength(3);

        accounts.update(accounts.activeId()!, { permissions: [] });
        fixture.detectChanges();
        expect(headerButtons(element)).toHaveLength(2);
      });
    });
  });

  describe('the table', () => {
    it('has the columns name, position, email and phone, headers from the language file', () => {
      const headers = Array.from(render().element.querySelectorAll('th')).map((th) => th.textContent?.trim());
      expect(headers).toEqual([
        uk.colleagues.columns.name,
        uk.colleagues.columns.position,
        uk.colleagues.columns.email,
        uk.colleagues.columns.phone,
        '',
      ]);
    });

    it('draws the rows with the avatar, the name with the pronouns, the position and the contacts', () => {
      const rows = Array.from(render().element.querySelectorAll('tbody tr'));
      expect(rows.length).toBeGreaterThan(0);
      for (const row of rows) {
        expect(row.querySelector('app-avatar')).not.toBeNull();
        expect(row.querySelector('.person__name')?.textContent?.trim()).toBe(uk.colleagues.sample.name);
        expect(row.querySelector('.person__pronouns')?.textContent?.trim()).toBeTruthy();
        expect(row.querySelector('.badge')?.textContent?.trim()).toBe(uk.colleagues.sample.badge);
        const cells = row.querySelectorAll('td');
        expect(cells[2].textContent?.trim()).toBe(uk.colleagues.sample.email);
        expect(cells[3].textContent?.trim()).toBe(uk.colleagues.sample.phone);
      }
    });

    it('draws the lines of the position with a colored dot', () => {
      const { element } = render();
      const dots = Array.from(element.querySelectorAll('.position__dot'));
      expect(dots.length).toBeGreaterThan(0);
      expect(dots.every((d) => d.classList.contains('position__dot--green') || d.classList.contains('position__dot--blue'))).toBe(true);
      expect(element.querySelector('.position__line')?.textContent?.trim()).toBe(uk.colleagues.sample.line);
    });

    describe('the three dots at the end of the rows', () => {
      const menus = (element: HTMLElement) => element.querySelectorAll('tbody .data-table__menu');

      it('are in every row, as the last cell, with any of the six rights', () => {
        const { element } = render(['Permissions.UpdateUser']);
        const rows = element.querySelectorAll('tbody tr');
        expect(menus(element)).toHaveLength(rows.length);
        for (const row of Array.from(rows)) {
          expect(row.lastElementChild?.querySelector('.data-table__menu')).not.toBeNull();
        }
      });

      it.each(ROW_ACTIONS)('are shown when the user has only %s', (right) => {
        expect(menus(render([right]).element).length).toBeGreaterThan(0);
      });

      it('are hidden when the user has none of the six rights (the cell stays, empty)', () => {
        const { element } = render(['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.ViewCustomFields', 'Permissions.ViewEmergencyContact']);
        expect(menus(element)).toHaveLength(0);
        expect(element.querySelectorAll('tbody tr').length).toBeGreaterThan(0);
        expect(element.querySelector('tbody tr')?.lastElementChild?.classList).toContain('data-table__menu-cell');
      });

      it('appear and disappear together with the rights', () => {
        const { fixture, element } = render([]);
        const accounts = TestBed.inject(AccountsService);
        expect(menus(element)).toHaveLength(0);

        accounts.update(accounts.activeId()!, { permissions: ['Permissions.DeleteUser'] });
        fixture.detectChanges();
        expect(menus(element).length).toBeGreaterThan(0);
      });
    });

    describe('the plus at the end of the header row', () => {
      const plus = (element: HTMLElement) => element.querySelector('thead .data-table__add');

      it('is shown with Permissions.ViewCustomFields', () => {
        expect(plus(render(['Permissions.ViewCustomFields']).element)).not.toBeNull();
      });

      it('is hidden without it, whatever other rights there are', () => {
        const others = ['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.UpdateUser', 'Permissions.ViewEmergencyContact', ...ROW_ACTIONS];
        expect(plus(render(others).element)).toBeNull();
      });

      it('appears and disappears together with the right', () => {
        const { fixture, element } = render([]);
        const accounts = TestBed.inject(AccountsService);
        expect(plus(element)).toBeNull();

        accounts.update(accounts.activeId()!, { permissions: ['Permissions.ViewCustomFields'] });
        fixture.detectChanges();
        expect(plus(element)).not.toBeNull();
      });
    });
  });

  describe('the actions menu behind the three dots', () => {
    /** Пункти меню, які бачить користувач із такими правами (id у порядку меню). */
    const actionIds = (permissions: string[]) => {
      const page = render(permissions).fixture.componentInstance as unknown as { actions(): { id: string }[] };
      return page.actions().map((a) => a.id);
    };

    it('has all five items, in this order, for a user with all the rights', () => {
      expect(actionIds(ROW_ACTIONS)).toEqual(['permissions', 'edit', 'resetPassword', 'disable', 'delete']);
    });

    it.each([
      [['Permissions.AssignRoleToUser'], ['permissions']],
      [['Permissions.ManageRole'], ['permissions']],
      [['Permissions.AssignRoleToUser', 'Permissions.ManageRole'], ['permissions']],
      [['Permissions.UpdateUser'], ['edit']],
      [['Permissions.CanResetPassword'], ['resetPassword']],
      [['Permissions.DisableUser'], ['disable']],
      [['Permissions.DeleteUser'], ['delete']],
      [['Permissions.UpdateUser', 'Permissions.DeleteUser'], ['edit', 'delete']],
    ])('for the rights %j shows only %j', (rights, expected) => {
      expect(actionIds(rights)).toEqual(expected);
    });

    it('has no items, and so no three dots, without any of the rights', () => {
      expect(actionIds(['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.ViewCustomFields'])).toEqual([]);
    });

    it('takes the texts and the icons of the items from the language file keys', () => {
      const page = render(ROW_ACTIONS).fixture.componentInstance as unknown as { actions(): { labelKey: string; icon: string }[] };
      expect(page.actions().map((a) => a.labelKey)).toEqual([
        'colleagues.actions.permissions',
        'colleagues.actions.edit',
        'colleagues.actions.resetPassword',
        'colleagues.actions.disable',
        'colleagues.actions.delete',
      ]);
      expect(page.actions().map((a) => a.icon)).toEqual([
        'shield-checkmark-outline',
        'create-outline',
        'key-outline',
        'ban-outline',
        'trash-outline',
      ]);
      expect(Object.keys(uk.colleagues.actions)).toEqual(['permissions', 'edit', 'resetPassword', 'disable', 'delete']);
    });

    describe('the popover', () => {
      const popoverOf = (fixture: ComponentFixture<ColleaguesPage>) =>
        fixture.debugElement.query(By.directive(PopoverComponent)).componentInstance as PopoverComponent;
      const rowDots = (element: HTMLElement, index: number) =>
        element.querySelectorAll('tbody .data-table__menu')[index] as HTMLButtonElement;
      const activeRows = (element: HTMLElement) =>
        Array.from(element.querySelectorAll('tbody tr')).map((tr) => tr.classList.contains('data-table__row--active'));

      it('is closed at first', () => {
        expect(popoverOf(render(ROW_ACTIONS).fixture).isOpen()).toBe(false);
      });

      it('opens under the three dots that were clicked, and keeps that row highlighted', () => {
        const { fixture, element } = render(ROW_ACTIONS);
        const button = rowDots(element, 1);
        button.click();
        fixture.detectChanges();

        const popover = popoverOf(fixture);
        expect(popover.isOpen()).toBe(true);
        // Вікно прив'язується до самої кнопки (за її id).
        expect((element.querySelector('ion-popover') as unknown as { trigger: string }).trigger).toBe(button.id);
        expect(activeRows(element)).toEqual([false, true, false, false]);
      });

      it('moves to another row when other dots are clicked', () => {
        const { fixture, element } = render(ROW_ACTIONS);
        rowDots(element, 0).click();
        fixture.detectChanges();
        rowDots(element, 2).click();
        fixture.detectChanges();
        expect(activeRows(element)).toEqual([false, false, true, false]);
      });

      it.each([
        ['permissions', 'changePermissions'],
        ['edit', 'editColleague'],
        ['resetPassword', 'resetPassword'],
        ['disable', 'disableColleague'],
        ['delete', 'deleteColleague'],
      ])('calls the function of the action "%s" (%s) with the id of the colleague of that row', (action, method) => {
        const { fixture, element } = render(ROW_ACTIONS);
        const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        const spies = ['changePermissions', 'editColleague', 'resetPassword', 'disableColleague', 'deleteColleague'].map(
          (name) => [name, vi.spyOn(page, name)] as const,
        );
        rowDots(element, 2).click();
        fixture.detectChanges();

        page['onActionChosen'](action);

        for (const [name, spy] of spies) {
          if (name === method) {
            expect(spy).toHaveBeenCalledTimes(1);
            expect(spy).toHaveBeenCalledWith('sample-3');
          } else {
            expect(spy).not.toHaveBeenCalled();
          }
        }
        expect(popoverOf(fixture).isOpen()).toBe(false);
      });

      it('calls nothing when no menu is open (no colleague to act on)', () => {
        const { fixture } = render(ROW_ACTIONS);
        const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        const spy = vi.spyOn(page, 'editColleague');
        page['onActionChosen']('edit');
        expect(spy).not.toHaveBeenCalled();
      });

      it('closes when an action is chosen, and the row is not highlighted any more', async () => {
        const { fixture, element } = render(ROW_ACTIONS);
        rowDots(element, 0).click();
        fixture.detectChanges();
        const popover = popoverOf(fixture);
        expect(popover.isOpen()).toBe(true);

        const page = fixture.componentInstance as unknown as { onActionChosen(a: string): void };
        page.onActionChosen('edit');
        fixture.detectChanges();
        expect(popover.isOpen()).toBe(false);

        // Вікно сповіщає про закриття після анімації; у тесті подаємо це саме.
        (element.querySelector('ion-popover') as HTMLElement).dispatchEvent(new CustomEvent('didDismiss'));
        fixture.detectChanges();
        await vi.waitFor(() => expect(activeRows(element)).toEqual([false, false, false, false]));
      });
    });
  });

  describe('the side panel', () => {
    const panelOf = (fixture: ComponentFixture<ColleaguesPage>) =>
      fixture.debugElement.query(By.directive(SidePanelComponent)).componentInstance as SidePanelComponent;
    const state = (fixture: ComponentFixture<ColleaguesPage>) =>
      (fixture.componentInstance as unknown as { panel(): unknown }).panel();
    const FULL_RIGHTS = [...FULL, ...ROW_ACTIONS];

    it('is closed at first, with nothing opened', () => {
      const { fixture } = render(FULL_RIGHTS);
      expect(panelOf(fixture).isOpen()).toBe(false);
      expect(state(fixture)).toBeNull();
    });

    it('is called by the button "Roles"', () => {
      const { fixture, element } = render(FULL_RIGHTS);
      (headerButtons(element)[0] as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(panelOf(fixture).isOpen()).toBe(true);
      expect(state(fixture)).toEqual({ kind: 'roles', userId: null });
    });

    it('is called by the button "Add"', () => {
      const { fixture, element } = render(FULL_RIGHTS);
      const add = headerButtons(element).find((b) => b.textContent?.trim() === uk.colleagues.add) as HTMLButtonElement;
      add.click();
      fixture.detectChanges();
      expect(panelOf(fixture).isOpen()).toBe(true);
      expect(state(fixture)).toEqual({ kind: 'add', userId: null });
    });

    it.each([
      ['permissions', 'changePermissions'],
      ['edit', 'editColleague'],
      ['resetPassword', 'resetPassword'],
      ['disable', 'disableColleague'],
      ['delete', 'deleteColleague'],
    ])('is called by every action of the menu: "%s" opens it for the colleague of that row', (action, method) => {
      const { fixture, element } = render(FULL_RIGHTS);
      (element.querySelectorAll('tbody .data-table__menu')[3] as HTMLButtonElement).click();
      fixture.detectChanges();

      const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
      const stub = vi.spyOn(page, method);
      page['onActionChosen'](action);
      fixture.detectChanges();

      expect(stub).toHaveBeenCalledWith('sample-4');
      expect(panelOf(fixture).isOpen()).toBe(true);
      expect(state(fixture)).toEqual({ kind: action, userId: 'sample-4' });
    });

    it('opens by each of the five stubs themselves, with the id that was given to them', () => {
      const { fixture } = render(FULL_RIGHTS);
      const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
      const cases: [string, string][] = [
        ['changePermissions', 'permissions'],
        ['editColleague', 'edit'],
        ['resetPassword', 'resetPassword'],
        ['disableColleague', 'disable'],
        ['deleteColleague', 'delete'],
      ];
      for (const [method, kind] of cases) {
        page[method]('user-1');
        expect(state(fixture)).toEqual({ kind, userId: 'user-1' });
        panelOf(fixture).close();
      }
    });

    it('is closed by its cross: the opened state is cleared', () => {
      const { fixture, element } = render(FULL_RIGHTS);
      (headerButtons(element)[0] as HTMLButtonElement).click();
      fixture.detectChanges();

      (element.querySelector('.side-panel__close') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(panelOf(fixture).isOpen()).toBe(false);
      expect(state(fixture)).toBeNull();
    });
  });

  describe('the rights for the columns that are not there yet', () => {
    it('knows whether the user may see the emergency contact, and does not use it yet', () => {
      const withRight = render(['Permissions.ViewEmergencyContact']).fixture.componentInstance as unknown as { canViewEmergencyContact(): boolean };
      expect(withRight.canViewEmergencyContact()).toBe(true);
    });

    it('does not show the emergency contact column even with the right (there is no such column yet)', () => {
      const { element } = render(['Permissions.ViewEmergencyContact']);
      expect(Array.from(element.querySelectorAll('th'))).toHaveLength(5);
    });
  });
});
