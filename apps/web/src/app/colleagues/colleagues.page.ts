import { Component, OnInit, TemplateRef, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { IonContent, IonHeader, IonIcon, IonInfiniteScroll, IonInfiniteScrollContent, IonToast, IonToolbar } from '@ionic/angular';

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
import { UserProfile, UserRole } from '../core/users/user-profile';
import { SEARCH_MAX_LENGTH, UserSummary, UsersService, UsersSort } from '../core/users/users.service';
import { DataTableCellContext, DataTableColumn, DataTableRow, DataTableRowMenuEvent, DataTableSort, DataTableComponent } from '../shared/data-table/data-table.component';
import { ConfirmDialogComponent } from '../shared/confirm-dialog/confirm-dialog.component';
import { ModalComponent } from '../shared/modal/modal.component';
import { PopoverComponent } from '../shared/popover/popover.component';
import { SidePanelComponent } from '../shared/side-panel/side-panel.component';
import { ColleagueAction, ColleagueActionItem, ColleagueActionsComponent } from './colleague-actions/colleague-actions.component';
import { ColleagueFormComponent, NewColleague } from './colleague-form/colleague-form.component';
import { InvitationKind, InvitationShare, InvitationStatus, InvitationDialogComponent } from './invitation-dialog/invitation-dialog.component';
import { NewRole, RoleFormComponent } from './role-form/role-form.component';
import { RoleChoicesComponent } from './role-choices/role-choices.component';
import { RolesPanelComponent } from './roles-panel/roles-panel.component';
import { RolesView } from './roles-view';
import { AvatarComponent } from '../shared/avatar/avatar.component';
import { RolePillComponent } from '../shared/role-pill/role-pill.component';
import { ActionButtonComponent } from '../shared/action-button/action-button.component';
import { SearchHeaderComponent } from '../shared/search-header/search-header.component';
import { ProfileFormComponent, ProfileFormValue } from '../shared/profile-form/profile-form.component';

/** Стан форми редагування ролі: запит ролі (`GET /api/v1/roles/{id}`) і сама роль, коли вона є. */
export interface RoleEditView {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly role: RoleDetails | null;
  /** Причина невдачі (лише для `failed`). */
  readonly failure: RequestFailure | null;
}

/** Стан форми «Редагувати» колегу: запит профілю (`GET /api/v1/users/{id}`) і сам профіль. */
export interface ColleagueEditView {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly profile: UserProfile | null;
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

/** Дії з колегою, які спершу підтверджують у вікні по центру екрана. */
type ConfirmedColleagueAction = 'disable' | 'delete' | 'resetPassword';

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

/**
 * Пункт меню дій з колегою й права, з яких він показується: хоча б одне з `requiresAny` і, якщо
 * задано, усі `requiresAll`.
 */
interface ColleagueActionRule extends ColleagueActionItem {
  readonly requiresAny: readonly string[];
  readonly requiresAll?: readonly string[];
}

const ACTION_RULES: readonly ColleagueActionRule[] = [
  // Форма редагування спершу читає профіль колеги (`GET /users/{id}`), а це вимагає `ViewUser`.
  {
    id: 'edit',
    labelKey: 'colleagues.actions.edit',
    icon: 'create-outline',
    requiresAny: [PERMISSIONS.updateUser],
    requiresAll: [PERMISSIONS.viewUser],
  },
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
  /** Екстрений контакт (колонка лише з правом `ViewEmergencyContact`); нема — прочерк. */
  readonly emergencyContact: string;
  /** Акаунт увімкнено; вимкнений — сірий аватар, приглушене імʼя й іконка перед ним. */
  readonly isActive: boolean;
}

/**
 * Вкладка «Колеги» (`/colleagues`): шапка з кнопками «Ролі», «Додати» і пошуку та
 * таблиця (`app-data-table`). «Ролі» й «Додати» працюють (список і форми ролей, запрошення
 * колеги); пошук і частина дій з колегою в меню біля трьох крапок поки не зроблені. Колонки
 * (`columns`) і рядки (`rows`) задаються кодом, тож їх можна змінити, не чіпаючи розмітки;
 * вигляд складних комірок — шаблони внизу розмітки сторінки.
 *
 * Що показується, залежить від прав користувача (права активного акаунта зі
 * знімка в `localStorage`): кнопка «Додати» — з `Permissions.CreateUser`; три
 * крапки в кінці рядків відкривають меню дій, у якому кожен пункт має своє
 * право («Редагувати» — `UpdateUser` разом із `ViewUser`, «Скинути пароль» — `CanResetPassword`, «Вимкнути акаунт» —
 * `DisableUser`, «Ввімкнути акаунт» — `UpdateUser`, «Видалити» — `DeleteUser`),
 * а без жодного з них крапок нема; «+» в кінці рядка заголовків — з
 * `Permissions.ViewCustomFields`. У вікні «Ролі» кнопки «Додати роль»,
 * «Редагувати» й «Видалити» — з `Permissions.ManageRole` (список ролей видно
 * всім). Саму вкладку в бічному меню показують лише з `Permissions.ViewUser`
 * (`PrimaryNavComponent`).
 *
 * Бічне вікно (`app-side-panel`) відкривають кнопки «Ролі» й «Додати» та дія «Редагувати» з
 * меню біля трьох крапок: у ньому список ролей, форми ролі, форма колеги й форма профілю колеги;
 * `panel` каже, що саме його відкрило й для якого колеги. «Вимкнути акаунт», «Видалити» й «Скинути пароль» питають
 * підтвердження у вікні по центру екрана й тоді шлють запит (скидання пароля після цього
 * показує посилання для колеги), «Ввімкнути акаунт» шле його одразу; після вимкнення,
 * ввімкнення й видалення список оновлюється.
 *
 * Колеги приходять із `GET /api/v1/users` по 20 (перша сторінка при кожному відкритті
 * вкладки, далі підвантаження при прокрутці до кінця таблиці, `ion-infinite-scroll`). У рядках —
 * ПІБ, аватар, займенники, ролі, пошта й телефон з відповіді; чого нема — прочерк. Рядки
 * «проєкт — роль» під ролями поки зразкові (`colleagues.sample.line`): проєктів сервер ще
 * не віддає.
 */
@Component({
  selector: 'app-colleagues',
  templateUrl: './colleagues.page.html',
  styleUrls: ['./colleagues.page.scss'],
  imports: [
    IonContent,
    IonHeader,
    IonIcon,
    IonInfiniteScroll,
    IonInfiniteScrollContent,
    IonToast,
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
    SearchHeaderComponent,
    ActionButtonComponent,
    ProfileFormComponent,
    RoleChoicesComponent,
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
  /** Може змінювати ролі колеги: ролі у формі «Редагувати». */
  protected readonly canAssignRoles = computed(() => this.permissions().includes(PERMISSIONS.assignRoleToUser));
  /** Дії з колегою, доступні користувачеві (пункти меню біля трьох крапок). */
  protected readonly actions = computed<readonly ColleagueActionItem[]>(() =>
    ACTION_RULES.filter(
      (rule) =>
        hasAnyPermission(this.permissions(), rule.requiresAny) &&
        (rule.requiresAll ?? []).every((permission) => this.permissions().includes(permission)),
    ),
  );
  /** Може щось робити з колегою: показуємо три крапки в кінці рядків. */
  protected readonly canOpenRowMenu = computed(() => this.actions().length > 0);

  private readonly rowMenu = viewChild(PopoverComponent);
  private readonly sidePanel = viewChild(SidePanelComponent);
  private readonly invitationModal = viewChild<ModalComponent>('invitationDialog');
  private readonly roleDeleteModal = viewChild<ModalComponent>('roleDeleteDialog');
  private readonly colleagueConfirmModal = viewChild<ModalComponent>('colleagueConfirmDialog');
  /**
   * Дія з колегою, що чекає підтвердження у вікні по центру екрана: вимкнення чи видалення
   * (`name` — як назвати колегу в питанні). Лишається, поки вікно закривається.
   */
  protected readonly colleagueConfirm = signal<{
    readonly action: ConfirmedColleagueAction;
    readonly colleague: ColleagueRef;
    readonly name: string;
  } | null>(null);
  /** Іде підтверджений запит (вимкнення чи видалення): кнопка «Так» вимкнена. */
  protected readonly colleagueActing = signal(false);
  /** Ключ помилки у вікні підтвердження (`null` — нема). */
  protected readonly colleagueConfirmError = signal<string | null>(null);
  /** Ключ помилки ввімкнення акаунта для спливаючого повідомлення внизу (`null` — нема). */
  protected readonly enableError = signal<string | null>(null);
  /** Колеги, чиї акаунти зараз вмикаються: повторний вибір того самого не дублює запит. */
  private readonly enabling = new Set<string>();
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
  /**
   * Створене посилання для вікна з токеном: запрошення чи скидання пароля (`kind`; лишається,
   * поки вікно закривається).
   */
  protected readonly invitation = signal<{
    readonly kind: InvitationKind;
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
  /** Профіль колеги для форми «Редагувати» (запит іде при кожному її відкритті). */
  protected readonly colleagueEditView = signal<ColleagueEditView>({ status: 'loading', profile: null, failure: null });
  /** Номер запиту профілю: відповідь на давніший (вікно відкрили для іншого колеги) ігнорується. */
  private colleagueEditRequest = 0;
  /** Ролі, вибрані у формі «Редагувати» (`id`): спершу — ролі з профілю колеги. */
  protected readonly colleagueEditRoles = signal<ReadonlySet<string>>(new Set());
  /**
   * Ролі, які колега має на сервері (`id`): з профілю, далі — після кожної вдалої зміни. З ними
   * при збереженні порівнюються вибрані (`colleagueEditRoles`).
   */
  private colleagueSavedRoles: ReadonlySet<string> = new Set();
  /** Ключ помилки ролей у формі «Редагувати» (після спроби зберегти без ролі); `null` — нема. */
  protected readonly colleagueEditRolesError = signal<string | null>(null);
  /** Іде збереження форми «Редагувати»: кнопка вимкнена. */
  protected readonly colleagueEditSaving = signal(false);
  /** Ключі помилок збереження форми «Редагувати» (над «Зберегти»); порожньо — нема. */
  protected readonly colleagueEditErrors = signal<readonly string[]>([]);
  private readonly colleagueProfileForm = viewChild(ProfileFormComponent);
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
  /**
   * Акаунт колеги, чиє меню відкрито, увімкнено. Лишається й після закриття меню: інакше
   * пункти мінялися б, поки воно ще зникає.
   */
  private readonly menuRowActive = signal(true);
  /**
   * Пункти відкритого меню: для себе — лише налаштування профілю, для інших — дії за правами,
   * з яких «Вимкнути» лише для ввімкненого акаунта, а «Ввімкнути» — лише для вимкненого.
   */
  protected readonly menuItems = computed<readonly ColleagueActionItem[]>(() =>
    this.itemsFor(this.menuForSelf(), this.menuRowActive()),
  );
  /** Рядок, для якого відкрите меню дій (лишається виділеним, поки меню відкрите); `null` — меню закрите. */
  protected readonly menuRowId = signal<string | null>(null);
  /**
   * Бачить додаткові поля: «+» в кінці рядка заголовків таблиці (додавання колонки). Колонок із
   * додатковими полями ще нема, тож кнопка позначена `planned` і не показується.
   */
  protected readonly canViewCustomFields = computed(() => this.permissions().includes(PERMISSIONS.viewCustomFields));
  /**
   * Може бачити екстрені контакти колег: показуємо їхню колонку. Без цього права сервер однаково
   * віддає замість чужого контакту `null`, тож колонка була б порожньою.
   */
  protected readonly canViewEmergencyContact = computed(() => this.permissions().includes(PERMISSIONS.viewEmergencyContact));

  private readonly personCell = viewChild<TemplateRef<DataTableCellContext>>('personCell');
  private readonly positionCell = viewChild<TemplateRef<DataTableCellContext>>('positionCell');

  /**
   * Колонки; `orderBy` — що йде в `OrderBy` запиту при сортуванні за колонкою (одне імʼя
   * властивості `UserDto`). «ПІБ» — `LastName`: сервер сортує за ним прізвище, потім імʼя,
   * потім по батькові. «Посада» поки без сортування (порожній), «Пошта» — `Email`,
   * «Телефон» — `PhoneNumber`. З правом `ViewEmergencyContact` після «Телефону» йде
   * «Екстрений контакт» — `EmergencyContact`, а ширини решти колонок трохи менші.
   */
  protected readonly columns = computed<readonly DataTableColumn[]>(() => {
    const emergency = this.canViewEmergencyContact();
    return [
      { id: 'name', labelKey: 'colleagues.columns.name', width: emergency ? '26%' : '30%', template: this.personCell(), orderBy: 'LastName' },
      { id: 'position', labelKey: 'colleagues.columns.position', width: emergency ? '24%' : '30%', template: this.positionCell(), orderBy: '' },
      { id: 'email', labelKey: 'colleagues.columns.email', width: emergency ? '18%' : '22%', orderBy: 'Email' },
      { id: 'phone', labelKey: 'colleagues.columns.phone', width: emergency ? '16%' : '18%', orderBy: 'PhoneNumber' },
      ...(emergency
        ? [{ id: 'emergencyContact', labelKey: 'colleagues.columns.emergencyContact', width: '16%', orderBy: 'EmergencyContact' }]
        : []),
    ];
  });

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
  /** Вкладку вже відкривали: наступні входи на неї оновлюють список (`ionViewWillEnter`). */
  private entered = false;

  protected readonly searchMaxLength = SEARCH_MAX_LENGTH;
  /** Пошук, за яким запитано поточний список (`SearchTerm`); `null` — без пошуку. */
  protected readonly searchTerm = signal<string | null>(null);

  /**
   * Рядки з полів колеги (`UserSummary`): ПІБ («Прізвище Імʼя По батькові», як на плитках
   * акаунтів), пошта, телефон і екстрений контакт; чого нема — прочерк (`common.empty`), а не
   * вигадане значення.
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
      emergencyContact: user.emergencyContact?.trim() || empty,
      avatarUrl: user.avatarUrl,
      firstName: user.firstName,
      lastName: user.lastName,
      roles: user.roles,
      lines: SAMPLE_LINES.map((color) => ({ text: lineText, color })),
      isActive: user.isActive,
    }));
  });

  ngOnInit(): void {
    void this.loadColleagues();
  }

  /**
   * Ionic тримає сторінку в стеку, тож `ngOnInit` спрацьовує лише при першому відкритті вкладки.
   * При кожному наступному поверненні на неї список запитується заново (`reloadColleagues`), щоб
   * показувати зміни, зроблені деінде.
   */
  ionViewWillEnter(): void {
    if (this.entered) {
      this.reloadColleagues();
    }
    this.entered = true;
  }

  /**
   * Будь-яка зміна в полі пошуку шапки (і закриття пошуку з непорожнім полем) одразу запитує
   * список заново з першої сторінки: введений рядок (без пробілів по краях) іде в `SearchTerm`,
   * порожній — `null`, тобто повний список. Відповідь на попередній запит, що ще йшов,
   * ігнорується. Сервер не приймає рядок, коротший за `SEARCH_MIN_LENGTH` символи (400): тоді
   * над таблицею помилка запиту.
   */
  protected onSearch(value: string): void {
    this.searchTerm.set(value.trim() || null);
    this.reloadColleagues();
  }

  /**
   * Запитує список з першої сторінки заново з тим самим сортуванням: показані рядки лишаються,
   * поки не прийде відповідь, а тоді замінюються нею. Запит, що ще йде, скасовується.
   */
  private reloadColleagues(): void {
    this.colleaguesGeneration++;
    this.colleaguesLoading = false;
    this.nextPage = 1;
    void this.loadColleagues(true);
  }

  /**
   * Підвантажує наступну сторінку колег (`GET /api/v1/users`, по `COLLEAGUES_PAGE_SIZE`)
   * і додає її до вже показаних (`fresh` — замінює ними показані: оновлення після дії з
   * колегою). Невдача лишає, що вже є, і показує причину; той самий виклик повторює запит.
   * Запит, що ще триває, не дублюється.
   */
  protected async loadColleagues(fresh = false): Promise<void> {
    if (this.colleaguesLoading) {
      return;
    }
    this.colleaguesLoading = true;
    const generation = this.colleaguesGeneration;
    const before = this.colleagues();
    // Оновлення з першої сторінки (`fresh`): таблиця показує старі рядки, поки не прийде відповідь,
    // а тоді замінюється нею, а не доповнюється.
    const kept = fresh ? [] : before.users;
    this.colleagues.set({ ...before, status: 'loading', failure: null });
    try {
      const result = await this.users.list(this.nextPage, COLLEAGUES_PAGE_SIZE, this.sort, this.searchTerm());
      if (generation !== this.colleaguesGeneration) {
        return;
      }
      if (typeof result === 'string') {
        this.colleagues.set({ ...before, status: 'failed', failure: result });
        return;
      }
      this.nextPage++;
      const known = new Set(kept.map((user) => user.id));
      this.colleagues.set({
        status: 'ready',
        users: [...kept, ...result.items.filter((user) => !known.has(user.id))],
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
    const self = this.isCurrentUser(event.row.id);
    const active = this.asColleague(event.row).isActive;
    // Порожнє меню не відкриваємо: так буває, коли з прав є лише те, що не стосується цього
    // акаунта (наприклад, лише `DisableUser`, а акаунт уже вимкнено).
    if (this.itemsFor(self, active).length === 0) {
      return;
    }
    this.menuForSelf.set(self);
    this.menuRowActive.set(active);
    this.menuRowId.set(event.row.id);
    this.rowMenu()?.open(event.anchor);
  }

  /** Пункти меню рядка: див. `menuItems`. */
  private itemsFor(self: boolean, active: boolean): readonly ColleagueActionItem[] {
    if (self) {
      return [PROFILE_SETTINGS_ITEM];
    }
    const hidden: ColleagueAction = active ? 'enable' : 'disable';
    return this.actions().filter((item) => item.id !== hidden);
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
      case 'edit':
        void this.editColleague(colleague);
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
    this.invitation.set({ kind: 'invitation', email: colleague.email, token: result.token, expiresAt: result.expiresAt, server });
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
   * з помилкою за причиною (зокрема `invalid`: роль має хтось із колег або вона системна).
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

  /**
   * «Редагувати» (`UpdateUser`): бічне вікно з формою профілю колеги (`app-profile-form`, та сама,
   * що в «Даних профілю»), заповненою з `GET /api/v1/users/{id}`. Поки йде запит чи якщо він не
   * вдався, замість форми — повідомлення. З правом `AssignRoleToUser` у формі ще й ролі плашками,
   * як у формі нового колеги (список — `GET /api/v1/roles`, спершу вибрані ролі з профілю).
   * «Зберегти» — `saveColleague`.
   */
  protected async editColleague(colleague: ColleagueRef): Promise<void> {
    const request = ++this.colleagueEditRequest;
    this.colleagueEditView.set({ status: 'loading', profile: null, failure: null });
    this.colleagueEditRoles.set(new Set());
    this.colleagueSavedRoles = new Set();
    this.colleagueEditErrors.set([]);
    this.colleagueEditRolesError.set(null);
    this.openPanel('edit', colleague);
    if (this.canAssignRoles()) {
      void this.loadRoles();
    }
    const result = await this.users.get(colleague.id);
    if (request !== this.colleagueEditRequest) {
      return;
    }
    if (typeof result === 'string') {
      this.colleagueEditView.set({ status: 'failed', profile: null, failure: result });
      return;
    }
    this.colleagueSavedRoles = new Set(result.roles.map((role) => role.id));
    this.colleagueEditRoles.set(this.colleagueSavedRoles);
    this.colleagueEditView.set({ status: 'ready', profile: result, failure: null });
  }

  /**
   * «Зберегти» у формі «Редагувати» (поля форма вже перевірила): `PUT /api/v1/users/{id}`; якщо
   * обрано файл — `POST /api/v1/users/{id}/avatar`; з правом `AssignRoleToUser` — ролі за
   * різницею вибраних із тими, що колега має (хоча б одна роль обовʼязкова, як у формі нового
   * колеги; без неї нічого не надсилається): нові — `POST /users/{id}/roles/{roleId}`,
   * зняті — `DELETE` (по черзі, див. `saveColleagueRoles`). Невдалий `PUT` зупиняє збереження.
   * Якщо не вдалося завантажити аватар чи змінити якусь роль, решта все одно зберігається, вікно лишається
   * відкритим з помилкою за кожну частину, а повторне «Зберегти» робить лише те, що лишилося
   * (вдалі зміни ролей запамʼятовуються). Усе вдалося — вікно закривається. Після будь-якої
   * вдалої зміни список колег запитується заново.
   */
  protected async saveColleague({ profile, avatar }: ProfileFormValue): Promise<void> {
    const id = this.panel()?.userId;
    if (!id || this.colleagueEditSaving()) {
      return;
    }
    // Як і у формі нового колеги, хоча б одна роль обовʼязкова (лише коли ролі видно й можна міняти).
    if (this.canAssignRoles() && this.colleagueEditRoles().size === 0) {
      this.colleagueEditRolesError.set('colleagues.colleagueForm.error.rolesRequired');
      return;
    }
    this.colleagueEditSaving.set(true);
    this.colleagueEditErrors.set([]);
    try {
      const updated = await this.users.updateProfile(profile, id);
      if (updated !== 'updated') {
        this.colleagueEditErrors.set([`colleagues.profileForm.saveError.${updated}`]);
        return;
      }
      const errors: string[] = [];
      if (avatar) {
        const uploaded = await this.users.uploadAvatar(avatar, id);
        if (uploaded === 'uploaded') {
          this.colleagueProfileForm()?.clearAvatar();
        } else {
          errors.push(`colleagues.profileForm.avatarError.${uploaded}`);
        }
      }
      if (this.canAssignRoles()) {
        const failure = await this.saveColleagueRoles(id);
        if (failure) {
          errors.push(`colleagues.profileForm.rolesError.${failure}`);
        }
      }
      this.reloadColleagues();
      if (errors.length > 0) {
        this.colleagueEditErrors.set(errors);
        return;
      }
      void this.sidePanel()?.close();
    } finally {
      this.colleagueEditSaving.set(false);
    }
  }

  /**
   * Ролі колеги за різницею вибраних із тими, що він має: спершу нові, потім зняті (так колега
   * посеред збереження не лишається без ролі), по одному запиту. Одночасно не можна: сервер не
   * приймає паралельних змін одного користувача (400, «Optimistic concurrency failure»). Вдалі
   * зміни запамʼятовуються в `colleagueSavedRoles`; невдала не зупиняє решту. Повертає причину
   * першої невдачі або `null`.
   */
  private async saveColleagueRoles(id: string): Promise<RequestFailure | null> {
    const selected = this.colleagueEditRoles();
    const saved = new Set(this.colleagueSavedRoles);
    const added = [...selected].filter((roleId) => !saved.has(roleId));
    const removed = [...saved].filter((roleId) => !selected.has(roleId));
    let failure: RequestFailure | null = null;
    for (const roleId of added) {
      const result = await this.users.assignRole(id, roleId);
      if (result === 'done') {
        saved.add(roleId);
      } else {
        failure ??= result;
      }
    }
    for (const roleId of removed) {
      const result = await this.users.removeRole(id, roleId);
      if (result === 'done') {
        saved.delete(roleId);
      } else {
        failure ??= result;
      }
    }
    this.colleagueSavedRoles = saved;
    return failure;
  }

  /** Натиснули на плашку ролі у формі «Редагувати»: роль вибирається чи знімається. */
  protected toggleColleagueEditRole(id: string): void {
    const selected = new Set(this.colleagueEditRoles());
    if (!selected.delete(id)) {
      selected.add(id);
    }
    this.colleagueEditRoles.set(selected);
    this.colleagueEditErrors.set([]);
    this.colleagueEditRolesError.set(null);
  }

  /**
   * «Скинути пароль» (`CanResetPassword`): спершу питаємо підтвердження, тоді
   * `POST /api/v1/users/{id}/password-reset-token` і вікно з посиланням (див.
   * `confirmColleagueAction`).
   */
  protected resetPassword(colleague: ColleagueRef): void {
    this.askToConfirm('resetPassword', colleague);
  }

  /** «Вимкнути акаунт» (`DisableUser`): спершу питаємо підтвердження. */
  protected disableColleague(colleague: ColleagueRef): void {
    this.askToConfirm('disable', colleague);
  }

  /**
   * «Ввімкнути акаунт» (`UpdateUser`): одразу `POST /api/v1/users/{id}/activate`. Успіх вмикає
   * колегу в таблиці й оновлює список з сервера; невдача — спливаюче повідомлення з причиною.
   */
  protected async enableColleague(colleague: ColleagueRef): Promise<void> {
    if (this.enabling.has(colleague.id)) {
      return;
    }
    this.enabling.add(colleague.id);
    this.enableError.set(null);
    const result = await this.users.activate(colleague.id);
    this.enabling.delete(colleague.id);
    if (result !== 'done') {
      this.enableError.set(`colleagues.actionError.enable.${result}`);
      return;
    }
    this.applyColleagueChange('enable', colleague.id);
  }

  /** «Видалити» (`DeleteUser`): спершу питаємо підтвердження. */
  protected deleteColleague(colleague: ColleagueRef): void {
    this.askToConfirm('delete', colleague);
  }

  /** Відкриває вікно підтвердження дії з колегою (як видалення ролі); без імені колегу називаємо поштою. */
  private askToConfirm(action: ConfirmedColleagueAction, colleague: ColleagueRef): void {
    if (this.colleagueActing()) {
      return;
    }
    const email = this.colleagues().users.find((user) => user.id === colleague.id)?.email.trim() ?? '';
    this.colleagueConfirm.set({ action, colleague, name: colleague.name ?? email });
    this.colleagueConfirmError.set(null);
    this.colleagueConfirmModal()?.open();
  }

  /**
   * «Так» у вікні підтвердження: `POST /api/v1/users/{id}/deactivate` чи `DELETE /api/v1/users/{id}`.
   * Поки йде запит, кнопка вимкнена. Успіх закриває вікно, змінює таблицю (вимкнений — сірий,
   * видалений зникає) й оновлює список з сервера; невдача лишає вікно з помилкою за причиною
   * (зокрема 400: останній адміністратор чи власний акаунт). Для скидання пароля — див.
   * `confirmPasswordReset`.
   */
  protected async confirmColleagueAction(): Promise<void> {
    const pending = this.colleagueConfirm();
    if (!pending || this.colleagueActing()) {
      return;
    }
    if (pending.action === 'resetPassword') {
      return this.confirmPasswordReset(pending.colleague);
    }
    this.colleagueActing.set(true);
    this.colleagueConfirmError.set(null);
    const id = pending.colleague.id;
    const result = pending.action === 'disable' ? await this.users.deactivate(id) : await this.users.delete(id);
    this.colleagueActing.set(false);
    if (result !== 'done') {
      this.colleagueConfirmError.set(`colleagues.actionError.${pending.action}.${result}`);
      return;
    }
    void this.colleagueConfirmModal()?.close();
    this.applyColleagueChange(pending.action, id);
  }

  /**
   * «Так» у питанні про скидання пароля: `POST /api/v1/users/{id}/password-reset-token`. Успіх
   * закриває питання й показує вікно з посиланням для колеги (`<домен сервера>/reset-password?
   * token=…`, те саме вікно, що й для запрошення, `kind: 'passwordReset'`), з поштою колеги й
   * датою, до якої воно діє; невдача лишає питання з помилкою за причиною.
   */
  private async confirmPasswordReset(colleague: ColleagueRef): Promise<void> {
    this.colleagueActing.set(true);
    this.colleagueConfirmError.set(null);
    // Сервер, на якому створено токен: посилання має вести саме на нього.
    const server = this.connection.serverUrl();
    const result = await this.users.createPasswordResetToken(colleague.id);
    this.colleagueActing.set(false);
    if (typeof result === 'string') {
      this.colleagueConfirmError.set(`colleagues.actionError.resetPassword.${result}`);
      return;
    }
    const email = this.colleagues().users.find((user) => user.id === colleague.id)?.email.trim() ?? '';
    this.invitation.set({ kind: 'passwordReset', email, token: result.token, expiresAt: result.expiresAt, server });
    this.invitationStatus.set('idle');
    await this.colleagueConfirmModal()?.close();
    this.invitationModal()?.open();
  }

  /** «Скасувати» у вікні підтвердження дії з колегою. */
  protected cancelColleagueAction(): void {
    void this.colleagueConfirmModal()?.close();
  }

  /** Вікно підтвердження закрито будь-яким способом: забуваємо дію й помилку (якщо запит не йде). */
  protected onColleagueConfirmClosed(): void {
    if (this.colleagueActing()) {
      return;
    }
    this.colleagueConfirm.set(null);
    this.colleagueConfirmError.set(null);
  }

  /**
   * Дія вдалася: таблиця одразу показує результат (вимкнений чи ввімкнений акаунт, видалений
   * колега зникає), а тоді список запитується з першої сторінки заново з тим самим сортуванням.
   */
  private applyColleagueChange(action: 'disable' | 'enable' | 'delete', id: string): void {
    this.colleagues.update((view) => ({
      ...view,
      users:
        action === 'delete'
          ? view.users.filter((user) => user.id !== id)
          : view.users.map((user) => (user.id === id ? { ...user, isActive: action === 'enable' } : user)),
    }));
    this.reloadColleagues();
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
