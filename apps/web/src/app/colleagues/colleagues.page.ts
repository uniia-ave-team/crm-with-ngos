import { Component, TemplateRef, computed, inject, signal, viewChild } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { PERMISSIONS, hasAnyPermission } from '../core/auth/permissions';
import { LanguageService } from '../core/i18n/language.service';
import { CurrentUserService } from '../core/users/current-user.service';
import {
  DataTableCellContext,
  DataTableColumn,
  DataTableRow,
  DataTableRowMenuEvent,
} from '../shared/data-table/data-table.component';
import { PopoverComponent } from '../shared/popover/popover.component';
import { SidePanelComponent } from '../shared/side-panel/side-panel.component';
import { ColleagueAction, ColleagueActionItem } from './colleague-actions/colleague-actions.component';

type BadgeTone = 'green' | 'teal' | 'gray';
type DotColor = 'green' | 'blue';

/** Що відкрито в бічному вікні: кнопки шапки або дія з колегою. */
export type ColleaguesPanelKind = 'roles' | 'add' | ColleagueAction;

/** Стан бічного вікна: що відкрито й для якого колеги (`null` — не для колеги). */
export interface ColleaguesPanelState {
  readonly kind: ColleaguesPanelKind;
  readonly userId: string | null;
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
  { id: 'delete', labelKey: 'colleagues.actions.delete', icon: 'trash-outline', requiresAny: [PERMISSIONS.deleteUser] },
];

/** Рядок таблиці колег. */
interface ColleagueRow extends DataTableRow {
  readonly pronouns: string;
  /** Бейдж над списком (`null` — без бейджа). */
  readonly badge: { readonly text: string; readonly tone: BadgeTone } | null;
  /** Рядки під бейджем: кольорова крапка й текст. */
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
 * право («Змінити права» — `AssignRoleToUser` або `ManageRole`, «Редагувати» —
 * `UpdateUser`, «Скинути пароль» — `CanResetPassword`, «Вимкнути» —
 * `DisableUser`, «Видалити» — `DeleteUser`), а без жодного з них крапок нема; «+» в
 * кінці рядка заголовків — з `Permissions.ViewCustomFields`. Саму вкладку
 * в бічному меню показують лише з `Permissions.ViewUser` (`PrimaryNavComponent`).
 *
 * Бічне вікно (`app-side-panel`) відкривають кнопки «Ролі» й «Додати» та кожна дія
 * з меню біля трьох крапок (функції-заглушки нижче); що в ньому показувати, ще не
 * зроблено — вікно порожнє, а `panel` каже, що саме його відкрило й для якого колеги.
 *
 * Рядки зараз — тимчасовий зразок із нейтральним текстом із мовного файлу
 * (`colleagues.sample.*`), щоб було видно вигляд таблиці; їх замінять дані з API.
 */
@Component({
  selector: 'app-colleagues',
  templateUrl: './colleagues.page.html',
  styleUrls: ['./colleagues.page.scss'],
  standalone: false,
})
export class ColleaguesPage {
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService).language;
  private readonly permissions = inject(CurrentUserService).permissions;

  /** Може додавати колег (кнопка «Додати»). */
  protected readonly canAdd = computed(() => this.permissions().includes(PERMISSIONS.createUser));
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

  protected readonly columns = computed<readonly DataTableColumn[]>(() => [
    { id: 'name', labelKey: 'colleagues.columns.name', width: '30%', template: this.personCell() },
    { id: 'position', labelKey: 'colleagues.columns.position', width: '30%', template: this.positionCell() },
    { id: 'email', labelKey: 'colleagues.columns.email', width: '22%' },
    { id: 'phone', labelKey: 'colleagues.columns.phone', width: '18%' },
  ]);

