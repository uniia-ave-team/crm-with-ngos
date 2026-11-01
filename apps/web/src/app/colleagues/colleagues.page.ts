import { Component, OnInit, TemplateRef, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

import { AccountsService } from '../core/accounts/accounts.service';
import { PERMISSIONS, hasAnyPermission } from '../core/auth/permissions';
import { LanguageService } from '../core/i18n/language.service';
import { RequestFailure } from '../core/http/request-failure';
import { Role, RoleDetails, RolesService } from '../core/roles/roles.service';
import { CurrentUserService } from '../core/users/current-user.service';
import { PRONOUN_CATEGORY } from '../core/users/pronouns';
import { UserRole } from '../core/users/user-profile';
import { UserSummary, UsersService, UsersSort } from '../core/users/users.service';
import {
  DataTableCellContext,
  DataTableColumn,
  DataTableRow,
  DataTableRowMenuEvent,
  DataTableSort,
} from '../shared/data-table/data-table.component';
import { PopoverComponent } from '../shared/popover/popover.component';
import { SidePanelComponent } from '../shared/side-panel/side-panel.component';
import { ColleagueAction, ColleagueActionItem } from './colleague-actions/colleague-actions.component';
import { NewColleague } from './colleague-form/colleague-form.component';
import { NewRole } from './role-form/role-form.component';
import { RolesView } from './roles-panel/roles-panel.component';

/** Стан форми редагування ролі: запит ролі (`GET /api/v1/roles/{id}`) і сама роль, коли вона є. */
export interface RoleEditView {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly role: RoleDetails | null;
  /** Причина невдачі (лише для `failed`). */
  readonly failure: RequestFailure | null;
}

/** Скільки колег підвантажується за один запит. */
export const COLLEAGUES_PAGE_SIZE = 20;

/** Стан таблиці колег: що з запитом, які колеги вже є й чи є ще сторінки. */
export interface ColleaguesView {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly users: readonly UserSummary[];
  /** Причина невдачі (лише для `failed`). */
  readonly failure: RequestFailure | null;
  /** Сервер каже, що є ще сторінки. */
  readonly hasMore: boolean;
}

/** Що відкрито в бічному вікні: кнопки шапки або дія з колегою. */
export type ColleaguesPanelKind = 'roles' | 'add' | ColleagueAction;

/** Колега, якого стосується дія: `id` й імʼя з запиту (`null` — імені нема). */
export interface ColleagueRef {
  readonly id: string;
  readonly name: string | null;
}

/** Стан бічного вікна: що відкрито й для якого колеги (`null` — не для колеги). */
export interface ColleaguesPanelState {
  readonly kind: ColleaguesPanelKind;
  readonly userId: string | null;
  /** Імʼя колеги з запиту (`null` — не для колеги чи імені нема). */
  readonly userName: string | null;
}

/** Пункт меню дій з колегою й права, з яких він показується (достатньо будь-якого одного). */
interface ColleagueActionRule extends ColleagueActionItem {
  readonly requiresAny: readonly string[];
}

const ACTION_RULES: readonly ColleagueActionRule[] = [
  {
    id: 'permissions',
    labelKey: 'colleagues.actions.permissions',
    icon: 'shield-checkmark-outline',
    requiresAny: [PERMISSIONS.assignRoleToUser, PERMISSIONS.manageRole],
  },
  { id: 'edit', labelKey: 'colleagues.actions.edit', icon: 'create-outline', requiresAny: [PERMISSIONS.updateUser] },
  {
    id: 'resetPassword',
    labelKey: 'colleagues.actions.resetPassword',
    icon: 'key-outline',
    requiresAny: [PERMISSIONS.canResetPassword],
  },
  { id: 'disable', labelKey: 'colleagues.actions.disable', icon: 'ban-outline', requiresAny: [PERMISSIONS.disableUser] },
  // Як і на сервері (`POST /users/{id}/activate`), вмикання вимагає `UpdateUser`, а не `DisableUser`.
  {
    id: 'enable',
    labelKey: 'colleagues.actions.enable',
    icon: 'checkmark-circle-outline',
    requiresAny: [PERMISSIONS.updateUser],
  },
  { id: 'delete', labelKey: 'colleagues.actions.delete', icon: 'trash-outline', requiresAny: [PERMISSIONS.deleteUser] },
];

type DotColor = 'green' | 'blue';

/** Зразкові рядки «проєкт — роль» (поки сервер проєктів не віддає): однакові в усіх рядках. */
const SAMPLE_LINES: readonly DotColor[] = ['green', 'blue'];

/**
 * Єдиний пункт меню в рядку самого користувача: той самий «Налаштування профілю»,
 * що в меню перемикання акаунтів (`nav.account.settings`); права для нього не
 * потрібні.
 */
const PROFILE_SETTINGS_ITEM: ColleagueActionItem = {
  id: 'profileSettings',
  labelKey: 'nav.account.settings',
  icon: 'cog-outline',
};

/** Рядок таблиці колег. */
interface ColleagueRow extends DataTableRow {
  readonly pronouns: string;
  /** Аватар й імена для нього (`null` / порожньо — аватар за замовчуванням). */
  readonly avatarUrl: string | null;
  readonly firstName: string;
  readonly lastName: string;
  /** Ролі колеги в колонці «Посада» плашками; нема ролей — нема й плашок. */
  readonly roles: readonly UserRole[];
  /** Рядки під плашками: кольорова крапка й текст («проєкт — роль»; поки зразкові). */
  readonly lines: readonly { readonly text: string; readonly color: DotColor }[];
  readonly name: string;
  readonly email: string;
  readonly phone: string;
}

/**
 * Вкладка «Колеги» (`/colleagues`): шапка з кнопками «Ролі», «Додати» і пошуку та
 * таблиця (`app-data-table`). Кнопки й крапки в кінці рядків поки нічого не
 * роблять. Колонки (`columns`) і рядки (`rows`) задаються кодом, тож їх можна
 * змінити, не чіпаючи розмітки; вигляд складних комірок — шаблони внизу
 * розмітки сторінки.
 *
 * Що показується, залежить від прав користувача (права активного акаунта зі
 * знімка в `localStorage`): кнопка «Додати» — з `Permissions.CreateUser`; три
 * крапки в кінці рядків відкривають меню дій, у якому кожен пункт має своє
 * право («Змінити ролі» — `AssignRoleToUser` або `ManageRole`, «Редагувати» —
 * `UpdateUser`, «Скинути пароль» — `CanResetPassword`, «Вимкнути акаунт» —
 * `DisableUser`, «Ввімкнути акаунт» — `UpdateUser`, «Видалити» — `DeleteUser`),
 * а без жодного з них крапок нема; «+» в кінці рядка заголовків — з
 * `Permissions.ViewCustomFields`. У вікні «Ролі» кнопки «Додати роль»,
 * «Редагувати» й «Видалити» — з `Permissions.ManageRole` (список ролей видно
 * всім). Саму вкладку в бічному меню показують лише з `Permissions.ViewUser`
 * (`PrimaryNavComponent`).
 *
 * Бічне вікно (`app-side-panel`) відкривають кнопки «Ролі» й «Додати» та кожна дія
 * з меню біля трьох крапок (функції-заглушки нижче); що в ньому показувати, ще не
 * зроблено — вікно порожнє, а `panel` каже, що саме його відкрило й для якого колеги.
 *
 * Колеги приходять із `GET /api/v1/users` по 20 (перша сторінка при відкритті,
 * далі підвантаження при прокрутці до кінця таблиці, `ion-infinite-scroll`); у
 * рядках поки лише імʼя й пошта з відповіді, решта — плейсхолдери з мовного
 * файлу (`colleagues.sample.*`).
 */
@Component({
  selector: 'app-colleagues',
  templateUrl: './colleagues.page.html',
  styleUrls: ['./colleagues.page.scss'],
  standalone: false,
})
export class ColleaguesPage implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService).language;
  private readonly roles = inject(RolesService);
  private readonly users = inject(UsersService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly permissions = this.currentUser.permissions;
  private readonly router = inject(Router);
  private readonly accounts = inject(AccountsService);

  /**
   * Назва організації для кольору аватарів: колеги з тієї самої організації, що й
   * користувач, тож беремо її з його знімка (як меню), і колір аватара колеги без
   * зображення такий самий, як у нього скрізь.
   */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');

  /** Може додавати колег (кнопка «Додати»). */
  protected readonly canAdd = computed(() => this.permissions().includes(PERMISSIONS.createUser));
  /** Може керувати ролями: кнопки «Додати роль», «Редагувати» й «Видалити» у вікні «Ролі». */
  protected readonly canManageRoles = computed(() => this.permissions().includes(PERMISSIONS.manageRole));
  /** Дії з колегою, доступні користувачеві (пункти меню біля трьох крапок). */
  protected readonly actions = computed<readonly ColleagueActionItem[]>(() =>
    ACTION_RULES.filter((rule) => hasAnyPermission(this.permissions(), rule.requiresAny)),
  );
  /** Може щось робити з колегою: показуємо три крапки в кінці рядків. */
  protected readonly canOpenRowMenu = computed(() => this.actions().length > 0);

  private readonly rowMenu = viewChild(PopoverComponent);
  private readonly sidePanel = viewChild(SidePanelComponent);
  /** Що відкрито в бічному вікні (`null` — вікно закрите). */
  protected readonly panel = signal<ColleaguesPanelState | null>(null);
  /**
   * Що було відкрито востаннє: зміст вікна лишається, поки воно ховається
   * (інакше він зникав би ще до кінця анімації закриття).
   */
  protected readonly panelContent = signal<ColleaguesPanelKind | null>(null);
  /** Ролі для бічного вікна «Ролі» (запит іде при кожному його відкритті). */
  protected readonly rolesView = signal<RolesView>({ status: 'loading', roles: [], failure: null });
  private rolesLoading = false;
  /** Що показано у вікні «Ролі»: список, форма нової ролі («Додати роль») чи форма ролі, що редагується. */
  private readonly rolesMode = signal<'list' | 'add' | 'edit'>('list');
  /** Форми лише для того, хто може керувати ролями; інакше завжди список. */
  protected readonly rolesScreen = computed<'list' | 'add' | 'edit'>(() => (this.canManageRoles() ? this.rolesMode() : 'list'));
  /** Роль, що редагується: запит і її дані для форми. */
  protected readonly roleEditView = signal<RoleEditView>({ status: 'loading', role: null, failure: null });
  /** Номер останнього запиту ролі: відповідь на давніший (інша роль чи вже «Назад») ігнорується. */
  private roleRequest = 0;
  /** Меню відкрите для рядка самого користувача: у ньому лише «Налаштування профілю». */
  protected readonly menuForSelf = signal(false);
  /** Пункти відкритого меню: для себе — лише налаштування профілю, для інших — дії за правами. */
  protected readonly menuItems = computed<readonly ColleagueActionItem[]>(() =>
    this.menuForSelf() ? [PROFILE_SETTINGS_ITEM] : this.actions(),
  );
  /** Рядок, для якого відкрите меню дій (лишається виділеним, поки меню відкрите); `null` — меню закрите. */
  protected readonly menuRowId = signal<string | null>(null);
  /**
   * Бачить додаткові поля: показуємо «+» в кінці рядка заголовків таблиці
   * (додавання колонки). Колонок із додатковими полями ще нема.
   */
  protected readonly canViewCustomFields = computed(() => this.permissions().includes(PERMISSIONS.viewCustomFields));
  /**
   * Право на екстрений контакт: перевірено при побудові таблиці, але колонки
   * «Екстрений контакт» ще нема, тож поки не використовується.
   */
  protected readonly canViewEmergencyContact = computed(() => this.permissions().includes(PERMISSIONS.viewEmergencyContact));

  private readonly personCell = viewChild<TemplateRef<DataTableCellContext>>('personCell');
  private readonly positionCell = viewChild<TemplateRef<DataTableCellContext>>('positionCell');

  /**
   * Колонки; `orderBy` — що йде в `OrderBy` запиту при сортуванні за колонкою. Сервер
   * приймає одне імʼя властивості (з `UserDto`), тож для «ПІБ» не вийде передати
   * одразу прізвище, імʼя й по батькові: беремо прізвище. «Посада» поки без
   * сортування (порожній), «Телефон» — `PhoneNumber` (списку сервер цього поля поки
   * не віддає, тож сортування за ним ще не діє).
   */
  protected readonly columns = computed<readonly DataTableColumn[]>(() => [
    { id: 'name', labelKey: 'colleagues.columns.name', width: '30%', template: this.personCell(), orderBy: 'LastName' },
    { id: 'position', labelKey: 'colleagues.columns.position', width: '30%', template: this.positionCell(), orderBy: '' },
    { id: 'email', labelKey: 'colleagues.columns.email', width: '22%', orderBy: 'Email' },
    { id: 'phone', labelKey: 'colleagues.columns.phone', width: '18%', orderBy: 'PhoneNumber' },
  ]);

  /** Колеги з сервера (по `COLLEAGUES_PAGE_SIZE` за запит, решта підвантажується при прокрутці). */
  protected readonly colleagues = signal<ColleaguesView>({ status: 'loading', users: [], failure: null, hasMore: false });
  private nextPage = 1;
  private colleaguesLoading = false;
  /** Сортування таблиці (клік по заголовку); `null` — без аргументів сортування. Діє й на наступні сторінки. */
  private sort: UsersSort | null = null;
  /** Номер «покоління» списку: зростає при кожному новому сортуванні, відповідь на давніший запит ігнорується. */
  private colleaguesGeneration = 0;

  /**
   * Рядки з полів колеги (`UserSummary`); порожнє поле (телефон, імʼя, пошта)
   * заміняє тимчасовий плейсхолдер із мовного файлу (`colleagues.sample.*`),
   * займенники — за `pronounsText`. Тексти перераховуються при зміні мови.
   */
  protected readonly rows = computed<readonly ColleagueRow[]>(() => {
    this.language();
    const t = (key: string) => this.translate.instant(key) as string;
    const lineText = t('colleagues.sample.line');
    return this.colleagues().users.map((user) => ({
      id: user.id,
      name: `${user.firstName} ${user.lastName}`.trim() || t('colleagues.sample.name'),
      email: user.email.trim() || t('colleagues.sample.email'),
      pronouns: this.pronounsText(user),
      phone: user.phoneNumber?.trim() || t('colleagues.sample.phone'),
      avatarUrl: user.avatarUrl,
      firstName: user.firstName,
      lastName: user.lastName,
      roles: user.roles,
      lines: SAMPLE_LINES.map((color) => ({ text: lineText, color })),
    }));
  });

  ngOnInit(): void {
    void this.loadColleagues();
  }

  /**
   * Підвантажує наступну сторінку колег (`GET /api/v1/users`, по `COLLEAGUES_PAGE_SIZE`)
   * і додає її до вже показаних. Невдача лишає, що вже є, і показує причину; той
   * самий виклик повторює запит. Запит, що ще триває, не дублюється.
   */
  protected async loadColleagues(): Promise<void> {
    if (this.colleaguesLoading) {
      return;
    }
    this.colleaguesLoading = true;
    const generation = this.colleaguesGeneration;
    const before = this.colleagues();
    this.colleagues.set({ ...before, status: 'loading', failure: null });
    try {
      const result = await this.users.list(this.nextPage, COLLEAGUES_PAGE_SIZE, this.sort);
      if (generation !== this.colleaguesGeneration) {
        return;
      }
      if (typeof result === 'string') {
        this.colleagues.set({ ...before, status: 'failed', failure: result });
        return;
      }
      this.nextPage++;
      const known = new Set(before.users.map((user) => user.id));
      this.colleagues.set({
        status: 'ready',
        users: [...before.users, ...result.items.filter((user) => !known.has(user.id))],
        failure: null,
        hasMore: result.hasNextPage,
      });
    } finally {
      if (generation === this.colleaguesGeneration) {
        this.colleaguesLoading = false;
      }
    }
  }

  /**
   * Клік по заголовку колонки (див. `app-data-table`): таблиця очищається й список
   * запитується з першої сторінки заново. Перший клік — `OrderBy` колонки й
   * `SortOrder=asc`, другий — `desc`, третій — без аргументів сортування. Колонка
   * з порожнім `orderBy` теж очищає таблицю, але сортування не передає. Вибране
   * сортування діє й на наступні сторінки при прокрутці.
   */
  protected onSort(sort: DataTableSort | null): void {
    this.sort = sort && sort.column.orderBy ? { orderBy: sort.column.orderBy, sortOrder: sort.direction } : null;
    this.colleaguesGeneration++;
    this.colleaguesLoading = false;
    this.nextPage = 1;
    this.colleagues.set({ status: 'loading', users: [], failure: null, hasMore: false });
    void this.loadColleagues();
  }

  /** Докрутили до кінця таблиці: підвантажуємо наступну сторінку й знімаємо індикатор. */
  protected async onInfiniteScroll(event: Event): Promise<void> {
    if (this.colleagues().hasMore) {
      await this.loadColleagues();
    }
    await (event.target as { complete?: () => Promise<void> | void } | null)?.complete?.();
  }

  /**
   * Займенники колеги в таблиці: за категорією — «Вона/її», «Він/його» чи «Вони/їх»;
   * без категорії («Інше» в профілі) — власний текст `pronouns`; нема й його — прочерк.
   */
  private pronounsText(user: UserSummary): string {
    const t = (key: string) => this.translate.instant(key) as string;
    switch (user.pronounCategory) {
      case PRONOUN_CATEGORY.feminine:
        return t('settings.personal.pronounShe');
      case PRONOUN_CATEGORY.masculine:
        return t('settings.personal.pronounHe');
      case PRONOUN_CATEGORY.neutral:
        return t('settings.personal.pronounThey');
      default:
        return user.pronouns?.trim() || t('common.empty');
    }
  }

  /** Три крапки в рядку: відкриваємо меню дій біля кнопки. */
  protected openRowMenu(event: DataTableRowMenuEvent): void {
    this.menuRowId.set(event.row.id);
    this.menuForSelf.set(this.isCurrentUser(event.row.id));
    this.rowMenu()?.open(event.anchor);
  }

  /** `id` колеги — це сам користувач (за профілем, а поки його нема, за знімком акаунта). */
  private isCurrentUser(id: string): boolean {
    const mine = this.currentUser.user()?.id || this.accounts.active()?.userId;
    return !!mine && mine === id;
  }

  /**
   * Вибрали дію з меню: закриваємо меню й викликаємо функцію цієї дії з колегою
   * (`id` та імʼя з запиту), чиє меню було відкрите.
   * «Налаштування профілю» (меню самого користувача) лише переходить на `/settings`.
   */
  protected onActionChosen(action: ColleagueAction): void {
    const id = this.menuRowId();
    void this.rowMenu()?.close();
    if (id === null) {
      return;
    }
    if (action === 'profileSettings') {
      void this.router.navigateByUrl('/settings');
      return;
    }
    const user = this.colleagues().users.find((candidate) => candidate.id === id);
    const colleague: ColleagueRef = { id, name: user ? `${user.firstName} ${user.lastName}`.trim() || null : null };
    switch (action) {
      case 'permissions':
        this.changePermissions(colleague);
        break;
      case 'edit':
        this.editColleague(colleague);
        break;
      case 'resetPassword':
        this.resetPassword(colleague);
        break;
      case 'disable':
        this.disableColleague(colleague);
        break;
      case 'enable':
        this.enableColleague(colleague);
        break;
      case 'delete':
        this.deleteColleague(colleague);
        break;
    }
  }

  /** Відкриває бічне вікно для кнопки шапки чи дії (`colleague` — колега, якого стосується дія). */
  protected openPanel(kind: ColleaguesPanelKind, colleague: ColleagueRef | null = null): void {
    this.panel.set({ kind, userId: colleague?.id ?? null, userName: colleague?.name ?? null });
    this.panelContent.set(kind);
    this.sidePanel()?.open();
    if (kind === 'add') {
      // Форма колеги вибирає ролі зі списку: він такий самий, як у вікні «Ролі».
      void this.loadRoles();
    }
    if (kind === 'roles') {
      // «Ролі» завжди відкривають на списку, а не на формі, де зупинились минулого разу.
      this.roleRequest++;
      this.rolesMode.set('list');
      void this.loadRoles();
    }
  }

  /** «Додати роль» у шапці вікна «Ролі»: замість списку з'являється форма нової ролі. */
  protected addRole(): void {
    this.rolesMode.set('add');
  }

  /** «Назад» у шапці вікна на формі ролі: знову список ролей. */
  protected backToRoles(): void {
    this.roleRequest++;
    this.rolesMode.set('list');
  }

  /** «Додати» у формі нового колеги. Заглушка: дані поки не надсилаємо. */
  protected createColleague(colleague: NewColleague): void {
    console.log(`[ColleaguesPage] Додати колегу: ${colleague.email}, ролей: ${colleague.roleIds.length} (відправку ще не зроблено)`);
  }

  /** «Додати» у формі нової ролі. Заглушка: дані поки не надсилаємо. */
  protected createRole(role: NewRole): void {
    console.log(`[ColleaguesPage] Додати роль: ${role.name} (відправку ще не зроблено)`);
  }

  /**
   * «Редагувати» (олівець) біля ролі у вікні «Ролі»: питає роль у сервера
   * (`GET /api/v1/roles/{id}`) і показує форму, заповнену її назвами. Поки
   * йде запит чи якщо він не вдався, замість форми — повідомлення.
   */
  protected async editRole(role: Role): Promise<void> {
    const request = ++this.roleRequest;
    this.roleEditView.set({ status: 'loading', role: null, failure: null });
    this.rolesMode.set('edit');
    const result = await this.roles.get(role.id);
    if (request !== this.roleRequest) {
      return;
    }
    this.roleEditView.set(
      typeof result === 'string' ? { status: 'failed', role: null, failure: result } : { status: 'ready', role: result, failure: null },
    );
  }

  /** «Зберегти» у формі редагування ролі. Заглушка: дані поки не надсилаємо. */
  protected updateRole(role: RoleDetails, changes: NewRole): void {
    console.log(`[ColleaguesPage] Зберегти роль ${role.id}: ${changes.name} (відправку ще не зроблено)`);
  }

  /** «Видалити» (смітник) біля ролі у вікні «Ролі». Заглушка: отримує роль. */
  protected deleteRole(role: Role): void {
    console.log(`[ColleaguesPage] Видалити роль: ${role.id} (поки не реалізовано)`);
  }

  /**
   * Запит ролей (`GET /api/v1/roles`) при відкритті вікна «Ролі»: старий список
   * лишається, поки йде оновлення; невдача показує причину (список, що вже є, теж).
   * Запит, що ще триває, не дублюється.
   */
  private async loadRoles(): Promise<void> {
    if (this.rolesLoading) {
      return;
    }
    this.rolesLoading = true;
    const previous = this.rolesView().roles;
    this.rolesView.set({ status: 'loading', roles: previous, failure: null });
    try {
      const result = await this.roles.all();
      this.rolesView.set(
        typeof result === 'string'
          ? { status: 'failed', roles: previous, failure: result }
          : { status: 'ready', roles: result, failure: null },
      );
    } finally {
      this.rolesLoading = false;
    }
  }

  // Заглушки дій з колегою: отримують його `id` та імʼя з запиту й відкривають бічне
  // вікно, самих дій поки нема.

  /** «Змінити ролі» (`AssignRoleToUser` або `ManageRole`). Заглушка. */
  protected changePermissions(colleague: ColleagueRef): void {
    console.log(`[ColleaguesPage] Змінити права користувача: ${colleague.id} (поки не реалізовано)`);
    this.openPanel('permissions', colleague);
  }

  /** «Редагувати» (`UpdateUser`). Заглушка. */
  protected editColleague(colleague: ColleagueRef): void {
    console.log(`[ColleaguesPage] Редагувати користувача: ${colleague.id} (поки не реалізовано)`);
    this.openPanel('edit', colleague);
  }

  /** «Скинути пароль» (`CanResetPassword`). Заглушка. */
  protected resetPassword(colleague: ColleagueRef): void {
    console.log(`[ColleaguesPage] Скинути пароль користувача: ${colleague.id} (поки не реалізовано)`);
    this.openPanel('resetPassword', colleague);
  }

  /** «Вимкнути акаунт» (`DisableUser`). Заглушка. */
  protected disableColleague(colleague: ColleagueRef): void {
    console.log(`[ColleaguesPage] Вимкнути користувача: ${colleague.id} (поки не реалізовано)`);
    this.openPanel('disable', colleague);
  }

  /** «Ввімкнути акаунт» (`UpdateUser`). Заглушка. */
  protected enableColleague(colleague: ColleagueRef): void {
    console.log(`[ColleaguesPage] Ввімкнути користувача: ${colleague.id} (поки не реалізовано)`);
    this.openPanel('enable', colleague);
  }

  /** «Видалити» (`DeleteUser`). Заглушка. */
  protected deleteColleague(colleague: ColleagueRef): void {
    console.log(`[ColleaguesPage] Видалити користувача: ${colleague.id} (поки не реалізовано)`);
    this.openPanel('delete', colleague);
  }

  /** Рядок із шаблону комірки: типізація для `let-row`. */
  protected asColleague(row: DataTableRow): ColleagueRow {
    return row as ColleagueRow;
  }
}
