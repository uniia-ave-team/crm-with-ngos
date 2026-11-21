import { Component, OnInit, TemplateRef, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { IonButtons, IonContent, IonHeader, IonIcon, IonInfiniteScroll, IonInfiniteScrollContent, IonTitle, IonToolbar } from '@ionic/angular';

import { AccountsService } from '../core/accounts/accounts.service';
import { ConnectionService } from '../core/connection/connection.service';
import { PERMISSIONS, hasAnyPermission } from '../core/auth/permissions';
import { LanguageService } from '../core/i18n/language.service';
import { RequestFailure } from '../core/http/request-failure';
import { InvitationsService } from '../core/invitations/invitations.service';
import { diffClaims } from '../core/roles/role-permissions';
import { Role, RoleDetails, RolesService } from '../core/roles/roles.service';
import { CurrentUserService } from '../core/users/current-user.service';
import { PRONOUN_CATEGORY } from '../core/users/pronouns';
import { UserRole } from '../core/users/user-profile';
import { UserSummary, UsersService, UsersSort } from '../core/users/users.service';
import { DataTableCellContext, DataTableColumn, DataTableRow, DataTableRowMenuEvent, DataTableSort, DataTableComponent } from '../shared/data-table/data-table.component';
import { ConfirmDialogComponent } from '../shared/confirm-dialog/confirm-dialog.component';
import { ModalComponent } from '../shared/modal/modal.component';
import { PopoverComponent } from '../shared/popover/popover.component';
import { SidePanelComponent } from '../shared/side-panel/side-panel.component';
import { ColleagueAction, ColleagueActionItem, ColleagueActionsComponent } from './colleague-actions/colleague-actions.component';
import { ColleagueFormComponent, NewColleague } from './colleague-form/colleague-form.component';
import { InvitationShare, InvitationStatus, InvitationDialogComponent } from './invitation-dialog/invitation-dialog.component';
import { NewRole, RoleFormComponent } from './role-form/role-form.component';
import { RolesPanelComponent } from './roles-panel/roles-panel.component';
import { RolesView } from './roles-view';
import { AvatarComponent } from '../shared/avatar/avatar.component';
import { RolePillComponent } from '../shared/role-pill/role-pill.component';

/** Стан форми редагування ролі: запит ролі (`GET /api/v1/roles/{id}`) і сама роль, коли вона є. */
export interface RoleEditView {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly role: RoleDetails | null;
  /** Причина невдачі (лише для `failed`). */
  readonly failure: RequestFailure | null;
}

/** Скільки колег підвантажується за один запит. */
export const COLLEAGUES_PAGE_SIZE = 20;

/**
 * Порядок колег, поки користувач не вибрав інший: за ПІБ, від А до Я (таблиця позначає це
 * на заголовку «ПІБ», `defaultSortColumnId`). Без сортування сервер
 * віддає колег за `id`, тобто в порядку, що для людини виглядає випадковим. `LastName` сервер
 * розуміє як «прізвище, потім імʼя, потім по батькові».
 */
const DEFAULT_SORT: UsersSort = { orderBy: 'LastName', sortOrder: 'asc' };

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
 * таблиця (`app-data-table`). «Ролі» й «Додати» працюють (список і форми ролей, запрошення
 * колеги); пошук і дії з колегою в меню біля трьох крапок поки не зроблено. Колонки
 * (`columns`) і рядки (`rows`) задаються кодом, тож їх можна змінити, не чіпаючи розмітки;
 * вигляд складних комірок — шаблони внизу розмітки сторінки.
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
 * з меню біля трьох крапок. Для «Ролі» й «Додати» в ньому список ролей, форми ролі й
 * форма колеги; для дій (функції-заглушки нижче) вікно поки порожнє, а `panel` каже, що
 * саме його відкрило й для якого колеги.
 *
 * Колеги приходять із `GET /api/v1/users` по 20 (перша сторінка при відкритті,
 * далі підвантаження при прокрутці до кінця таблиці, `ion-infinite-scroll`). У рядках —
 * ПІБ, аватар, займенники, ролі, пошта й телефон з відповіді; чого нема — прочерк. Рядки
 * «проєкт — роль» під ролями поки зразкові (`colleagues.sample.line`): проєктів сервер ще
 * не віддає.
 */
@Component({
  selector: 'app-colleagues',
  templateUrl: './colleagues.page.html',
  styleUrls: ['./colleagues.page.scss'],
  imports: [
    IonButtons,
    IonContent,
    IonHeader,
    IonIcon,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    IonTitle,
    IonToolbar,
    DataTableComponent,
    PopoverComponent,
    ColleagueActionsComponent,
    SidePanelComponent,
    RoleFormComponent,
    ColleagueFormComponent,
    RolesPanelComponent,
    ModalComponent,
    ConfirmDialogComponent,
    InvitationDialogComponent,
    AvatarComponent,
    RolePillComponent,
    TranslatePipe,
  ],
})
export class ColleaguesPage implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService).language;
  private readonly roles = inject(RolesService);
  private readonly users = inject(UsersService);
  private readonly invitations = inject(InvitationsService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly permissions = this.currentUser.permissions;
  private readonly router = inject(Router);
  private readonly accounts = inject(AccountsService);
  private readonly connection = inject(ConnectionService);

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
  private readonly invitationModal = viewChild<ModalComponent>('invitationDialog');
  private readonly roleDeleteModal = viewChild<ModalComponent>('roleDeleteDialog');
  /** Роль у вікні підтвердження видалення (лишається, поки вікно закривається). */
  protected readonly roleToDelete = signal<Role | null>(null);
  /** Іде видалення ролі: кнопка «Так» вимкнена. */
  protected readonly roleDeleting = signal(false);
  /** Ключ помилки видалення ролі (`null` — нема). */
  protected readonly roleDeleteError = signal<string | null>(null);
  private readonly colleagueForm = viewChild(ColleagueFormComponent);
  /** Іде відправка форми нового колеги. */
  protected readonly colleagueSaving = signal(false);
  /** Ключ помилки відправки форми нового колеги (`null` — нема). */
  protected readonly colleagueSaveError = signal<string | null>(null);
  /** Створене запрошення для вікна з токеном (лишається, поки вікно закривається). */
  protected readonly invitation = signal<{
    readonly email: string;
    readonly token: string;
    readonly expiresAt: string;
    /** Origin сервера, на якому створено запрошення (за ним будується посилання). */
    readonly server: string;
  } | null>(null);
  /** Що вийшло з копіюванням чи поширенням посилання у вікні запрошення. */
  protected readonly invitationStatus = signal<InvitationStatus>('idle');

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
  /** Іде відправка форми нової ролі. */
  protected readonly roleSaving = signal(false);
  /** Ключ помилки відправки форми нової ролі (`null` — нема). */
  protected readonly roleSaveError = signal<string | null>(null);
  /** Ключ повідомлення над списком ролей (наприклад, не всі права додалися); `null` — нема. */
  protected readonly rolesNotice = signal<string | null>(null);
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
   * Колонки; `orderBy` — що йде в `OrderBy` запиту при сортуванні за колонкою (одне імʼя
   * властивості `UserDto`). «ПІБ» — `LastName`: сервер сортує за ним прізвище, потім імʼя,
   * потім по батькові. «Посада» поки без сортування (порожній), «Пошта» — `Email`,
   * «Телефон» — `PhoneNumber`.
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
  /**
   * Сортування запиту колег: колонка, за якою клацнули, а поки не клацнули (чи сортування
   * зняли, чи колонка не сортується) — за ПІБ (`DEFAULT_SORT`). Діє й на наступні сторінки.
   */
  private sort: UsersSort = DEFAULT_SORT;
  /** Номер «покоління» списку: зростає при кожному новому сортуванні, відповідь на давніший запит ігнорується. */
  private colleaguesGeneration = 0;

  /**
   * Рядки з полів колеги (`UserSummary`): ПІБ («Прізвище Імʼя По батькові», як на плитках
   * акаунтів), пошта й телефон; чого нема — прочерк (`common.empty`), а не вигадане значення.
   * Займенники — за `pronounsText`. Тексти перераховуються при зміні мови.
   */
  protected readonly rows = computed<readonly ColleagueRow[]>(() => {
    this.language();
    const t = (key: string) => this.translate.instant(key) as string;
    const empty = t('common.empty');
    const lineText = t('colleagues.sample.line');
    return this.colleagues().users.map((user) => ({
      id: user.id,
      name: fullName(user) || empty,
      email: user.email.trim() || empty,
      pronouns: this.pronounsText(user),
      phone: user.phoneNumber?.trim() || empty,
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
   * `SortOrder=asc`, другий — `desc`, третій — знову сортування за замовчуванням (за ПІБ).
   * Колонка з порожнім `orderBy` теж очищає таблицю, але сортується за замовчуванням.
   * Вибране сортування діє й на наступні сторінки при прокрутці.
   */
  protected onSort(sort: DataTableSort | null): void {
    this.sort = sort && sort.column.orderBy ? { orderBy: sort.column.orderBy, sortOrder: sort.direction } : DEFAULT_SORT;
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
    const colleague: ColleagueRef = { id, name: user ? fullName(user) || null : null };
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
      this.colleagueSaveError.set(null);
      void this.loadRoles();
    }
    if (kind === 'roles') {
      // «Ролі» завжди відкривають на списку, а не на формі, де зупинились минулого разу.
      this.roleRequest++;
      this.roleSaveError.set(null);
      this.rolesNotice.set(null);
      this.rolesMode.set('list');
      void this.loadRoles();
    }
  }

  /** «Додати роль» у шапці вікна «Ролі»: замість списку з'являється форма нової ролі. */
  protected addRole(): void {
    this.roleSaveError.set(null);
    this.rolesMode.set('add');
  }

  /** «Назад» у шапці вікна на формі ролі: знову список ролей. */
  protected backToRoles(): void {
    this.roleRequest++;
    this.roleSaveError.set(null);
    this.rolesMode.set('list');
  }

  /**
   * «Додати» у формі нового колеги: створює запрошення (`POST /api/v1/invitations`, пошта й
   * вибрані ролі). Поки йде відправка, кнопка вимкнена. Успіх очищає форму, закриває бічне
   * вікно й показує в модальному вікні посилання для колеги з отриманим токеном
   * (`<домен сервера>/join?token=…`, див. `InvitationDialogComponent`); невдача лишає форму з
   * помилкою за причиною.
   */
  protected async createColleague(colleague: NewColleague): Promise<void> {
    if (this.colleagueSaving()) {
      return;
    }
    this.colleagueSaving.set(true);
    this.colleagueSaveError.set(null);
    // Сервер, на якому створюється запрошення: посилання для колеги має вести саме на нього.
    const server = this.connection.serverUrl();
    const result = await this.invitations.invite(colleague.email, colleague.roleIds);
    this.colleagueSaving.set(false);
    if (typeof result === 'string') {
      this.colleagueSaveError.set(`colleagues.colleagueForm.saveError.${result}`);
      return;
    }
    this.invitation.set({ email: colleague.email, token: result.token, expiresAt: result.expiresAt, server });
    this.invitationStatus.set('idle');
    this.colleagueForm()?.reset();
    this.sidePanel()?.close();
    this.invitationModal()?.open();
  }

  /** Кнопка копіювання у вікні запрошення: кладе посилання для колеги в буфер обміну. */
  protected async copyInvitationLink(link: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(link);
      this.invitationStatus.set('copied');
    } catch {
      this.invitationStatus.set('failed');
    }
  }

  /**
   * Кнопка «Поділитися» у вікні запрошення: вбудований засіб поширення браузера чи системи
   * (`navigator.share`) із заголовком, підписом і посиланням. Те, що користувач сам закрив
   * вікно поширення, не помилка; інша невдача показується під посиланням.
   */
  protected async shareInvitation(data: InvitationShare): Promise<void> {
    try {
      await navigator.share(data);
      this.invitationStatus.set('idle');
    } catch (error) {
      if ((error as { name?: string } | null)?.name !== 'AbortError') {
        this.invitationStatus.set('shareFailed');
      }
    }
  }

  /** Хрестик у вікні запрошення. */
  protected closeInvitation(): void {
    void this.invitationModal()?.close();
  }

  /**
   * «Додати» у формі нової ролі: створює роль (`POST /api/v1/roles`) і додає їй права
   * з форми — вибраних груп і права без налаштування (`claimsOfGroups`) — окремими
   * запитами. Поки йде відправка, кнопка вимкнена. Успіх повертає до
   * списку ролей і оновлює його (якщо якісь права не додалися — з попередженням); невдача
   * лишає форму з помилкою за причиною.
   */
  protected async createRole(role: NewRole): Promise<void> {
    if (this.roleSaving()) {
      return;
    }
    this.roleSaving.set(true);
    this.roleSaveError.set(null);
    const result = await this.roles.create(
      { name: role.name, feminitiveName: role.feminitiveName, pluralName: role.pluralName },
      role.permissions,
    );
    this.roleSaving.set(false);
    if (typeof result === 'string') {
      this.roleSaveError.set(`colleagues.roleForm.saveError.${result}`);
      return;
    }
    this.rolesNotice.set(result.failedClaims.length > 0 ? 'colleagues.rolesPanel.noticePartial' : null);
    this.rolesMode.set('list');
    void this.loadRoles();
  }

  /**
   * «Редагувати» (олівець) біля ролі у вікні «Ролі»: питає роль у сервера
   * (`GET /api/v1/roles/{id}`) і показує форму, заповнену її назвами. Поки
   * йде запит чи якщо він не вдався, замість форми — повідомлення.
   */
  protected async editRole(role: Role): Promise<void> {
    const request = ++this.roleRequest;
    this.roleSaveError.set(null);
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

  /**
   * «Зберегти» у формі редагування ролі: оновлює назви (`PUT /api/v1/roles/{id}`) і окремими
   * запитами додає та прибирає права за різницею з початковими `claims` ролі (`diffClaims`:
   * лише групи, які користувач може надати; права без налаштування додаються, якщо їх нема).
   * Поки йде відправка, кнопка вимкнена. Успіх повертає до оновленого списку (якщо якусь зміну
   * прав не вдалося застосувати — з попередженням); невдача лишає форму з помилкою за причиною.
   */
  protected async updateRole(role: RoleDetails, changes: NewRole): Promise<void> {
    if (this.roleSaving()) {
      return;
    }
    this.roleSaving.set(true);
    this.roleSaveError.set(null);
    const result = await this.roles.update(
      role.id,
      { name: changes.name, feminitiveName: changes.feminitiveName, pluralName: changes.pluralName },
      diffClaims(role.claims, changes.permissions, this.permissions()),
    );
    this.roleSaving.set(false);
    if (typeof result === 'string') {
      this.roleSaveError.set(`colleagues.roleForm.updateError.${result}`);
      return;
    }
    this.rolesNotice.set(result.failedClaims.length > 0 ? 'colleagues.rolesPanel.noticePartialUpdate' : null);
    this.rolesMode.set('list');
    void this.loadRoles();
  }

  /** «Видалити» (смітник) біля ролі у вікні «Ролі»: питає підтвердження у вікні по центру екрана. */
  protected deleteRole(role: Role): void {
    if (this.roleDeleting()) {
      return;
    }
    this.roleToDelete.set(role);
    this.roleDeleteError.set(null);
    this.roleDeleteModal()?.open();
  }

  /**
   * «Так» у вікні підтвердження: `DELETE /api/v1/roles/{id}`. Поки йде запит, кнопка вимкнена.
   * Успіх закриває вікно, прибирає роль зі списку й оновлює його з сервера; невдача лишає вікно
   * з помилкою за причиною (зокрема 400: роль має хтось із колег або вона системна).
   */
  protected async confirmRoleDelete(role: Role): Promise<void> {
    if (this.roleDeleting()) {
      return;
    }
    this.roleDeleting.set(true);
    this.roleDeleteError.set(null);
    const result = await this.roles.delete(role.id);
    this.roleDeleting.set(false);
    if (result !== 'deleted') {
      this.roleDeleteError.set(`colleagues.rolesPanel.deleteError.${result}`);
      return;
    }
    void this.roleDeleteModal()?.close();
    this.rolesNotice.set(null);
    this.rolesView.update((view) => ({ ...view, roles: view.roles.filter((candidate) => candidate.id !== role.id) }));
    void this.loadRoles();
  }

  /** «Скасувати» у вікні підтвердження видалення ролі. */
  protected cancelRoleDelete(): void {
    void this.roleDeleteModal()?.close();
  }

  /** Вікно підтвердження закрито будь-яким способом: забуваємо роль і помилку (якщо запит не йде). */
  protected onRoleDeleteClosed(): void {
    if (this.roleDeleting()) {
      return;
    }
    this.roleToDelete.set(null);
    this.roleDeleteError.set(null);
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
    this.openPanel('permissions', colleague);
  }

  /** «Редагувати» (`UpdateUser`). Заглушка. */
  protected editColleague(colleague: ColleagueRef): void {
    this.openPanel('edit', colleague);
  }

  /** «Скинути пароль» (`CanResetPassword`). Заглушка. */
  protected resetPassword(colleague: ColleagueRef): void {
    this.openPanel('resetPassword', colleague);
  }

  /** «Вимкнути акаунт» (`DisableUser`). Заглушка. */
  protected disableColleague(colleague: ColleagueRef): void {
    this.openPanel('disable', colleague);
  }

  /** «Ввімкнути акаунт» (`UpdateUser`). Заглушка. */
  protected enableColleague(colleague: ColleagueRef): void {
    this.openPanel('enable', colleague);
  }

  /** «Видалити» (`DeleteUser`). Заглушка. */
  protected deleteColleague(colleague: ColleagueRef): void {
    this.openPanel('delete', colleague);
  }

  /** Рядок із шаблону комірки: типізація для `let-row`. */
  protected asColleague(row: DataTableRow): ColleagueRow {
    return row as ColleagueRow;
  }
}

/** ПІБ колеги: прізвище, імʼя й по батькові (що з них є) через пробіл; нічого нема — порожньо. */
function fullName(user: UserSummary): string {
  return [user.lastName, user.firstName, user.patronymic ?? '']
    .map((part) => part.trim())
    .filter(Boolean)
    .join(' ');
}