  /** Рядки: тимчасовий зразок, тексти перераховуються при зміні мови. */
  protected readonly rows = computed<readonly ColleagueRow[]>(() => {
    this.language();
    const t = (key: string) => this.translate.instant(key) as string;
    const common = { name: t('colleagues.sample.name'), email: t('colleagues.sample.email'), phone: t('colleagues.sample.phone') };
    const badge = (tone: BadgeTone) => ({ text: t('colleagues.sample.badge'), tone });
    const line = (color: DotColor) => ({ text: t('colleagues.sample.line'), color });
    return [
      { id: 'sample-1', ...common, pronouns: t('settings.personal.pronounShe'), badge: badge('green'), lines: [line('green'), line('blue')] },
      { id: 'sample-2', ...common, pronouns: t('settings.personal.pronounHe'), badge: badge('gray'), lines: [] },
      { id: 'sample-3', ...common, pronouns: t('settings.personal.pronounHe'), badge: badge('teal'), lines: [line('green')] },
      { id: 'sample-4', ...common, pronouns: t('settings.personal.pronounShe'), badge: badge('teal'), lines: [line('green')] },
    ];
  });

  /** Три крапки в рядку: відкриваємо меню дій біля кнопки. */
  protected openRowMenu(event: DataTableRowMenuEvent): void {
    this.menuRowId.set(event.row.id);
    this.rowMenu()?.open(event.anchor);
  }

  /**
   * Вибрали дію з меню: закриваємо меню й викликаємо функцію цієї дії з `id`
   * колеги, чиє меню було відкрите.
   */
  protected onActionChosen(action: ColleagueAction): void {
    const id = this.menuRowId();
    void this.rowMenu()?.close();
    if (id === null) {
      return;
    }
    switch (action) {
      case 'permissions':
        this.changePermissions(id);
        break;
      case 'edit':
        this.editColleague(id);
        break;
      case 'resetPassword':
        this.resetPassword(id);
        break;
      case 'disable':
        this.disableColleague(id);
        break;
      case 'delete':
        this.deleteColleague(id);
        break;
    }
  }

  /** Відкриває бічне вікно для кнопки шапки чи дії (`userId` — колега, якого стосується дія). */
  protected openPanel(kind: ColleaguesPanelKind, userId: string | null = null): void {
    this.panel.set({ kind, userId });
    this.sidePanel()?.open();
  }

  // Заглушки дій з колегою: отримують `id` користувача й відкривають бічне вікно,
  // самих дій поки нема.

  /** «Змінити права» (`AssignRoleToUser` або `ManageRole`). Заглушка. */
  protected changePermissions(userId: string): void {
    console.log(`[ColleaguesPage] Змінити права користувача: ${userId} (поки не реалізовано)`);
    this.openPanel('permissions', userId);
  }

  /** «Редагувати» (`UpdateUser`). Заглушка. */
  protected editColleague(userId: string): void {
    console.log(`[ColleaguesPage] Редагувати користувача: ${userId} (поки не реалізовано)`);
    this.openPanel('edit', userId);
  }

  /** «Скинути пароль» (`CanResetPassword`). Заглушка. */
  protected resetPassword(userId: string): void {
    console.log(`[ColleaguesPage] Скинути пароль користувача: ${userId} (поки не реалізовано)`);
    this.openPanel('resetPassword', userId);
  }

  /** «Вимкнути» (`DisableUser`). Заглушка. */
  protected disableColleague(userId: string): void {
    console.log(`[ColleaguesPage] Вимкнути користувача: ${userId} (поки не реалізовано)`);
    this.openPanel('disable', userId);
  }

  /** «Видалити» (`DeleteUser`). Заглушка. */
  protected deleteColleague(userId: string): void {
    console.log(`[ColleaguesPage] Видалити користувача: ${userId} (поки не реалізовано)`);
    this.openPanel('delete', userId);
  }

  /** Рядок із шаблону комірки: типізація для `let-row`. */
  protected asColleague(row: DataTableRow): ColleagueRow {
    return row as ColleagueRow;
  }
}
