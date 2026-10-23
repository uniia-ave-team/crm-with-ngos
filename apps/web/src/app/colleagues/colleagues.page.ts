import { Component, TemplateRef, computed, inject, viewChild } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { COLLEAGUE_ACTIONS_PERMISSIONS, PERMISSIONS, hasAnyPermission } from '../core/auth/permissions';
import { LanguageService } from '../core/i18n/language.service';
import { CurrentUserService } from '../core/users/current-user.service';
import { DataTableCellContext, DataTableColumn, DataTableRow } from '../shared/data-table/data-table.component';

type BadgeTone = 'green' | 'teal' | 'gray';
type DotColor = 'green' | 'blue';

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
 * крапки в кінці рядків — з будь-яким із `UpdateUser`, `DisableUser`,
 * `DeleteUser`, `AssignRoleToUser`, `CanResetPassword`, `ManageRole`; «+» в
 * кінці рядка заголовків — з `Permissions.ViewCustomFields`. Саму вкладку
 * в бічному меню показують лише з `Permissions.ViewUser` (`PrimaryNavComponent`).
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
  /** Може щось робити з колегою: показуємо три крапки в кінці рядків. */
  protected readonly canOpenRowMenu = computed(() => hasAnyPermission(this.permissions(), COLLEAGUE_ACTIONS_PERMISSIONS));
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

  /** Рядок із шаблону комірки: типізація для `let-row`. */
  protected asColleague(row: DataTableRow): ColleagueRow {
    return row as ColleagueRow;
  }
}
