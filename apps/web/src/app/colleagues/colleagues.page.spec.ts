import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { AccountsService } from '../core/accounts/accounts.service';
import { ConnectionService } from '../core/connection/connection.service';
import { CurrentUserService } from '../core/users/current-user.service';
import { UsersService } from '../core/users/users.service';
import { avatarColor } from '../shared/avatar/avatar-color';
import { PopoverComponent } from '../shared/popover/popover.component';
import { SidePanelComponent } from '../shared/side-panel/side-panel.component';
import { ColleagueActionsComponent } from './colleague-actions/colleague-actions.component';
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

  /** Колеги зі сторінки сервера: нейтральні імена, пошта за номером. */
  const user = (n: number, extra: Record<string, unknown> = {}) => ({
    id: `user-${n}`,
    email: `user${n}@example.org`,
    firstName: `Перший${n}`,
    lastName: `Колега${n}`,
    ...extra,
  });
  const usersUrl = () => `${TestBed.inject(ConnectionService).serverUrl()}/api/v1/users`;
  const usersRequest = (page = 1) =>
    TestBed.inject(HttpTestingController).expectOne((request) => request.url === usersUrl() && request.params.get('PageNumber') === String(page));

  /** Сторінка, на яку сервер уже відповів чотирма колегами (рядки таблиці є). */
  async function renderLoaded(permissions: string[] = FULL) {
    const ctx = render(permissions);
    usersRequest().flush({ items: [1, 2, 3, 4].map((n) => user(n)), hasNextPage: false });
    await vi.waitFor(() => {
      ctx.fixture.detectChanges();
      expect(ctx.element.querySelectorAll('tbody tr')).toHaveLength(4);
    });
    return ctx;
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

    it('draws a row for every colleague of the server: the name and the email are theirs, the rest are the placeholders', async () => {
      const { element } = await renderLoaded();
      const rows = Array.from(element.querySelectorAll('tbody tr'));
      rows.forEach((row, index) => {
        const n = index + 1;
        expect(row.querySelector('app-avatar')).not.toBeNull();
        expect(row.querySelector('.person__name')?.textContent?.trim()).toBe(`Перший${n} Колега${n}`);
        expect(row.querySelector('.person__pronouns')?.textContent?.trim()).toBe(uk.common.empty);
        const cells = row.querySelectorAll('td');
        expect(cells[2].textContent?.trim()).toBe(`user${n}@example.org`);
        expect(cells[3].textContent?.trim()).toBe(uk.colleagues.sample.phone);
      });
    });

    describe('uses what the colleague has, so that only the request has to change', () => {
      const full = {
        id: 'user-9',
        email: 'user9@example.org',
        firstName: 'Перший',
        lastName: 'Колега',
        phoneNumber: ' +380000000001 ',
        country: null,
        emergencyContact: null,
        pronouns: 'Займенники колеги',
        avatarUrl: 'https://cdn.example.org/a.png',
        roles: [
          { id: 'r1', name: 'Перша' },
          { id: 'r2', name: 'Друга' },
        ],
      };

      /** Сторінка, для якої сервіс віддає колегу з усіма полями (так, як віддаватиме оновлений запит). */
      async function renderWith(colleague: Record<string, unknown>) {
        vi.spyOn(UsersService.prototype, 'list').mockResolvedValue({ items: [colleague as never], hasNextPage: false });
        const ctx = render();
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(ctx.element.querySelectorAll('tbody tr')).toHaveLength(1);
        });
        return ctx;
      }

      it('shows the pronouns, the phone and the avatar of the colleague, the roles as pills', async () => {
        const { element } = await renderWith(full);
        const row = element.querySelector('tbody tr') as HTMLElement;
        expect(row.querySelector('.person__pronouns')?.textContent?.trim()).toBe('Займенники колеги');
        expect(row.querySelectorAll('td')[3].textContent?.trim()).toBe('+380000000001');
        const avatar = row.querySelector('app-avatar') as unknown as { avatarUrl: unknown };
        expect(avatar).not.toBeNull();
        expect(Array.from(row.querySelectorAll('.position app-role-pill .pill')).map((p) => p.textContent?.trim())).toEqual(['Перша', 'Друга']);
      });

      it('keeps the dash for the pronouns and the placeholder of the phone for what is empty, and shows no roles', async () => {
        const { element } = await renderWith({ ...full, roles: [], pronouns: '  ', phoneNumber: null });
        const row = element.querySelector('tbody tr') as HTMLElement;
        expect(row.querySelector('.person__pronouns')?.textContent?.trim()).toBe(uk.common.empty);
        expect(row.querySelectorAll('td')[3].textContent?.trim()).toBe(uk.colleagues.sample.phone);
        expect(row.querySelector('app-role-pill')).toBeNull();
      });

      it.each([
        [2, uk.settings.personal.pronounShe],
        [1, uk.settings.personal.pronounHe],
        [3, uk.settings.personal.pronounThey],
      ])('shows the words of the category %i, not the text, when there is a category', async (category, words) => {
        const { element } = await renderWith({ ...full, pronounCategory: category, pronouns: 'Текст, якого не видно' });
        expect(element.querySelector('.person__pronouns')?.textContent?.trim()).toBe(words);
      });

      it('shows the own text of the colleague when there is no category ("other" in the profile)', async () => {
        const { element } = await renderWith({ ...full, pronounCategory: null, pronouns: ' Власні займенники ' });
        expect(element.querySelector('.person__pronouns')?.textContent?.trim()).toBe('Власні займенники');
      });

      it.each([
        ['there is neither a category nor a text', { pronounCategory: null, pronouns: null }],
        ['the text is only spaces', { pronounCategory: null, pronouns: '   ' }],
        ['the category is not one of the known ones and there is no text', { pronounCategory: 9, pronouns: null }],
      ])('shows a dash when %s', async (_name, pronouns) => {
        const { element } = await renderWith({ ...full, ...pronouns });
        expect(element.querySelector('.person__pronouns')?.textContent?.trim()).toBe(uk.common.empty);
      });
    });

    it('colors the avatar of a colleague without a picture like the avatar of that person is colored everywhere: by the names and the organization of the user', async () => {
      const accounts = TestBed.inject(AccountsService);
      const { fixture, element } = render();
      accounts.update(accounts.activeId()!, { ngoName: 'Організація' });
      usersRequest().flush({ items: [user(1)], hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelectorAll('tbody tr')).toHaveLength(1);
      });

      const swatch = document.createElement('span');
      swatch.style.backgroundColor = avatarColor('Перший1', 'Колега1', 'Організація') ?? '';
      const avatar = element.querySelector('tbody .avatar') as HTMLElement;
      expect(swatch.style.backgroundColor).not.toBe('');
      expect(avatar.style.backgroundColor).toBe(swatch.style.backgroundColor);
    });

    it('puts the placeholders of the name and the email in place of those that are empty', async () => {
      const { fixture, element } = render();
      usersRequest().flush({ items: [user(1, { firstName: ' ', lastName: '', email: '' })], hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelectorAll('tbody tr')).toHaveLength(1);
      });
      expect(element.querySelector('.person__name')?.textContent?.trim()).toBe(uk.colleagues.sample.name);
      expect(element.querySelectorAll('tbody td')[2].textContent?.trim()).toBe(uk.colleagues.sample.email);
    });

    describe('the column "Position" (the roles of the colleague)', () => {
      const pillsOf = (row: Element) => Array.from(row.querySelectorAll('.position app-role-pill .pill')).map((p) => p.textContent?.trim());
      const colleague = (n: number, roles: { id: string; name: string }[]) => ({
        id: `user-${n}`,
        email: `user${n}@example.org`,
        firstName: `Перший${n}`,
        lastName: `Колега${n}`,
        phoneNumber: null,
        country: null,
        emergencyContact: null,
        pronouns: null,
        pronounCategory: null,
        avatarUrl: null,
        roles,
      });

      /** Сторінка, для якої сервіс віддає колег із такими ролями (так, як віддаватиме оновлений запит). */
      async function renderWithRoles(...roleLists: { id: string; name: string }[][]) {
        vi.spyOn(UsersService.prototype, 'list').mockResolvedValue({
          items: roleLists.map((roles, index) => colleague(index + 1, roles)),
          hasNextPage: false,
        });
        const ctx = render();
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(ctx.element.querySelectorAll('tbody tr')).toHaveLength(roleLists.length);
        });
        return ctx;
      }
      const ROLES = [
        { id: 'r1', name: 'Перша' },
        { id: 'r2', name: 'Друга' },
        { id: 'r3', name: 'Третя' },
      ];

      it('shows the roles of every colleague as the same pills as the list of "Roles" (app-role-pill, filled)', async () => {
        const { element } = await renderWithRoles(ROLES, [ROLES[1]]);
        const rows = Array.from(element.querySelectorAll('tbody tr'));
        expect(rows.map((row) => pillsOf(row))).toEqual([['Перша', 'Друга', 'Третя'], ['Друга']]);
        expect(element.querySelectorAll('tbody .pill--active')).toHaveLength(4);
      });

      it('shows no pills for a colleague without roles (there are no placeholder roles)', async () => {
        const { element } = await renderLoaded();
        expect(element.querySelectorAll('tbody app-role-pill')).toHaveLength(0);
        expect(element.querySelectorAll('tbody .position__roles').length).toBe(4);
      });

      it('puts the pills of one colleague side by side, wrapping to the next line when they do not fit', async () => {
        const { element } = await renderWithRoles(ROLES);
        const cell = element.querySelector('tbody .position__roles') as HTMLElement;
        const style = getComputedStyle(cell);
        expect(style.display).toBe('flex');
        expect(style.flexDirection).not.toBe('column');
        expect(style.flexWrap).toBe('wrap');
      });

      it('keeps the lines "project — role" with a colored dot under the pills', async () => {
        const { element } = await renderWithRoles(ROLES);
        const rows = Array.from(element.querySelectorAll('tbody tr'));
        expect(rows.map((row) => row.querySelectorAll('.position__line').length)).toEqual([2]);
        const dots = Array.from(element.querySelectorAll('.position__dot'));
        expect(dots.every((d) => d.classList.contains('position__dot--green') || d.classList.contains('position__dot--blue'))).toBe(true);
        expect(element.querySelector('.position__line')?.textContent?.trim()).toBe(uk.colleagues.sample.line);
        const first = rows[0].querySelector('.position') as HTMLElement;
        expect(first.firstElementChild?.classList).toContain('position__roles');
      });

      it('colors the pill by the name of the role, like in the list of "Roles"', async () => {
        const { element } = await renderWithRoles(ROLES);
        const pills = Array.from(element.querySelectorAll<HTMLElement>('tbody .position app-role-pill .pill'));
        const colors = pills.map((pill) => pill.style.getPropertyValue('--pill-base'));
        expect(colors.every((color) => color !== '')).toBe(true);
        expect(new Set(colors).size).toBe(3);
      });
    });

    describe('the three dots at the end of the rows', () => {
      const menus = (element: HTMLElement) => element.querySelectorAll('tbody .data-table__menu');

      it('are in every row, as the last cell, with any of the six rights', async () => {
        const { element } = await renderLoaded(['Permissions.UpdateUser']);
        const rows = element.querySelectorAll('tbody tr');
        expect(menus(element)).toHaveLength(rows.length);
        for (const row of Array.from(rows)) {
          expect(row.lastElementChild?.querySelector('.data-table__menu')).not.toBeNull();
        }
      });

      it.each(ROW_ACTIONS)('are shown when the user has only %s', async (right) => {
        expect(menus((await renderLoaded([right])).element).length).toBeGreaterThan(0);
      });

      it('are hidden when the user has none of the six rights (the cell stays, empty)', async () => {
        const { element } = await renderLoaded(['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.ViewCustomFields', 'Permissions.ViewEmergencyContact']);
        expect(menus(element)).toHaveLength(0);
        expect(element.querySelectorAll('tbody tr').length).toBeGreaterThan(0);
        expect(element.querySelector('tbody tr')?.lastElementChild?.classList).toContain('data-table__menu-cell');
      });

      it('appear and disappear together with the rights', async () => {
        const { fixture, element } = await renderLoaded([]);
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

      it('opens under the three dots that were clicked, and keeps that row highlighted', async () => {
        const { fixture, element } = await renderLoaded(ROW_ACTIONS);
        const button = rowDots(element, 1);
        button.click();
        fixture.detectChanges();

        const popover = popoverOf(fixture);
        expect(popover.isOpen()).toBe(true);
        // Вікно прив'язується до самої кнопки (за її id).
        expect((element.querySelector('ion-popover') as unknown as { trigger: string }).trigger).toBe(button.id);
        expect(activeRows(element)).toEqual([false, true, false, false]);
      });

      it('moves to another row when other dots are clicked', async () => {
        const { fixture, element } = await renderLoaded(ROW_ACTIONS);
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
      ])('calls the function of the action "%s" (%s) with the id of the colleague of that row', async (action, method) => {
        const { fixture, element } = await renderLoaded(ROW_ACTIONS);
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
            expect(spy).toHaveBeenCalledWith({ id: 'user-3', name: 'Перший3 Колега3' });
          } else {
            expect(spy).not.toHaveBeenCalled();
          }
        }
        expect(popoverOf(fixture).isOpen()).toBe(false);
      });

      describe('the row of the user himself', () => {
        const itemsOf = (fixture: ComponentFixture<ColleaguesPage>) =>
          (fixture.debugElement.query(By.directive(ColleagueActionsComponent)).componentInstance as ColleagueActionsComponent).items();

        const state = (fixture: ComponentFixture<ColleaguesPage>) => (fixture.componentInstance as unknown as { panel(): unknown }).panel();

        function asUser(id: string) {
          const accounts = TestBed.inject(AccountsService);
          accounts.update(accounts.activeId()!, { userId: id });
        }

        it('has only "Profile settings" (the item of the account menu), none of the other items', async () => {
          const { fixture, element } = await renderLoaded(ROW_ACTIONS);
          asUser('user-2');
          rowDots(element, 1).click();
          fixture.detectChanges();

          const items = itemsOf(fixture);
          expect(items).toEqual([{ id: 'profileSettings', labelKey: 'nav.account.settings', icon: 'cog-outline' }]);
          expect(uk.nav.account.settings).toBe('Налаштування профілю');
        });

        it('has all the items by the rights for any other row, and again after the own row was opened', async () => {
          const { fixture, element } = await renderLoaded(ROW_ACTIONS);
          asUser('user-2');
          rowDots(element, 0).click();
          fixture.detectChanges();
          expect(itemsOf(fixture).map((item) => item.id)).toEqual(['permissions', 'edit', 'resetPassword', 'disable', 'enable', 'delete']);

          rowDots(element, 1).click();
          fixture.detectChanges();
          expect(itemsOf(fixture)).toHaveLength(1);

          rowDots(element, 2).click();
          fixture.detectChanges();
          expect(itemsOf(fixture)).toHaveLength(6);
        });

        it('is known by the id of the loaded profile too (it wins over the snapshot of the account)', async () => {
          const { fixture, element } = await renderLoaded(ROW_ACTIONS);
          asUser('user-9');
          (TestBed.inject(CurrentUserService) as unknown as { userSignal: { set(user: unknown): void } }).userSignal.set({ id: 'user-3' });
          rowDots(element, 2).click();
          fixture.detectChanges();
          expect(itemsOf(fixture).map((item) => item.id)).toEqual(['profileSettings']);
        });

        it('is not recognized when the id of the user is not known yet (no profile, an empty id in the snapshot)', async () => {
          const { fixture, element } = await renderLoaded(ROW_ACTIONS);
          asUser('');
          rowDots(element, 1).click();
          fixture.detectChanges();
          expect(itemsOf(fixture)).toHaveLength(6);
        });

        it('goes to the settings of the profile when it is chosen: no panel and none of the actions with a colleague', async () => {
          const { fixture, element } = await renderLoaded(ROW_ACTIONS);
          asUser('user-2');
          const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
          const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
          const spies = ['changePermissions', 'editColleague', 'resetPassword', 'disableColleague', 'enableColleague', 'deleteColleague'].map((name) =>
            vi.spyOn(page, name),
          );
          rowDots(element, 1).click();
          fixture.detectChanges();

          page['onActionChosen']('profileSettings');
          fixture.detectChanges();

          expect(navigate).toHaveBeenCalledWith('/settings');
          expect(spies.every((spy) => !spy.mock.calls.length)).toBe(true);
          expect(popoverOf(fixture).isOpen()).toBe(false);
          expect(state(fixture)).toBeNull();
        });
      });

      it('calls nothing when no menu is open (no colleague to act on)', () => {
        const { fixture } = render(ROW_ACTIONS);
        const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
        const spy = vi.spyOn(page, 'editColleague');
        page['onActionChosen']('edit');
        expect(spy).not.toHaveBeenCalled();
      });

      it('closes when an action is chosen, and the row is not highlighted any more', async () => {
        const { fixture, element } = await renderLoaded(ROW_ACTIONS);
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
      expect(state(fixture)).toEqual({ kind: 'roles', userId: null, userName: null });
    });

    it('is called by the button "Add"', () => {
      const { fixture, element } = render(FULL_RIGHTS);
      const add = headerButtons(element).find((b) => b.textContent?.trim() === uk.colleagues.add) as HTMLButtonElement;
      add.click();
      fixture.detectChanges();
      expect(panelOf(fixture).isOpen()).toBe(true);
      expect(state(fixture)).toEqual({ kind: 'add', userId: null, userName: null });
    });

    it.each([
      ['permissions', 'changePermissions'],
      ['edit', 'editColleague'],
      ['resetPassword', 'resetPassword'],
      ['disable', 'disableColleague'],
      ['enable', 'enableColleague'],
      ['delete', 'deleteColleague'],
    ])('is called by every action of the menu: "%s" opens it for the colleague of that row', async (action, method) => {
      const { fixture, element } = await renderLoaded(FULL_RIGHTS);
      (element.querySelectorAll('tbody .data-table__menu')[3] as HTMLButtonElement).click();
      fixture.detectChanges();

      const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
      const stub = vi.spyOn(page, method);
      page['onActionChosen'](action);
      fixture.detectChanges();

      expect(stub).toHaveBeenCalledWith({ id: 'user-4', name: 'Перший4 Колега4' });
      expect(panelOf(fixture).isOpen()).toBe(true);
      expect(state(fixture)).toEqual({ kind: action, userId: 'user-4', userName: 'Перший4 Колега4' });
    });

    it('gives the name of the colleague from the request to the panel with the id, and null when the colleague has no name', async () => {
      vi.spyOn(UsersService.prototype, 'list').mockResolvedValue({
        items: [
          { id: 'user-1', email: '', firstName: ' ', lastName: '', phoneNumber: null, country: null, emergencyContact: null, pronouns: null, pronounCategory: null, avatarUrl: null, roles: [] },
          { id: 'user-2', email: '', firstName: 'Перший', lastName: 'Колега', phoneNumber: null, country: null, emergencyContact: null, pronouns: null, pronounCategory: null, avatarUrl: null, roles: [] },
        ],
        hasNextPage: false,
      });
      const { fixture, element } = render(FULL_RIGHTS);
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelectorAll('tbody tr')).toHaveLength(2);
      });
      const page = fixture.componentInstance as unknown as Record<string, (...args: unknown[]) => void>;
      for (const [index, expected] of [
        [0, { userId: 'user-1', userName: null }],
        [1, { userId: 'user-2', userName: 'Перший Колега' }],
      ] as const) {
        (element.querySelectorAll('tbody .data-table__menu')[index] as HTMLButtonElement).click();
        fixture.detectChanges();
        page['onActionChosen']('edit');
        fixture.detectChanges();
        expect(state(fixture)).toEqual({ kind: 'edit', ...expected });
        panelOf(fixture).close();
      }
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
        page[method]({ id: 'user-1', name: 'Перший' });
        expect(state(fixture)).toEqual({ kind, userId: 'user-1', userName: 'Перший' });
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

  describe('the colleagues from the server (GET /api/v1/users, 20 at a time, the rest while scrolling)', () => {
    const many = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, i) => user(from + i));
    const rowsCount = (element: HTMLElement) => element.querySelectorAll('tbody tr').length;
    const infinite = (element: HTMLElement) => element.querySelector('ion-infinite-scroll') as HTMLElement & { complete: () => void; disabled: boolean };
    const message = (element: HTMLElement) => element.querySelector('.colleagues__list-message');

    it('asks for the first page with 20 colleagues when the page opens', () => {
      render();
      const request = usersRequest(1);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.get('PageSize')).toBe('20');
    });

    it('says that the colleagues are loading while it waits, and shows no rows', () => {
      const { element } = render();
      expect(message(element)?.textContent?.trim()).toBe(uk.colleagues.list.loading);
      expect(rowsCount(element)).toBe(0);
    });

    it('says that there are none when the server has none', async () => {
      const { fixture, element } = render();
      usersRequest().flush({ items: [], hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(message(element)?.textContent?.trim()).toBe(uk.colleagues.list.empty);
      });
    });

    it('draws the colleagues, and the message is gone', async () => {
      const { element } = await renderLoaded();
      expect(rowsCount(element)).toBe(4);
      expect(message(element)).toBeNull();
    });

    it('asks for the next page when the end of the table is reached, and adds it under the rows that are there', async () => {
      const { fixture, element } = render();
      usersRequest(1).flush({ items: many(1, 20), hasNextPage: true });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(20);
      });
      http().expectNone((request) => request.params.get('PageNumber') === '2');

      const complete = vi.fn();
      infinite(element).complete = complete;
      infinite(element).dispatchEvent(new CustomEvent('ionInfinite'));
      const second = usersRequest(2);
      expect(second.request.params.get('PageSize')).toBe('20');
      second.flush({ items: many(21, 25), hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(25);
      });
      expect(complete).toHaveBeenCalled();
      expect(element.querySelectorAll('.person__name')[20].textContent?.trim()).toBe('Перший21 Колега21');
    });

    it('does not ask again when the server says there are no more pages, but still finishes the scroll', async () => {
      const { fixture, element } = render();
      usersRequest(1).flush({ items: many(1, 3), hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(3);
      });
      expect(infinite(element).disabled).toBe(true);

      const complete = vi.fn();
      infinite(element).complete = complete;
      infinite(element).dispatchEvent(new CustomEvent('ionInfinite'));
      await vi.waitFor(() => expect(complete).toHaveBeenCalled());
      http().expectNone((request) => request.url === usersUrl());
    });

    it('does not show the same colleague twice when the pages overlap', async () => {
      const { fixture, element } = render();
      usersRequest(1).flush({ items: many(1, 3), hasNextPage: true });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(3);
      });
      infinite(element).dispatchEvent(new CustomEvent('ionInfinite'));
      usersRequest(2).flush({ items: many(3, 5), hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(5);
      });
    });

    it('does not ask twice while a request is in flight', async () => {
      const { fixture, element } = render();
      usersRequest(1).flush({ items: many(1, 3), hasNextPage: true });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(3);
      });
      infinite(element).dispatchEvent(new CustomEvent('ionInfinite'));
      infinite(element).dispatchEvent(new CustomEvent('ionInfinite'));
      usersRequest(2).flush({ items: many(4, 5), hasNextPage: false });
    });

    it.each([
      [403, 'forbidden'],
      [500, 'error'],
    ] as const)('says why it failed (HTTP %i) with a button to try again, and keeps the rows that are there', async (status, failure) => {
      const { fixture, element } = render();
      usersRequest(1).flush({ items: many(1, 3), hasNextPage: true });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(3);
      });
      infinite(element).dispatchEvent(new CustomEvent('ionInfinite'));
      usersRequest(2).flush({}, { status, statusText: 'x' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(message(element)?.getAttribute('role')).toBe('alert');
      });
      expect(message(element)?.textContent).toContain(uk.colleagues.list.error[failure]);
      expect(rowsCount(element)).toBe(3);
      expect(infinite(element).disabled).toBe(true);

      (element.querySelector('.colleagues__retry') as HTMLButtonElement).click();
      usersRequest(2).flush({ items: many(4, 5), hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(5);
      });
      expect(message(element)).toBeNull();
    });

    it('says why the first page failed and can try again', async () => {
      const { fixture, element } = render();
      usersRequest(1).flush({}, { status: 403, statusText: 'Forbidden' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(message(element)?.textContent).toContain(uk.colleagues.list.error.forbidden);
      });
      expect(rowsCount(element)).toBe(0);

      (element.querySelector('.colleagues__retry') as HTMLButtonElement).click();
      usersRequest(1).flush({ items: many(1, 2), hasNextPage: false });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(rowsCount(element)).toBe(2);
      });
    });

    describe('sorting by a click on the header (OrderBy and SortOrder of the request)', () => {
      const heads = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLButtonElement>('thead th .data-table__sort'));
      const clickHead = (ctx: { fixture: ComponentFixture<ColleaguesPage>; element: HTMLElement }, index: number) => {
        heads(ctx.element)[index].click();
        ctx.fixture.detectChanges();
      };
      async function loaded(count = 3) {
        const ctx = render();
        usersRequest(1).flush({ items: many(1, count), hasNextPage: true });
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(rowsCount(ctx.element)).toBe(count);
        });
        return ctx;
      }
      const params = (request: ReturnType<typeof usersRequest>) => ({
        orderBy: request.request.params.get('OrderBy'),
        sortOrder: request.request.params.get('SortOrder'),
      });

      it('has a sort button in the four headers of the columns', () => {
        const { element } = render();
        expect(heads(element)).toHaveLength(4);
      });

      it('clears the table and asks for the first page again with OrderBy and SortOrder=asc by the first click', async () => {
        const ctx = await loaded();
        clickHead(ctx, 3);
        expect(rowsCount(ctx.element)).toBe(0);
        expect(message(ctx.element)?.textContent?.trim()).toBe(uk.colleagues.list.loading);
        const request = usersRequest(1);
        expect(request.request.params.get('PageSize')).toBe('20');
        expect(params(request)).toEqual({ orderBy: 'PhoneNumber', sortOrder: 'asc' });
        request.flush({ items: many(1, 2), hasNextPage: false });
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(rowsCount(ctx.element)).toBe(2);
        });
      });

      it('asks with SortOrder=desc by the second click and without any arguments of the sorting by the third', async () => {
        const ctx = await loaded();
        clickHead(ctx, 2);
        expect(params(usersRequest(1))).toEqual({ orderBy: 'Email', sortOrder: 'asc' });

        clickHead(ctx, 2);
        expect(params(usersRequest(1))).toEqual({ orderBy: 'Email', sortOrder: 'desc' });

        clickHead(ctx, 2);
        const third = usersRequest(1);
        expect(third.request.params.has('OrderBy')).toBe(false);
        expect(third.request.params.has('SortOrder')).toBe(false);
      });

      it('takes OrderBy from the column: LastName for the name, nothing for the position, Email, PhoneNumber', async () => {
        const ctx = render();
        usersRequest(1).flush({ items: [], hasNextPage: false });
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(message(ctx.element)?.textContent?.trim()).toBe(uk.colleagues.list.empty);
        });
        const orderBy: (string | null)[] = [];
        for (const index of [0, 1, 2, 3]) {
          clickHead(ctx, index);
          const request = usersRequest(1);
          orderBy.push(request.request.params.get('OrderBy'));
          request.flush({ items: [], hasNextPage: false });
          await vi.waitFor(() => {
            ctx.fixture.detectChanges();
            expect(message(ctx.element)?.textContent?.trim()).toBe(uk.colleagues.list.empty);
          });
        }
        expect(orderBy).toEqual(['LastName', null, 'Email', 'PhoneNumber']);
      });

      it('asks without OrderBy and SortOrder for the position (its argument is empty), but still clears the table', async () => {
        const ctx = await loaded();
        clickHead(ctx, 1);
        expect(rowsCount(ctx.element)).toBe(0);
        const request = usersRequest(1);
        expect(request.request.params.has('OrderBy')).toBe(false);
        expect(request.request.params.has('SortOrder')).toBe(false);
        expect(ctx.element.querySelectorAll('thead .data-table__head--sorted')).toHaveLength(1);
      });

      it('keeps the sorting for the next pages when the table is scrolled', async () => {
        const ctx = await loaded();
        clickHead(ctx, 2);
        usersRequest(1);
        clickHead(ctx, 2);
        usersRequest(1).flush({ items: many(1, 20), hasNextPage: true });
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(rowsCount(ctx.element)).toBe(20);
        });

        infinite(ctx.element).dispatchEvent(new CustomEvent('ionInfinite'));
        expect(params(usersRequest(2))).toEqual({ orderBy: 'Email', sortOrder: 'desc' });
      });

      it('starts from the first page after a new sorting, even if later pages were loaded', async () => {
        const ctx = await loaded(20);
        infinite(ctx.element).dispatchEvent(new CustomEvent('ionInfinite'));
        usersRequest(2).flush({ items: many(21, 25), hasNextPage: true });
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(rowsCount(ctx.element)).toBe(25);
        });

        clickHead(ctx, 0);
        expect(rowsCount(ctx.element)).toBe(0);
        usersRequest(1);
      });

      it('ignores the answer to the request of the former sorting that comes late', async () => {
        const ctx = await loaded();
        clickHead(ctx, 0);
        const former = usersRequest(1);
        clickHead(ctx, 0);
        const current = usersRequest(1);

        current.flush({ items: many(11, 12), hasNextPage: false });
        await vi.waitFor(() => {
          ctx.fixture.detectChanges();
          expect(rowsCount(ctx.element)).toBe(2);
        });
        former.flush({ items: many(1, 5), hasNextPage: false });
        await Promise.resolve();
        await Promise.resolve();
        ctx.fixture.detectChanges();
        expect(rowsCount(ctx.element)).toBe(2);
        expect(ctx.element.querySelectorAll('.person__name')[0].textContent?.trim()).toBe('Перший11 Колега11');
      });

      it('keeps one header selected while the sorting changes from one column to another', async () => {
        const ctx = await loaded();
        clickHead(ctx, 0);
        usersRequest(1);
        clickHead(ctx, 3);
        usersRequest(1);
        const selected = Array.from(ctx.element.querySelectorAll('thead .data-table__head--sorted'));
        expect(selected).toHaveLength(1);
        expect(selected[0]).toBe(ctx.element.querySelectorAll('thead th')[3]);
      });
    });

    const http = () => TestBed.inject(HttpTestingController);
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
        page[method]({ id: 'user-1', name: 'Перший' });
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
