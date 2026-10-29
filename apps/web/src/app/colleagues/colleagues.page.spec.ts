import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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

    it('has all six items, in this order, for a user with all the rights', () => {
      expect(actionIds(ROW_ACTIONS)).toEqual(['permissions', 'edit', 'resetPassword', 'disable', 'enable', 'delete']);
    });

    it.each([
      [['Permissions.AssignRoleToUser'], ['permissions']],
      [['Permissions.ManageRole'], ['permissions']],
      [['Permissions.AssignRoleToUser', 'Permissions.ManageRole'], ['permissions']],
      [['Permissions.UpdateUser'], ['edit', 'enable']],
      [['Permissions.CanResetPassword'], ['resetPassword']],
      [['Permissions.DisableUser'], ['disable']],
      [['Permissions.DeleteUser'], ['delete']],
      [['Permissions.UpdateUser', 'Permissions.DeleteUser'], ['edit', 'enable', 'delete']],
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
        'colleagues.actions.enable',
        'colleagues.actions.delete',
      ]);
      expect(page.actions().map((a) => a.icon)).toEqual([
        'shield-checkmark-outline',
        'create-outline',
        'key-outline',
        'ban-outline',
        'checkmark-circle-outline',
        'trash-outline',
      ]);
      expect(Object.keys(uk.colleagues.actions)).toEqual(['permissions', 'edit', 'resetPassword', 'disable', 'enable', 'delete']);
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
        ['enable', 'enableColleague'],
        ['delete', 'deleteColleague'],
      ])('calls the function of the action "%s" (%s) with the id of the colleague of that row', (action, method) => {
        const { fixture, element } = render(ROW_ACTIONS);
        const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        const spies = ['changePermissions', 'editColleague', 'resetPassword', 'disableColleague', 'enableColleague', 'deleteColleague'].map(
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
      ['enable', 'enableColleague'],
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

    it('opens by each of the six stubs themselves, with the id that was given to them', () => {
      const { fixture } = render(FULL_RIGHTS);
      const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
      const cases: [string, string][] = [
        ['changePermissions', 'permissions'],
        ['editColleague', 'edit'],
        ['resetPassword', 'resetPassword'],
        ['disableColleague', 'disable'],
        ['enableColleague', 'enable'],
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

  describe('the roles in the side panel (GET /api/v1/roles)', () => {
    const FULL_RIGHTS = [...FULL, ...ROW_ACTIONS];
    let http: HttpTestingController;
    let rolesUrl: string;

    beforeEach(() => {
      http = TestBed.inject(HttpTestingController);
      rolesUrl = `${TestBed.inject(ConnectionService).serverUrl()}/api/v1/roles`;
    });

    const rolesRequest = () => http.expectOne((request) => request.url === rolesUrl);
    const rolesButton = (element: HTMLElement) => headerButtons(element)[0] as HTMLButtonElement;
    const pills = (element: HTMLElement) => Array.from(element.querySelectorAll('app-roles-panel app-role-pill .pill')).map((p) => p.textContent?.trim());
    const body = (...names: string[]) => ({
      items: names.map((name, index) => ({ id: `r${index}`, name, feminitiveName: null, pluralName: null })),
      hasNextPage: false,
    });

    it('asks for the roles when the button "Roles" is pressed, and not before', () => {
      const { element } = render(FULL_RIGHTS);
      http.expectNone((request) => request.url === rolesUrl);

      rolesButton(element).click();
      expect(rolesRequest().request.method).toBe('GET');
    });

    it('shows the roles as colored pills in the panel when the answer comes', async () => {
      const { fixture, element } = render(FULL_RIGHTS);
      rolesButton(element).click();
      fixture.detectChanges();
      expect(element.querySelector('app-roles-panel .roles__message')?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.loading);

      rolesRequest().flush(body('Куратор', 'Член'));
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(pills(element)).toEqual(['Куратор', 'Член']);
      });
      const first = element.querySelector('app-roles-panel .pill') as HTMLElement;
      expect(first.style.getPropertyValue('--pill-base')).toMatch(/^#[0-9a-f]{6}$/);
    });

    it('asks again at every opening (the list can change), and keeps the old list while the new one loads', async () => {
      const { fixture, element } = render(FULL_RIGHTS);
      rolesButton(element).click();
      rolesRequest().flush(body('Куратор'));
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(pills(element)).toEqual(['Куратор']);
      });
      (element.querySelector('.side-panel__close') as HTMLButtonElement).click();
      fixture.detectChanges();

      rolesButton(element).click();
      fixture.detectChanges();
      const second = rolesRequest();
      expect(pills(element)).toEqual(['Куратор']);

      second.flush(body('Куратор', 'Голова ГО'));
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(pills(element)).toEqual(['Куратор', 'Голова ГО']);
      });
    });

    it('does not repeat a request that is still running', () => {
      const { element } = render(FULL_RIGHTS);
      rolesButton(element).click();
      rolesButton(element).click();
      expect(http.match((request) => request.url === rolesUrl)).toHaveLength(1);
    });

    it.each([
      [403, 'forbidden'],
      [401, 'unauthorized'],
      [500, 'error'],
    ] as const)('shows the error for HTTP %i from the language file', async (status, failure) => {
      const { fixture, element } = render(FULL_RIGHTS);
      rolesButton(element).click();
      rolesRequest().flush({}, { status, statusText: 'x' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('app-roles-panel .roles__message--error')?.textContent?.trim()).toBe(uk.colleagues.rolesPanel.error[failure]);
      });
    });

    it('does not ask for the roles for the actions of the menu', () => {
      const { fixture } = render(FULL_RIGHTS);
      const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
      for (const method of ['changePermissions', 'editColleague', 'resetPassword', 'disableColleague', 'enableColleague', 'deleteColleague']) {
        page[method]('user-1');
      }
      http.expectNone((request) => request.url === rolesUrl);
    });

    it('shows the list of the roles only for "Roles" (the form of "Add" has its own content, the actions have none)', () => {
      const { fixture, element } = render(FULL_RIGHTS);
      const add = headerButtons(element).find((b) => b.textContent?.trim() === uk.colleagues.add) as HTMLButtonElement;
      add.click();
      rolesRequest().flush(body());
      fixture.detectChanges();
      expect(element.querySelector('app-roles-panel')).toBeNull();
      expect(element.querySelector('app-colleague-form')).not.toBeNull();
    });

    describe('the form of a new colleague (the button "Add")', () => {
      const addButton = (element: HTMLElement) => headerButtons(element).find((b) => b.textContent?.trim() === uk.colleagues.add) as HTMLButtonElement;
      const choices = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLButtonElement>('app-colleague-form .colleague-form__choice'));

      async function openAdd() {
        const ctx = render(FULL_RIGHTS);
        addButton(ctx.element).click();
        rolesRequest().flush(body('Куратор', 'Член'));
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(choices(ctx.element)).toHaveLength(2);
        });
        return ctx;
      }

      it('opens the panel with the form "Add a colleague" and asks for the roles', () => {
        const { fixture, element } = render(FULL_RIGHTS);
        addButton(element).click();
        fixture.detectChanges();
        expect(rolesRequest().request.method).toBe('GET');
        expect(element.querySelector('app-colleague-form h2')?.textContent?.trim()).toBe(uk.colleagues.colleagueForm.title);
        expect(element.querySelector('app-roles-panel')).toBeNull();
        expect(element.querySelector('.side-panel__header-start')?.children).toHaveLength(0);
      });

      it('offers the roles as pills to choose, the same roles the list of "Roles" has', async () => {
        const { element } = await openAdd();
        expect(choices(element).map((c) => c.textContent?.trim())).toEqual(['Куратор', 'Член']);
      });

      it('gives the data to createColleague with the button "Add", and sends nothing', async () => {
        const { fixture, element } = await openAdd();
        const spy = vi.spyOn(fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>, 'createColleague');
        const email = element.querySelector('#colleague-form-email') as HTMLInputElement;
        email.value = 'a@b.org';
        email.dispatchEvent(new Event('input'));
        choices(element)[1].click();
        (element.querySelector('app-colleague-form form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));

        expect(spy).toHaveBeenCalledWith({ email: 'a@b.org', roleIds: ['r1'] });
        http.expectNone((request) => request.method !== 'GET');
      });
    });

    describe('the button "Add role" in the header of the panel', () => {
      const addRoleButton = (element: HTMLElement) =>
        element.querySelector('.side-panel__header-start .colleagues__btn') as HTMLButtonElement | null;

      it('stands at the left of the header row of the panel, on the same row with the cross, with the text from the language file', () => {
        const { fixture, element } = render(FULL_RIGHTS);
        rolesButton(element).click();
        fixture.detectChanges();

        const button = addRoleButton(element)!;
        expect(button).not.toBeNull();
        expect(button.textContent?.trim()).toBe(uk.colleagues.rolesPanel.add);
        expect(button.getAttribute('tabindex')).toBe('0');
        const header = element.querySelector('.side-panel__header')!;
        expect(header.contains(button)).toBe(true);
        expect(header.contains(element.querySelector('.side-panel__close'))).toBe(true);
        expect(button.compareDocumentPosition(element.querySelector('.side-panel__close')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      });

      it('is only there for the roles, not for "Add" and not for the actions of the menu', () => {
        const { fixture, element } = render(FULL_RIGHTS);
        const add = headerButtons(element).find((b) => b.textContent?.trim() === uk.colleagues.add) as HTMLButtonElement;
        add.click();
        fixture.detectChanges();
        expect(addRoleButton(element)).toBeNull();
      });

      it('calls its function when it is pressed', () => {
        const { fixture, element } = render(FULL_RIGHTS);
        const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        const spy = vi.spyOn(page, 'addRole');
        rolesButton(element).click();
        fixture.detectChanges();

        addRoleButton(element)!.click();
        expect(spy).toHaveBeenCalledTimes(1);
      });
    });

    describe('the right Permissions.ManageRole for the buttons "Add role", "Edit" and "Delete"', () => {
      const manageButtons = (element: HTMLElement) => ({
        add: element.querySelector('.side-panel__header-start .colleagues__btn'),
        actions: element.querySelectorAll('app-roles-panel .roles__action').length,
      });
      async function openRoles(rights: string[]) {
        const ctx = render(rights);
        rolesButton(ctx.element).click();
        rolesRequest().flush(body('Куратор', 'Член'));
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(pills(ctx.element)).toEqual(['Куратор', 'Член']);
        });
        return ctx;
      }

      it('shows all three kinds of buttons with it', async () => {
        const { element } = await openRoles(['Permissions.ViewUser', 'Permissions.ManageRole']);
        const buttons = manageButtons(element);
        expect(buttons.add).not.toBeNull();
        expect(buttons.actions).toBe(4);
      });

      it.each([[[]], [['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.UpdateUser', 'Permissions.AssignRoleToUser']]])(
        'shows none of them without it (rights: %j), but the panel and the roles are there',
        async (rights) => {
          const { element } = await openRoles(rights);
          const buttons = manageButtons(element);
          expect(buttons.add).toBeNull();
          expect(buttons.actions).toBe(0);
          expect(element.querySelector('.side-panel__close')).not.toBeNull();
          expect(element.querySelector('app-roles-panel .roles__title')).not.toBeNull();
        },
      );

      it('appear and disappear together with the right', async () => {
        const { fixture, element } = await openRoles([]);
        const accounts = TestBed.inject(AccountsService);
        expect(manageButtons(element).actions).toBe(0);

        accounts.update(accounts.activeId()!, { permissions: ['Permissions.ManageRole'] });
        fixture.detectChanges();
        expect(manageButtons(element).add).not.toBeNull();
        expect(manageButtons(element).actions).toBe(4);

        accounts.update(accounts.activeId()!, { permissions: [] });
        fixture.detectChanges();
        expect(manageButtons(element).add).toBeNull();
        expect(manageButtons(element).actions).toBe(0);
      });
    });

    describe('the form of a new role (after "Add role")', () => {
      const RIGHTS = ['Permissions.ViewUser', 'Permissions.ManageRole'];
      const headerStart = (element: HTMLElement) =>
        element.querySelector('.side-panel__header-start .colleagues__btn') as HTMLButtonElement | null;

      async function openRoles(rights = RIGHTS) {
        const ctx = render(rights);
        rolesButton(ctx.element).click();
        rolesRequest().flush(body('Куратор'));
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(pills(ctx.element)).toEqual(['Куратор']);
        });
        return ctx;
      }

      it('shows the list of the roles, not the form, at first', async () => {
        const { element } = await openRoles();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(element.querySelector('app-roles-panel')).not.toBeNull();
      });

      it('replaces the list with the form of a new role when "Add role" is pressed', async () => {
        const { fixture, element } = await openRoles();
        headerStart(element)!.click();
        fixture.detectChanges();

        expect(element.querySelector('app-role-form')).not.toBeNull();
        expect(element.querySelector('app-roles-panel')).toBeNull();
        expect(element.querySelectorAll('app-role-form input[type=text]')).toHaveLength(3);
      });

      it('puts the button "Back" instead of the button "Add role" in the same place of the header, left of the cross', async () => {
        const { fixture, element } = await openRoles();
        headerStart(element)!.click();
        fixture.detectChanges();

        const back = headerStart(element)!;
        expect(back.textContent?.trim()).toBe(uk.common.back);
        expect(back.classList).toContain('colleagues__btn--back');
        expect(back.getAttribute('tabindex')).toBe('0');
        expect((back.querySelector('ion-icon') as unknown as { name: string }).name).toBe('arrow-back-outline');
        expect(element.textContent).not.toContain(uk.colleagues.rolesPanel.add);
        expect(element.querySelectorAll('.side-panel__header-start .colleagues__btn')).toHaveLength(1);
        expect(back.compareDocumentPosition(element.querySelector('.side-panel__close')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      });

      it('goes back to the list by "Back": the roles are still there, with no new request, and "Add role" is back', async () => {
        const { fixture, element } = await openRoles();
        headerStart(element)!.click();
        fixture.detectChanges();

        headerStart(element)!.click();
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(pills(element)).toEqual(['Куратор']);
        expect(headerStart(element)!.textContent?.trim()).toBe(uk.colleagues.rolesPanel.add);
        http.expectNone((request) => request.url === rolesUrl);
      });

      it('opens "Roles" on the list again, not on the form where it was left', async () => {
        const { fixture, element } = await openRoles();
        headerStart(element)!.click();
        fixture.detectChanges();
        (element.querySelector('.side-panel__close') as HTMLButtonElement).click();
        fixture.detectChanges();

        rolesButton(element).click();
        fixture.detectChanges();
        rolesRequest().flush(body('Куратор'));
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(element.querySelector('app-roles-panel')).not.toBeNull();
      });

      it('gives the data of the form to createRole with the button "Add", and sends nothing', async () => {
        const { fixture, element } = await openRoles();
        const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        const spy = vi.spyOn(page, 'createRole');
        headerStart(element)!.click();
        fixture.detectChanges();

        const type = (id: string, value: string) => {
          const field = element.querySelector(`#${id}`) as HTMLInputElement;
          field.value = value;
          field.dispatchEvent(new Event('input'));
        };
        type('role-form-name', 'Куратор');
        type('role-form-plural', 'Куратори');
        (element.querySelector('app-role-form form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));

        expect(spy).toHaveBeenCalledWith({ name: 'Куратор', feminitiveName: null, pluralName: 'Куратори' });
        http.expectNone((request) => request.method !== 'GET');
      });

      it('shows the list again when the user loses the right while the form is open', async () => {
        const { fixture, element } = await openRoles();
        headerStart(element)!.click();
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).not.toBeNull();

        const accounts = TestBed.inject(AccountsService);
        accounts.update(accounts.activeId()!, { permissions: ['Permissions.ViewUser'] });
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(element.querySelector('app-roles-panel')).not.toBeNull();
        expect(headerStart(element)).toBeNull();
      });

      it('never shows the form to a user without the right, even if it is asked for', async () => {
        const { fixture, element } = await openRoles(['Permissions.ViewUser']);
        (fixture.componentInstance as unknown as { addRole(): void }).addRole();
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(element.querySelector('app-roles-panel')).not.toBeNull();
      });
    });

    describe('editing a role (the pencil: GET /api/v1/roles/{id} and the filled form)', () => {
      const headerStart = (element: HTMLElement) =>
        element.querySelector('.side-panel__header-start .colleagues__btn') as HTMLButtonElement | null;
      const roleUrl = (id: string) => `${rolesUrl}/${id}`;
      const detail = (id: string, name: string, extra: Record<string, unknown> = {}) => ({
        id,
        name,
        feminitiveName: null,
        pluralName: null,
        claims: [],
        ...extra,
      });

      async function withRoles() {
        const ctx = render(FULL_RIGHTS);
        rolesButton(ctx.element).click();
        rolesRequest().flush(body('Куратор', 'Член'));
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(ctx.element.querySelectorAll('app-roles-panel .roles__item')).toHaveLength(2);
        });
        return ctx;
      }
      const pencil = (element: HTMLElement, index: number) =>
        element.querySelectorAll('app-roles-panel .roles__action--edit')[index] as HTMLButtonElement;
      const field = (element: HTMLElement, id: string) => element.querySelector(`#${id}`) as HTMLInputElement;

      it('asks for that role by its id when the pencil is pressed, and shows the message while it waits', async () => {
        const { fixture, element } = await withRoles();
        pencil(element, 1).click();
        fixture.detectChanges();

        expect(http.expectOne(roleUrl('r1')).request.method).toBe('GET');
        expect(element.querySelector('app-roles-panel')).toBeNull();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(element.querySelector('.colleagues__panel-message')?.textContent?.trim()).toBe(uk.colleagues.roleForm.loading);
      });

      it('shows the form filled with the names of the role when the answer comes', async () => {
        const { fixture, element } = await withRoles();
        pencil(element, 0).click();
        http.expectOne(roleUrl('r0')).flush(detail('r0', 'Куратор', { feminitiveName: 'Кураторка', pluralName: 'Куратори' }));
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(element.querySelector('app-role-form')).not.toBeNull();
        });

        expect(field(element, 'role-form-name').value).toBe('Куратор');
        expect(field(element, 'role-form-feminitive').value).toBe('Кураторка');
        expect(field(element, 'role-form-plural').value).toBe('Куратори');
        expect(element.querySelector('app-role-form h2')?.textContent?.trim()).toBe(uk.colleagues.roleForm.editTitle);
        expect(element.querySelector('.colleagues__panel-message')).toBeNull();
      });

      it('has "Back" in the header like the form of a new role, and it returns to the list without a new request', async () => {
        const { fixture, element } = await withRoles();
        pencil(element, 0).click();
        http.expectOne(roleUrl('r0')).flush(detail('r0', 'Куратор'));
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(element.querySelector('app-role-form')).not.toBeNull();
        });
        expect(headerStart(element)!.textContent?.trim()).toBe(uk.common.back);

        headerStart(element)!.click();
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(pills(element)).toEqual(['Куратор', 'Член']);
        expect(headerStart(element)!.textContent?.trim()).toBe(uk.colleagues.rolesPanel.add);
        http.expectNone((request) => request.url.startsWith(rolesUrl));
      });

      it('says why it failed instead of the form, and "Back" is still there', async () => {
        const { fixture, element } = await withRoles();
        pencil(element, 0).click();
        http.expectOne(roleUrl('r0')).flush({}, { status: 403, statusText: 'Forbidden' });
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(element.querySelector('.colleagues__panel-message--error')).not.toBeNull();
        });

        const message = element.querySelector('.colleagues__panel-message--error');
        expect(message?.textContent?.trim()).toBe(uk.colleagues.roleForm.loadError.forbidden);
        expect(message?.getAttribute('role')).toBe('alert');
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(headerStart(element)!.textContent?.trim()).toBe(uk.common.back);
      });

      it('ignores the answer that comes after "Back" or for another role', async () => {
        const { fixture, element } = await withRoles();
        pencil(element, 0).click();
        const first = http.expectOne(roleUrl('r0'));
        fixture.detectChanges();
        headerStart(element)!.click();
        fixture.detectChanges();
        pencil(element, 1).click();
        const second = http.expectOne(roleUrl('r1'));

        first.flush(detail('r0', 'Куратор'));
        await Promise.resolve();
        await Promise.resolve();
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();

        second.flush(detail('r1', 'Член'));
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(field(element, 'role-form-name')?.value).toBe('Член');
        });
      });

      it('gives the saved data to updateRole with the role, and sends nothing', async () => {
        const { fixture, element } = await withRoles();
        const spy = vi.spyOn(fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>, 'updateRole');
        pencil(element, 0).click();
        http.expectOne(roleUrl('r0')).flush(detail('r0', 'Куратор', { claims: ['Permissions.ViewUser'] }));
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(element.querySelector('app-role-form')).not.toBeNull();
        });

        field(element, 'role-form-plural').value = 'Куратори';
        field(element, 'role-form-plural').dispatchEvent(new Event('input'));
        (element.querySelector('app-role-form form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));

        expect(spy).toHaveBeenCalledTimes(1);
        expect(spy.mock.calls[0][0]).toMatchObject({ id: 'r0', claims: ['Permissions.ViewUser'] });
        expect(spy.mock.calls[0][1]).toEqual({ name: 'Куратор', feminitiveName: null, pluralName: 'Куратори' });
        http.expectNone((request) => request.method !== 'GET');
      });

      it('opens "Roles" on the list again after editing was left open', async () => {
        const { fixture, element } = await withRoles();
        pencil(element, 0).click();
        http.expectOne(roleUrl('r0')).flush(detail('r0', 'Куратор'));
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(element.querySelector('app-role-form')).not.toBeNull();
        });
        (element.querySelector('.side-panel__close') as HTMLButtonElement).click();
        fixture.detectChanges();

        rolesButton(element).click();
        fixture.detectChanges();
        rolesRequest().flush(body('Куратор'));
        fixture.detectChanges();
        expect(element.querySelector('app-role-form')).toBeNull();
        expect(element.querySelector('app-roles-panel')).not.toBeNull();
      });
    });

    describe('the buttons "Edit" and "Delete" near a role', () => {
      async function withRoles() {
        const ctx = render(FULL_RIGHTS);
        const page = ctx.fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        rolesButton(ctx.element).click();
        rolesRequest().flush(body('Куратор', 'Член'));
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(ctx.element.querySelectorAll('app-roles-panel .roles__item')).toHaveLength(2);
        });
        return { ...ctx, page };
      }

      it('call editRole with the role of the row when the pencil is pressed', async () => {
        const { element, page } = await withRoles();
        const spy = vi.spyOn(page, 'editRole');
        const other = vi.spyOn(page, 'deleteRole');

        (element.querySelectorAll('app-roles-panel .roles__action--edit')[1] as HTMLButtonElement).click();
        expect(spy).toHaveBeenCalledTimes(1);
        expect(spy.mock.calls[0][0]).toMatchObject({ id: 'r1', name: 'Член' });
        expect(other).not.toHaveBeenCalled();
      });

      it('call deleteRole with the role of the row when the trash bin is pressed', async () => {
        const { element, page } = await withRoles();
        const spy = vi.spyOn(page, 'deleteRole');
        const other = vi.spyOn(page, 'editRole');

        (element.querySelectorAll('app-roles-panel .roles__action--delete')[0] as HTMLButtonElement).click();
        expect(spy).toHaveBeenCalledTimes(1);
        expect(spy.mock.calls[0][0]).toMatchObject({ id: 'r0', name: 'Куратор' });
        expect(other).not.toHaveBeenCalled();
      });
    });

    it('keeps the content while the panel hides (it does not vanish before the animation ends)', () => {
      const { fixture, element } = render(FULL_RIGHTS);
      rolesButton(element).click();
      rolesRequest().flush(body('Куратор'));
      fixture.detectChanges();

      (element.querySelector('.side-panel__close') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(element.querySelector('app-roles-panel')).not.toBeNull();
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
