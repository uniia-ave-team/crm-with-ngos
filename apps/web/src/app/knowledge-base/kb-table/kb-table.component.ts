import { Component, DestroyRef, ElementRef, Injector, TemplateRef, afterNextRender, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { ActionButtonComponent } from '../../shared/action-button/action-button.component';
import {
  DataTableCellColors,
  DataTableCellContext,
  DataTableColumn,
  DataTableComponent,
  DataTableHeaderContext,
  DataTableRow,
  DataTableSort,
} from '../../shared/data-table/data-table.component';
import { PERMISSIONS } from '../../core/auth/permissions';
import { CustomRecordData, CustomRecordPlace, CustomRecordsService, CustomRecordsSort } from '../../core/custom-tables/custom-records.service';
import {
  CUSTOM_FIELD_NAME_MAX_LENGTH,
  CUSTOM_RECORD_VALUE_MAX_LENGTH,
  CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH,
  CUSTOM_TABLE_MAX_FIELDS,
  CustomTableDetails,
  CustomTableField,
  CustomTableUpdate,
  CustomTablesService,
} from '../../core/custom-tables/custom-tables.service';
import { TableTemplatesService } from '../../core/custom-tables/table-templates.service';
import { KbItemFormComponent, KbItemFormValue } from '../../core/knowledge-base/kb-item-form/kb-item-form.component';
import { SuccessNoticeService } from '../../core/notice/success-notice.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { SEARCH_MAX_LENGTH } from '../../core/users/users.service';
import { AutoSizeDirective } from '../../shared/auto-size/auto-size.directive';
import { downloadFile } from '../../shared/download/download-file';
import { RichTextDirective, RichTextFormat, formatSelection } from '../../shared/rich-text/rich-text.directive';
import { joinBackground, richTextToPlain, sanitizeRichText, splitBackground } from '../../shared/rich-text/rich-text';
import { cellTextColor } from '../../shared/color/cell-colors';
import { ColorPickerComponent } from '../../shared/color-picker/color-picker.component';
import { PopoverComponent } from '../../shared/popover/popover.component';
import { ActionBarContent, ActionBarItem, ActionBarService } from '../../shared/action-bar/action-bar.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { DialogHeaderComponent } from '../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { SearchHeaderComponent } from '../../shared/search-header/search-header.component';
import { injectKnowledgeBaseEntry } from '../kb-entry';
import { DebouncedSaver } from './debounced-saver';
import { XLSX_TYPE, sheetName, toXlsx, xlsxFileName } from './table-xlsx';
import { LocalRecord, mergeRecords } from './merge-records';
import { NEW_FIELD_PREFIX, isNewField, mergeFields } from './merge-fields';
import { CHECKBOX_OFF, CHECKBOX_ON, checkboxLosesText, checkboxState, setCheckbox, toCheckbox, toggleCheckboxFormat } from './cell-checkbox';

/** Стан таблиці у виді: завантажується, є чи не вдалося (ключ причини). */
type TableState =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly table: CustomTableDetails }
  | { readonly status: 'failed'; readonly errorKey: string };

/** `id` порожнього рядка, який стоїть під заголовками, поки записів нема. */
export const EMPTY_ROW_ID = 'kb-table-empty-row';

/**
 * Ширина стовпчика: назви полів довгі, тож стовпчики не стискаються до ширини блоку — таблиця
 * ширшає й прокручується вбік (на широкому екрані з кількома стовпчиками — на всю ширину).
 */
const COLUMN_WIDTH = '220px';

/** Початок `id` колонки: за ним `key` поля (так `id` колонки не збігається з `id` рядка). */
const FIELD_COLUMN_PREFIX = 'field:';

/** Як часто перевіряти таблицю на зміни з інших пристроїв (опис, стовпчики, записи). */
export const POLL_INTERVAL_MS = 10_000;

/** Дії з вибраною клітинкою (смуга дій знизу сторінки). */
type CellAction =
  | 'background'
  | 'bold'
  | 'italic'
  | 'underline'
  | 'strikethrough'
  | 'addRowBelow'
  | 'deleteRow'
  | 'addColumnRight'
  | 'addColumnLeft'
  | 'deleteColumn'
  | 'checkbox'
  | 'clear'
  | 'copy';

/** Кнопка смуги дій (підпис — `knowledgeBase.table.cellActions.<id>`, якщо не вказано інший). */
const cellAction = (item: {
  readonly id: CellAction;
  readonly icon: string;
  readonly disabled: boolean;
  readonly swatch?: string | null;
  readonly labelKey?: string;
}): ActionBarItem<CellAction> => ({
  ...item,
  labelKey: item.labelKey ?? `knowledgeBase.table.cellActions.${item.id}`,
});

/**
 * Порожня клітинка — порожній рядок: значення — рядки, обовʼязковість `isRequired` не враховуємо
 * (на сервері її приберуть), а порожній рядок сервер приймає й в обовʼязковому полі.
 */
const EMPTY_VALUE = '';

/** Пауза після останнього символу в пошуку, перш ніж запитати записи (не запит на кожну літеру). */
export const SEARCH_DELAY_MS = 300;

/**
 * Скільки тіла запитів надсилати при закритті сторінки: браузер дає всім запитам із `keepalive`
 * разом до 64 КБ, тож трохи менше — із запасом на заголовки.
 */
const CLOSE_BUDGET_BYTES = 60_000;

/** Тег кольору тла навколо значення (`<div style="background-color: #rrggbb">…</div>`) — його довжина. */
const BACKGROUND_TAG_LENGTH = '<div style="background-color: #000000"></div>'.length;
/** Скільки тексту з форматуванням можна ввести в клітинку й у заголовок (ліміт сервера мінус тег кольору). */
const CELL_HTML_MAX_LENGTH = CUSTOM_RECORD_VALUE_MAX_LENGTH - BACKGROUND_TAG_LENGTH;
const HEAD_HTML_MAX_LENGTH = CUSTOM_FIELD_NAME_MAX_LENGTH - BACKGROUND_TAG_LENGTH;

/**
 * Що зробити після підтвердження: видалити рядок (ключ `LocalRecord.key`) чи стовпчик (поле) або
 * вставити прапорець у клітинку з текстом (текст зникне).
 */
type PendingDelete =
  | { readonly kind: 'row'; readonly key: string }
  | { readonly kind: 'column'; readonly field: CustomTableField }
  | { readonly kind: 'checkbox'; readonly key: string; readonly field: CustomTableField };

/** Вибрана клітинка (чи заголовок): рядок (`LocalRecord.key`), поле й елемент поля введення. */
interface SelectedCell {
  /** Ключ рядка; `null` — заголовок стовпчика (назва поля). */
  readonly key: string | null;
  readonly field: CustomTableField;
  readonly element: HTMLElement;
}

/**
 * Вид «таблиця» (`/knowledge-base/tables/:id`, `id` — вхід із адреси): назва (зі змісту, тож після
 * «Редагувати» оновлюється одразу), опис і сама таблиця — як список колег (`app-data-table`): на
 * всю ширину, прокручується вбік, масивний рядок заголовків. Стовпчики — поля таблиці в їхньому
 * порядку (`GET /api/v1/customtables/{id}`), рядки — записи (`GET /api/v1/customrecords`, право
 * `ViewCustomRecord`; без нього записи не запитуються). Записів нема — під заголовками порожній
 * рядок. Клік по заголовку сортує записи на сервері за значенням цього поля.
 *
 * Шапка — як у «Колегах» (`app-search-header` на тлі шапки сторінки): назва (до двох рядків, далі
 * трикрапка; повна — у підказці), праворуч контурний зелений тег «Зміни збережено» (стан
 * збереження: поки змін у таблиці нема, він завжди такий; поки відкрито пошук — схований), «Створити шаблон» (з
 * `CreateTableTemplate`), «Експортувати» (з `ViewCustomRecord`) — завжди лише іконки з підказкою — і лупа — пошук у записах
 * (`SearchTerm`). Дій у кнопок ще нема (TODO). Опис — приглушеним рядком під шапкою.
 *
 * Редагування (з `UpdateCustomTable`): опис — по кліку (поле на місці тексту; порожній —
 * «Додати опис»), клітинки — поля введення за типом поля без рамки (текстові — у кілька рядків, поле
 * росте разом із текстом; поки вибрано клітинку, знизу сторінки — смуга дій «Копіювати» й
 * «Очистити», `ActionBarService`) (з `CreateCustomRecord` у порожньому
 * рядку — новий запис, з `UpdateCustomRecord` — наявні записи). Після кожної зміни — 2 секунди тиші
 * (`DebouncedSaver`), тоді запити: опис і стовпчики — `PUT /api/v1/customtables/{id}` (поверх
 * свіжої структури), записи — `POST` (новий — на своєму місці) і `PATCH /api/v1/customrecords/{id}`
 * (лише змінені клітинки). Значення в записі — за `id` поля.
 * Тег у шапці: від зміни до відповіді — помаранчевий «Зберігаємо зміни» (`cloud-upload-outline`),
 * невдача — червоний «Зміни не збережено» (клік — повторити), потім знову «Зміни збережено».
 * Перед перезавантаженням записів (пошук, сортування), переходом на іншу таблицю й закриттям виду
 * незбережене надсилається одразу.
 *
 * Кожні 10 секунд (`POLL_INTERVAL_MS`; поки вкладка видима) — перевірка на зміни з інших пристроїв:
 * таблиця й записи заново, і показане оновлюється лише там, де щось змінилося (`mergeRecords`: решта
 * клітинок не перемальовується). Поки зміни чекають чи йдуть на сервер, перевірка пропускається;
 * відповідь, за час якої щось змінили, відкидається; рядки з незбереженими змінами, опис, який
 * зараз редагують, і клітинку з курсором вона не чіпає.
 */
@Component({
  selector: 'app-kb-table',
  templateUrl: './kb-table.component.html',
  styleUrls: ['../kb-view.scss', './kb-table.component.scss'],
  imports: [
    ActionButtonComponent,
    AutoSizeDirective,
    ColorPickerComponent,
    ConfirmDialogComponent,
    DataTableComponent,
    DialogHeaderComponent,
    IonIcon,
    KbItemFormComponent,
    ModalComponent,
    PopoverComponent,
    RichTextDirective,
    SearchHeaderComponent,
    TranslatePipe,
  ],
})
export class KbTableComponent {
  /** `id` таблиці з адреси. */
  readonly id = input.required<string>();

  private readonly tables = inject(CustomTablesService);
  private readonly recordsService = inject(CustomRecordsService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly translate = inject(TranslateService);
  private readonly entry = injectKnowledgeBaseEntry(this.id);
  private readonly templatesService = inject(TableTemplatesService);
  private readonly successNotice = inject(SuccessNoticeService);
  private readonly templateDialog = viewChild<ModalComponent>('templateDialog');
  /** Вікно «Створити шаблон»: запит іде; причина невдачі (ключ мовного файлу) чи `null`. */
  protected readonly templateBusy = signal(false);
  /** Вікно «Зміни не збережено» перед виходом із виду: відповідь чекає на вибір (`canLeave`). */
  private readonly leaveDialog = viewChild<ModalComponent>('leaveDialog');
  private leaveAnswer: ((leave: boolean) => void) | null = null;
  /** Іде експорт; причина невдачі експорту (ключ мовного файлу) чи `null`. */
  protected readonly exporting = signal(false);
  protected readonly exportError = signal<string | null>(null);
  protected readonly templateError = signal<string | null>(null);
  /** З чим відкривається вікно «Створити шаблон»: назва й опис таблиці. */
  protected readonly templateInitial = computed(() => {
    const state = this.state();
    return { title: this.title(), description: state.status === 'ready' ? state.table.description : null };
  });

  protected readonly state = signal<TableState>({ status: 'loading' });
  /** «Створити шаблон» — лише з правом створювати шаблони. */
  protected readonly canCreateTemplate = computed(() => this.currentUser.permissions().includes(PERMISSIONS.createTableTemplate));
  /** «Експортувати» — лише з правом бачити записи (експортуються вони). */
  protected readonly canExport = computed(() => this.currentUser.permissions().includes(PERMISSIONS.viewCustomRecord));
  private readonly records = signal<readonly LocalRecord[]>([]);
  /** Опис і клітинки можна змінювати (`UpdateCustomTable`). */
  protected readonly canEditTable = computed(() => this.currentUser.permissions().includes(PERMISSIONS.updateCustomTable));
  /** Порожній рядок стає новим записом — ще й з `CreateCustomRecord`. */
  private readonly canCreateRecords = computed(() => this.canEditTable() && this.currentUser.permissions().includes(PERMISSIONS.createCustomRecord));
  /** Наявні записи змінюються — ще й з `UpdateCustomRecord`. */
  private readonly canUpdateRecords = computed(() => this.canEditTable() && this.currentUser.permissions().includes(PERMISSIONS.updateCustomRecord));
  protected readonly descriptionMaxLength = CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH;
  /** Відкладене збереження змін; його стан показує тег у шапці. */
  protected readonly saver = new DebouncedSaver(() => this.saveChanges());
  private descriptionDirty = false;
  /** Ключі записів (`LocalRecord.key`), змінених після останнього збереження. */
  private readonly dirtyRecords = new Set<string>();
  /**
   * Які саме клітинки змінено тут (ключ запису → ключі полів): `PATCH` запису надсилає лише їх, тож
   * чужі зміни інших клітинок того самого рядка не затираються.
   */
  private readonly dirtyCells = new Map<string, Set<string>>();
  /**
   * Структура таблиці, якою її востаннє бачили на сервері: свої зміни стовпчиків — відносно неї
   * (`mergeFields`), щоб накласти їх на свіжу структуру, не видаливши чужих колонок.
   */
  private baseFields: readonly CustomTableField[] = [];
  /** Надіслана структура з новими полями, справжніх `id` яких ще не дізналися (див. `afterTableSaved`). */
  private unadoptedFields: readonly CustomTableField[] | null = null;
  /** Номер останнього запиту записів: відповідь на старіший (наприклад, на «фон» після «фонд») відкидається. */
  private recordsRequest = 0;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  /** Запис за ключем рядка: клітинки шукають свій запис тут, а не перебором усіх. */
  private readonly recordByKey = computed(() => new Map(this.records().map((record) => [record.key, record])));
  protected readonly cellMaxLength = CELL_HTML_MAX_LENGTH;
  protected readonly headMaxLength = HEAD_HTML_MAX_LENGTH;
  private readonly editableCell = viewChild<TemplateRef<DataTableCellContext>>('editableCell');
  private readonly editableHeader = viewChild<TemplateRef<DataTableHeaderContext>>('editableHeader');
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);
  /** Вибрана клітинка (з курсором): поки вона є, знизу смуга дій; перевірка на зміни її не чіпає. */
  protected readonly selectedCell = signal<SelectedCell | null>(null);
  private readonly actionBar = inject(ActionBarService);
  /** Видалити рядок — ще й з `DeleteCustomRecord`. */
  private readonly canDeleteRecords = computed(() => this.canEditTable() && this.currentUser.permissions().includes(PERMISSIONS.deleteCustomRecord));
  /**
   * Смуга дій для вибраної клітинки, чотири групи: форматування (колір тла, напівжирний, курсив,
   * підкреслення, закреслення — виділеного тексту, а в прапорці — усієї клітинки), рядки (додати
   * знизу, видалити), стовпчики (додати справа, зліва, видалити) і клітинка (вставити чи прибрати
   * прапорець, очистити, скопіювати текст). Недоступне за правами чи для цієї клітинки — вимкнене.
   */
  private readonly cellActionBar = computed<ActionBarContent<CellAction> | null>(() => {
    const cell = this.selectedCell();
    if (!cell) {
      return null;
    }
    const canEditTable = this.canEditTable();
    const isHeader = cell.key === null;
    // Заголовок — назва поля: змінюється з `UpdateCustomTable`, клітинка — за правами рядка.
    const canEditCell = isHeader ? canEditTable : this.canEditRow({ id: cell.key! });
    // Усе — рядки, форматувати можна будь-яку клітинку (і прапорець) та заголовок.
    const noFormatting = !canEditCell;
    const isCheckbox = !isHeader && checkboxState(this.valueOf(cell)) !== null;
    return {
      labelKey: 'knowledgeBase.table.cellActions.label',
      groups: [
        [
          cellAction({ id: 'background', icon: 'color-fill', disabled: noFormatting, swatch: splitBackground(this.valueOf(cell)).background }),
          // Заголовки й так напівжирні: «Напівжирний» там нічого б не додав.
          cellAction({ id: 'bold', icon: 'format-bold', disabled: noFormatting || isHeader }),
          cellAction({ id: 'italic', icon: 'format-italic', disabled: noFormatting }),
          cellAction({ id: 'underline', icon: 'format-underline', disabled: noFormatting }),
          cellAction({ id: 'strikethrough', icon: 'format-strikethrough', disabled: noFormatting }),
        ],
        [
          cellAction({ id: 'addRowBelow', icon: 'row-add-below', disabled: isHeader || !this.canCreateRecords() }),
          cellAction({ id: 'deleteRow', icon: 'row-remove', disabled: isHeader || !this.canDeleteRecords() }),
        ],
        [
          // Більше 50 полів сервер не прийме.
          cellAction({ id: 'addColumnRight', icon: 'column-add-right', disabled: !canEditTable || this.fields().length >= CUSTOM_TABLE_MAX_FIELDS }),
          cellAction({ id: 'addColumnLeft', icon: 'column-add-left', disabled: !canEditTable || this.fields().length >= CUSTOM_TABLE_MAX_FIELDS }),
          // Останній стовпчик не видалити: сервер вимагає хоча б одне поле.
          cellAction({ id: 'deleteColumn', icon: 'column-remove', disabled: !canEditTable || this.fields().length <= 1 }),
        ],
        [
          // Прапорець — у клітинці, не в заголовку (зберігається символом `☑` / `☐`).
          cellAction({
            id: 'checkbox',
            icon: isCheckbox ? 'checkbox-remove' : 'checkbox-outline',
            labelKey: `knowledgeBase.table.cellActions.${isCheckbox ? 'removeCheckbox' : 'insertCheckbox'}`,
            disabled: isHeader || !canEditCell,
          }),
          // Назву стовпчика не очистити: сервер вимагає непорожню.
          cellAction({ id: 'clear', icon: 'eraser-outline', disabled: isHeader || !canEditCell }),
          cellAction({ id: 'copy', icon: 'copy-outline', disabled: false }),
        ],
      ],
      choose: (action, button) => this.onCellAction(action, button),
    };
  });
  /** Вибір кольору тла (спливаюче вікно над кнопкою-кружком). */
  private readonly colorPopover = viewChild<PopoverComponent>('colorPopover');
  /** Клітинка чи заголовок, для якого відкрито вибір кольору. */
  protected readonly colorTarget = signal<SelectedCell | null>(null);
  /** Іде перевірка на зміни чи перезавантаження записів — тег «Оновлюються дані». */
  protected readonly syncing = signal(false);
  /** Що видалити після підтвердження (вікно «Видалити»). */
  protected readonly pendingDelete = signal<PendingDelete | null>(null);
  private readonly deleteDialog = viewChild<ModalComponent>('deleteDialog');
  /** Нові рядки (кнопка «Додати рядок знизу»): лічильник для їхніх ключів. */
  private newRowCount = 0;
  /** Нові стовпчики: лічильник для їхніх тимчасових `id` (`new:N`). */
  private newFieldCount = 0;
  /** Структуру (поля) змінено: піде з наступним збереженням (`PUT` таблиці). */
  private fieldsDirty = false;
  /** `id` записів на видалення (`DELETE` з наступним збереженням). */
  private readonly deletedRecords = new Set<string>();
  /** Росте з кожною зміною: відповідь перевірки, за час якої щось змінили, відкидається. */
  private changeGeneration = 0;
  private polling = false;
  /** Записи не завантажилися: ключ причини; `null` — усе гаразд. */
  protected readonly recordsError = signal<string | null>(null);
  private readonly sort = signal<CustomRecordsSort | null>(null);
  /** Пошук у записах (лупа в шапці); `null` — без пошуку. */
  private readonly searchTerm = signal<string | null>(null);
  protected readonly searchMaxLength = SEARCH_MAX_LENGTH;

  /** Назва — зі змісту (оновлюється після «Редагувати»), а до нього — з відповіді сервера. */
  protected readonly title = computed(() => {
    const state = this.state();
    return this.entry()?.title ?? (state.status === 'ready' ? state.table.name : '');
  });

  private readonly fields = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? [...state.table.fields].sort((a, b) => Number(a.order) - Number(b.order)) : [];
  });

  protected readonly columns = computed<readonly DataTableColumn[]>(() => {
    // Клітинки — свій шаблон: поля введення там, де можна змінювати (`canEditRow`), а текст із
    // форматуванням — з форматуванням і лише для перегляду.
    const template = this.editableCell();
    const headerTemplate = this.editableHeader();
    return this.fields().map((field) => {
      const background = splitBackground(field.name).background;
      return {
        id: FIELD_COLUMN_PREFIX + field.key,
        labelKey: null,
        // Підпис для кнопки сортування — лише текст назви (без форматування).
        label: richTextToPlain(field.name),
        width: COLUMN_WIDTH,
        orderBy: field.key,
        template,
        headerTemplate,
        headerColors: background ? colorsFor(background) : null,
        cellColors: (row: DataTableRow) => this.cellColors(row, field),
      };
    });
  });

  protected readonly rows = computed<readonly DataTableRow[]>(() => {
    const records = this.records();
    const fields = this.fields();
    if (!records.length) {
      // Нерозривний пробіл: порожній рядок такої самої висоти, як рядок із текстом.
      return [{ id: EMPTY_ROW_ID, ...Object.fromEntries(fields.map((field) => [FIELD_COLUMN_PREFIX + field.key, '\u00a0'])) }];
    }
    return records.map((record) => ({
      id: record.key,
      ...Object.fromEntries(fields.map((field) => [FIELD_COLUMN_PREFIX + field.key, richTextToPlain(splitBackground(this.textOf(record.data[field.key])).html)])),
    }));
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.sort.set(null);
        this.searchTerm.set(null);
        // Незбережене в попередній таблиці — спершу на сервер.
        void this.saver.flush().then(() => this.load(id));
      });
    });
    // Перевірка на зміни з інших пристроїв.
    const poll = setInterval(() => void this.poll(), POLL_INTERVAL_MS);
    // Смуга дій знизу — поки вибрано клітинку.
    effect(() => {
      const content = this.cellActionBar();
      if (content) {
        this.actionBar.show(this, content);
      } else {
        this.actionBar.hide(this);
      }
    });
    // Фокус пішов зі смуги дій кудись, крім клітинки, — клітинку вже не вибрано.
    const onFocusIn = (event: FocusEvent) => {
      const target = event.target as HTMLElement | null;
      // Смуга дій і вибір кольору (вікно Ionic може бути перенесене в кінець сторінки) — частина вибору.
      const keeps = target?.closest('app-action-bar, ion-popover') || target?.classList.contains('kb-cell') || target?.classList.contains('kb-head');
      if (this.selectedCell() && target && !keeps) {
        this.select(null);
      }
    };
    document.addEventListener('focusin', onFocusIn);
    // Вкладку сховали (перемкнули, згорнули чи закривають): незбережене — одразу, без затримки; поки
    // сторінка схована, запити йдуть із `keepalive` і не обриваються з її закриттям.
    // Повернулися на вкладку — одразу перевірка на зміни: схована вкладка сервер не опитує.
    const onVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        void this.saver.flush();
      } else {
        void this.poll();
      }
    };
    // Закривають сторінку, а зміни ще не збережено: браузер питає, чи залишити сайт.
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      if (this.saver.state() !== 'saved') {
        event.preventDefault();
      }
    };
    // Сторінку закривають: решту незбереженого — одразу (`sendRemainingOnClose`).
    const onPageHide = () => this.sendRemainingOnClose();
    document.addEventListener('visibilitychange', onVisibilityChange);
    window.addEventListener('beforeunload', onBeforeUnload);
    window.addEventListener('pagehide', onPageHide);
    // Закрили вид — перевірка зупиняється, смуга дій ховається, незбережене надсилається одразу
    // (сервіси живуть далі).
    this.destroyRef.onDestroy(() => {
      clearInterval(poll);
      document.removeEventListener('focusin', onFocusIn);
      document.removeEventListener('visibilitychange', onVisibilityChange);
      clearTimeout(this.searchTimer);
      window.removeEventListener('beforeunload', onBeforeUnload);
      window.removeEventListener('pagehide', onPageHide);
      this.actionBar.hide(this);
      void this.saver.flush();
    });
  }

  protected retry(): void {
    void this.load(this.id());
  }

  /**
   * Чи можна піти з виду (`canDeactivate` маршруту): незбережене — спершу на сервер; не вдалося —
   * вікно «Зміни не збережено»: «Залишитися» чи «Піти» (зміни пропадуть).
   */
  async canLeave(): Promise<boolean> {
    await this.saver.flush();
    if (this.saver.state() !== 'failed') {
      return true;
    }
    return new Promise<boolean>((resolve) => {
      this.leaveAnswer = resolve;
      this.leaveDialog()?.open();
    });
  }

  /** Відповідь у вікні «Зміни не збережено» (закрили без вибору — лишаємося). */
  protected answerLeave(leave: boolean): void {
    const answer = this.leaveAnswer;
    this.leaveAnswer = null;
    answer?.(leave);
    void this.leaveDialog()?.close();
  }

  /** «Створити шаблон»: вікно з назвою (спершу — назва таблиці) й описом шаблону. */
  protected createTemplate(): void {
    this.templateBusy.set(false);
    this.templateError.set(null);
    this.templateDialog()?.open();
  }

  /**
   * Надіслали назву й опис: шаблон зі стовпчиків таблиці (`POST /api/v1/tabletemplates`) — назви
   * заголовків як є (з форматуванням і кольором тла) у їхньому порядку. Успіх — вікно закривається,
   * угорі плашка «Шаблон створено…»; невдача — причина у вікні, воно лишається відкритим.
   */
  protected async submitTemplate(value: KbItemFormValue): Promise<void> {
    this.templateBusy.set(true);
    this.templateError.set(null);
    const newColumn = this.translate.instant('knowledgeBase.table.newColumn') as string;
    const result = await this.templatesService.create({
      name: value.title,
      description: value.description,
      // Порожньої назви сервер не прийме (поки людина ще пише в заголовку).
      fields: this.fields().map((field) => ({ name: richTextToPlain(field.name).trim() ? field.name : newColumn })),
    });
    this.templateBusy.set(false);
    if (typeof result === 'string') {
      this.templateError.set(`knowledgeBase.failure.${result}`);
      return;
    }
    void this.templateDialog()?.close();
    this.successNotice.show('knowledgeBase.table.templateDialog.done');
  }

  protected closeTemplateDialog(): void {
    void this.templateDialog()?.close();
  }

  /**
   * «Експортувати»: файл Excel `<назва таблиці>.xlsx` (див. `table-xlsx.ts`) — уся таблиця в
   * поточному порядку: незбережене спершу йде на сервер; поки відкрито пошук, записи — заново, без
   * нього. Заголовки й значення — з форматуванням і кольором тла, як у таблиці (прапорець — `☑` /
   * `☐`). Не вдалося отримати записи — повідомлення з «Спробувати ще раз».
   */
  protected async export(): Promise<void> {
    const state = this.state();
    if (state.status !== 'ready' || this.exporting()) {
      return;
    }
    this.exporting.set(true);
    this.exportError.set(null);
    try {
      await this.saver.flush();
      let records: readonly { readonly data: CustomRecordData }[] = this.records();
      if (this.searchTerm() !== null) {
        const all = await this.recordsService.listAll(state.table.id, this.sort(), null);
        if (typeof all === 'string') {
          this.exportError.set(`knowledgeBase.failure.${all}`);
          return;
        }
        records = all;
      }
      const fields = this.fields();
      const title = richTextToPlain(this.title());
      const fallback = this.translate.instant('knowledgeBase.table.exportFileName') as string;
      const file = toXlsx({
        sheetName: sheetName(title, fallback),
        headers: fields.map((field) => field.name),
        rows: records.map((record) => fields.map((field) => this.textOf(record.data[field.key]))),
      });
      downloadFile(xlsxFileName(title, fallback), file, XLSX_TYPE);
    } finally {
      this.exporting.set(false);
    }
  }

  protected retryRecords(): void {
    void this.reloadRecords();
  }

  /**
   * Текст у полі пошуку: записи заново, з пошуком за ним (як у «Колегах») — після паузи
   * `SEARCH_DELAY_MS` від останнього символу, а не на кожну літеру.
   */
  protected onSearch(value: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.searchTerm.set(value.trim() || null);
      void this.reloadRecords();
    }, SEARCH_DELAY_MS);
  }

  /** Клік по заголовку: записи заново, відсортовані за цим полем (чи без порядку — після скидання). */
  protected onSort(sort: DataTableSort | null): void {
    this.sort.set(sort?.column.orderBy ? { orderBy: sort.column.orderBy, sortOrder: sort.direction } : null);
    void this.reloadRecords();
  }

  /** Записи заново — після того, як незбережені зміни пішли на сервер (інакше вони б загубилися). */
  private async reloadRecords(): Promise<void> {
    // Перевірка на зміни, що вже йде, — зі старим пошуком чи порядком: її відповідь відкидається.
    this.changeGeneration++;
    await this.saver.flush();
    this.syncing.set(true);
    try {
      await this.loadRecords(this.id());
    } finally {
      this.syncing.set(false);
    }
  }

  /** Поле таблиці для колонки. */
  protected fieldOf(column: DataTableColumn): CustomTableField | undefined {
    return this.fields().find((field) => FIELD_COLUMN_PREFIX + field.key === column.id);
  }

  /** Чи можна змінювати рядок: порожній (новий запис) — з `CreateCustomRecord`, наявний — з `UpdateCustomRecord`. */
  protected canEditRow(row: Pick<DataTableRow, 'id'>): boolean {
    const record = this.recordByKey().get(String(row.id));
    return record && record.serverId !== null ? this.canUpdateRecords() : this.canCreateRecords();
  }

  /** Змінили текст із форматуванням (значення вже очищене: лише b, i, u, s, br); колір тла лишається. */
  protected onRichTextInput(row: DataTableRow, field: CustomTableField, html: string): void {
    const background = splitBackground(this.textValue(row, field)).background;
    this.setCell(String(row.id), field, joinBackground(background, html));
  }

  /**
   * Значення клітинки рядком: усе трактуємо як рядки (старі значення інших типів — їхнім текстом,
   * логічне — прапорцем `☑` / `☐`).
   */
  protected textValue(row: Pick<DataTableRow, 'id'>, field: CustomTableField): string {
    return this.textOf(this.recordByKey().get(String(row.id))?.data[field.key]);
  }

  private textOf(value: unknown): string {
    if (value === null || value === undefined) {
      return EMPTY_VALUE;
    }
    if (typeof value === 'boolean') {
      return value ? CHECKBOX_ON : CHECKBOX_OFF;
    }
    return String(value);
  }

  /**
   * Текст значення з форматуванням (без тегу кольору тла), очищений: лише `b`, `i`, `u`, `s`, `br`
   * без атрибутів — для редагованого блоку й `[innerHTML]` перегляду (значення з сервера могли
   * записати в обхід застосунку, а санітайзер Angular пропускає, наприклад, `img` і `a href`).
   */
  protected htmlOf(value: string | null | undefined): string {
    return sanitizeRichText(splitBackground(value).html);
  }

  /** Змінили назву стовпчика (у заголовку, з форматуванням); колір тла назви лишається. */
  protected onHeaderInput(field: CustomTableField, html: string): void {
    const background = splitBackground(field.name).background;
    this.renameField(field.key, joinBackground(background, html));
  }

  /** Вийшли із заголовка з порожньою назвою — «Новий стовпчик» (сервер порожньої не прийме). */
  protected onHeaderBlur(field: CustomTableField, event: FocusEvent): void {
    const current = this.fields().find((candidate) => candidate.key === field.key);
    if (current && !richTextToPlain(current.name).trim()) {
      this.renameField(field.key, joinBackground(splitBackground(current.name).background, this.translate.instant('knowledgeBase.table.newColumn') as string));
    }
    this.onCellBlur(event);
  }

  protected onHeaderFocus(field: CustomTableField, event: FocusEvent): void {
    this.select({ key: null, field, element: event.target as HTMLElement });
  }

  private renameField(key: string, name: string): void {
    this.setFields(this.fields().map((candidate) => (candidate.key === key ? { ...candidate, name } : candidate)));
  }

  /** Вибрали колір тла (чи «Без кольору») у спливаючому вікні: для клітинки чи заголовка. */
  protected onColorPick(color: string | null): void {
    const target = this.colorTarget();
    void this.colorPopover()?.close();
    if (!target) {
      return;
    }
    if (target.key === null) {
      const current = this.fields().find((candidate) => candidate.key === target.field.key);
      if (current) {
        this.renameField(current.key, joinBackground(color, splitBackground(current.name).html));
      }
    } else {
      this.setCell(target.key, target.field, joinBackground(color, splitBackground(this.valueOf(target)).html));
    }
    target.element.focus();
  }

  /** Колір тла клітинки чи заголовка (`null` — нема). */
  protected colorOf(cell: SelectedCell): string | null {
    return splitBackground(this.valueOf(cell)).background;
  }

  /** Значення вибраної клітинки чи назва вибраного стовпчика (рядком). */
  private valueOf(cell: SelectedCell): string {
    if (cell.key === null) {
      return this.fields().find((field) => field.key === cell.field.key)?.name ?? cell.field.name;
    }
    return this.textValue({ id: cell.key }, cell.field);
  }

  /** Кольори всієї клітинки: тло з тегу в значенні, текст — з контрастом 7:1. */
  private cellColors(row: DataTableRow, field: CustomTableField): DataTableCellColors | null {
    const background = splitBackground(this.textValue(row, field)).background;
    return background ? colorsFor(background) : null;
  }

  /**
   * Натиснули в клітинці поза її полем (на відступах): клітинку вибрано так само. У клітинці з
   * текстом (чи порожній) — курсор у поле, у кінець тексту; у клітинці з прапорцем — лише вибір
   * (смуга дій: форматування, «Прибрати прапорець»), а перемикає прапорець тільки клік по ньому.
   * Типова дія натискання скасована: фокус не йде на сторінку, тож смуга дій не блимає.
   */
  protected onTableMouseDown(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (target.closest('.kb-cell, .kb-head')) {
      return;
    }
    const cell = target.closest('td')?.querySelector<HTMLElement>('.kb-cell');
    if (!cell) {
      return;
    }
    event.preventDefault();
    cell.focus();
    if (cell.getAttribute('contenteditable') === 'true') {
      const range = document.createRange();
      range.selectNodeContents(cell);
      range.collapse(false);
      const selection = document.getSelection();
      selection?.removeAllRanges();
      selection?.addRange(range);
    }
  }

  /** Клік по самому прапорцю: Safari фокус від кліку прапорцю не дає — даємо його, щоб клітинку вибрали. */
  protected onTableClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (target.classList.contains('kb-cell--check') && document.activeElement !== target) {
      target.focus();
    }
  }

  protected onCellFocus(row: DataTableRow, field: CustomTableField, event: FocusEvent): void {
    this.select({ key: String(row.id), field, element: event.target as HTMLElement });
  }

  /** Фокус пішов із клітинки: її вже не вибрано, якщо він не перейшов на смугу дій чи вибір кольору. */
  protected onCellBlur(event: FocusEvent): void {
    const next = event.relatedTarget as HTMLElement | null;
    if (next?.closest('app-action-bar, ion-popover')) {
      return;
    }
    this.select(null);
  }

  /** Вибір клітинки (чи заголовка; `null` — нічого): поки вона є, знизу смуга дій. */
  private select(cell: SelectedCell | null): void {
    this.selectedCell.set(cell);
  }

  /** Клітинка з прапорцем: увімкнули чи вимкнули. */
  protected onCheckboxCellChange(row: DataTableRow, field: CustomTableField, event: Event): void {
    this.setCell(String(row.id), field, setCheckbox(this.textValue(row, field), (event.target as HTMLInputElement).checked));
  }

  /** Клітинка — прапорець (`☑` / `☐`). */
  protected isCheckboxCell(row: DataTableRow, field: CustomTableField): boolean {
    return checkboxState(this.textValue(row, field)) !== null;
  }

  protected isCheckboxCellChecked(row: DataTableRow, field: CustomTableField): boolean {
    return checkboxState(this.textValue(row, field)) === true;
  }

  /** Вставити прапорець у клітинку (текст, що не стане прапорцем, зникне — тоді спершу питаємо). */
  private insertCheckbox(key: string, field: CustomTableField, confirmed = false): void {
    const text = this.textValue({ id: key }, field);
    if (!confirmed && checkboxLosesText(text)) {
      this.askDelete({ kind: 'checkbox', key, field });
      return;
    }
    this.setCell(key, field, toCheckbox(text));
    this.focusCellAfterRender(key, field.key);
  }

  /**
   * Курсор — у клітинку `key` / `fieldKey` після перемальовування (поле введення в ній змінилося:
   * прапорець замість тексту чи навпаки).
   */
  private focusCellAfterRender(key: string, fieldKey: string): void {
    afterNextRender(
      () => {
        const row = this.rows().findIndex((candidate) => candidate.id === key);
        const column = this.fields().findIndex((field) => field.key === fieldKey);
        this.host.querySelectorAll('tbody tr')[row]?.querySelectorAll('td')[column]?.querySelector<HTMLElement>('.kb-cell')?.focus();
      },
      { injector: this.injector },
    );
  }

  /** Кнопка смуги дій для вибраної клітинки. */
  protected onCellAction(action: CellAction, button?: HTMLElement): void {
    const cell = this.selectedCell();
    if (!cell) {
      return;
    }
    switch (action) {
      case 'copy': {
        const text = this.valueOf(cell);
        void navigator.clipboard?.writeText(richTextToPlain(text));
        break;
      }
      case 'clear': {
        const field = cell.field;
        if (cell.key !== null) {
          const wasCheckbox = checkboxState(this.textValue({ id: cell.key }, field)) !== null;
          this.setCell(cell.key, field, EMPTY_VALUE);
          if (wasCheckbox) {
            // Замість прапорця — знову поле для тексту.
            this.focusCellAfterRender(cell.key, field.key);
            return;
          }
        }
        break;
      }
      case 'checkbox':
        if (cell.key !== null) {
          if (checkboxState(this.textValue({ id: cell.key }, cell.field)) !== null) {
            this.setCell(cell.key, cell.field, EMPTY_VALUE);
            this.focusCellAfterRender(cell.key, cell.field.key);
          } else {
            this.insertCheckbox(cell.key, cell.field);
          }
        }
        return;
      case 'background':
        this.colorTarget.set(cell);
        if (button) {
          this.colorPopover()?.open(button);
        }
        return;
      case 'addRowBelow':
        if (cell.key !== null) {
          this.addRowBelow(cell.key);
        }
        return;
      case 'addColumnRight':
      case 'addColumnLeft':
        this.addColumn(cell.field, action === 'addColumnRight' ? 'right' : 'left');
        break;
      case 'deleteRow':
        if (cell.key !== null) {
          this.askDelete({ kind: 'row', key: cell.key });
        }
        return;
      case 'deleteColumn':
        this.askDelete({ kind: 'column', field: cell.field });
        return;
      case 'bold':
      case 'italic':
      case 'underline':
      case 'strikethrough':
        if (cell.key !== null && checkboxState(this.valueOf(cell)) !== null) {
          // Прапорець: виділяти в ньому нічого, тож форматування — на всю клітинку.
          this.setCell(cell.key, cell.field, toggleCheckboxFormat(this.valueOf(cell), action));
          break;
        }
        // Курсор лишився в клітинці (кнопки смуги не забирають фокус): форматується виділене,
        // зміна приходить подією `input` клітинки (`onRichTextInput`).
        formatSelection(action satisfies RichTextFormat);
        return;
    }
    cell.element.focus();
  }

  /**
   * Новий порожній рядок під рядком `key` (з `CreateCustomRecord`). Запис створюється з наступним
   * збереженням (`POST`); курсор — у першу клітинку нового рядка. Сервер порядку рядків не
   * зберігає: після перезавантаження новий рядок — у кінці.
   */
  private addRowBelow(key: string): void {
    const newKey = `new-row-${++this.newRowCount}`;
    // Порожній рядок під заголовками, ще не записаний: спершу він сам стає записом.
    const placeholder = !this.records().some((record) => record.key === key);
    this.records.update((records) => {
      const base = placeholder ? [...records, { key, serverId: null, data: this.emptyData() }] : records;
      const index = base.findIndex((record) => record.key === key);
      return [...base.slice(0, index + 1), { key: newKey, serverId: null, data: this.emptyData() }, ...base.slice(index + 1)];
    });
    if (placeholder) {
      this.dirtyRecords.add(key);
    }
    this.dirtyRecords.add(newKey);
    this.changeGeneration++;
    this.saver.changed();
    const index = this.records().findIndex((record) => record.key === newKey);
    afterNextRender(() => this.host.querySelectorAll<HTMLElement>('tbody tr')[index]?.querySelector<HTMLElement>('.kb-cell')?.focus(), { injector: this.injector });
  }

  /**
   * Новий текстовий стовпчик праворуч чи ліворуч від поля `field` (з `UpdateCustomTable`): одразу в
   * таблиці, на сервер — з наступним збереженням (`PUT` таблиці з усією структурою; `id` нового поля
   * — `null`). До того поле має тимчасовий `id`, він же ключ значень у записах (`new:N`); після
   * збереження — справжній `id` з сервера (`adoptFieldIds`).
   */
  private addColumn(field: CustomTableField, side: 'left' | 'right'): void {
    const fields = this.fields();
    const index = fields.findIndex((candidate) => candidate.key === field.key) + (side === 'right' ? 1 : 0);
    const key = `${NEW_FIELD_PREFIX}${++this.newFieldCount}`;
    const added: CustomTableField = {
      id: key,
      name: this.translate.instant('knowledgeBase.table.newColumn') as string,
      key,
      // Обовʼязковість сервер ще вимагає: необовʼязкове.
      isRequired: false,
      order: 0,
    };
    this.setFields([...fields.slice(0, index), added, ...fields.slice(index)]);
  }

  private askDelete(pending: PendingDelete): void {
    this.pendingDelete.set(pending);
    this.deleteDialog()?.open();
  }

  protected cancelDelete(): void {
    void this.deleteDialog()?.close();
  }

  /** Підтвердили видалення: рядок чи стовпчик зникає одразу, на сервер — з наступним збереженням. */
  protected confirmDelete(): void {
    const pending = this.pendingDelete();
    void this.deleteDialog()?.close();
    if (!pending) {
      return;
    }
    if (pending.kind === 'checkbox') {
      this.insertCheckbox(pending.key, pending.field, true);
      return;
    }
    if (pending.kind === 'row') {
      const record = this.records().find((candidate) => candidate.key === pending.key);
      this.records.update((records) => records.filter((candidate) => candidate.key !== pending.key));
      this.dirtyRecords.delete(pending.key);
      this.dirtyCells.delete(pending.key);
      if (record?.serverId) {
        this.deletedRecords.add(record.serverId);
      }
    } else {
      this.setFields(this.fields().filter((candidate) => candidate.key !== pending.field.key));
      return;
    }
    this.changeGeneration++;
    this.saver.changed();
  }

  /** Нова структура: поля в цьому порядку (позиції — 0, 1, 2…), на сервер — з наступним збереженням. */
  private setFields(fields: readonly CustomTableField[]): void {
    const state = this.state();
    if (state.status !== 'ready') {
      return;
    }
    this.state.set({ status: 'ready', table: { ...state.table, fields: fields.map((field, order) => ({ ...field, order })) } });
    this.fieldsDirty = true;
    this.changeGeneration++;
    this.saver.changed();
  }

  /**
   * Перевірка на зміни з інших пристроїв: таблиця й записи (з тим самим пошуком і сортуванням)
   * заново; показане оновлюється лише там, де змінилося. Пропускається, поки вид не готовий, вкладку
   * сховано, зміни чекають чи йдуть на сервер або попередня перевірка ще триває.
   */
  private async poll(): Promise<void> {
    const state = this.state();
    if (state.status !== 'ready' || this.polling || this.saver.state() === 'saving' || document.hidden) {
      return;
    }
    this.polling = true;
    this.syncing.set(true);
    const id = state.table.id;
    const generation = this.changeGeneration;
    const canViewRecords = this.currentUser.permissions().includes(PERMISSIONS.viewCustomRecord);
    try {
      const [table, records] = await Promise.all([
        this.tables.get(id),
        canViewRecords ? this.recordsService.listAll(id, this.sort(), this.searchTerm()) : Promise.resolve(null),
      ]);
      // Поки йшла перевірка, щось змінили (чи відкрили іншу таблицю) — відповідь уже застаріла.
      if (generation !== this.changeGeneration || id !== this.id() || this.saver.state() === 'saving') {
        return;
      }
      if (table === 'gone') {
        this.tableGone();
        return;
      }
      if (typeof table !== 'string') {
        this.mergeTable(table);
      }
      if (records && typeof records !== 'string') {
        const selected = this.selectedCell();
        const focused = selected?.key ? { key: selected.key, field: selected.field.key } : null;
        this.records.update((local) => mergeRecords(local, records, { dirtyKeys: this.dirtyRecords, focused }));
      }
    } finally {
      this.polling = false;
      this.syncing.set(false);
    }
  }

  /** Свіжа таблиця з сервера: опис (якщо його зараз не редагують) і стовпчики, якщо змінилися. */
  private mergeTable(fresh: CustomTableDetails): void {
    const state = this.state();
    if (state.status !== 'ready') {
      return;
    }
    const editingDescription = this.descriptionDirty || (document.activeElement as HTMLElement | null)?.classList.contains('kb-table__description') === true;
    const description = editingDescription ? state.table.description : fresh.description;
    // Свої незбережені зміни стовпчиків лишаються (вони накладуться на свіжу структуру при збереженні).
    const fieldsChanged = !this.fieldsDirty && JSON.stringify(fresh.fields) !== JSON.stringify(state.table.fields);
    if (!this.fieldsDirty) {
      this.baseFields = fresh.fields;
    }
    if (description === state.table.description && !fieldsChanged) {
      return;
    }
    this.state.set({ status: 'ready', table: { ...state.table, description, fields: fieldsChanged ? fresh.fields : state.table.fields } });
  }

  /** Змінили опис (по кліку на нього). Порожній надсилається як `null`. */
  protected onDescriptionInput(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    const state = this.state();
    if (state.status !== 'ready') {
      return;
    }
    this.state.set({ status: 'ready', table: { ...state.table, description: textarea.value } });
    this.descriptionDirty = true;
    this.changeGeneration++;
    this.saver.changed();
  }

  private setCell(key: string, field: CustomTableField, value: unknown): void {
    this.records.update((records) => {
      if (records.some((record) => record.key === key)) {
        return records.map((record) => (record.key === key ? { ...record, data: { ...record.data, [field.key]: value } } : record));
      }
      // Порожній рядок: перший введений символ робить його новим записом (ще без `id` сервера).
      return [...records, { key, serverId: null, data: { ...this.emptyData(), [field.key]: value } }];
    });
    this.dirtyRecords.add(key);
    this.markCell(key, field.key);
    this.changeGeneration++;
    this.saver.changed();
  }

  private markCell(key: string, fieldKey: string): void {
    const cells = this.dirtyCells.get(key) ?? new Set<string>();
    cells.add(fieldKey);
    this.dirtyCells.set(key, cells);
  }

  /** Дані нового запису: усі поля — порожні рядки. */
  private emptyData(): CustomRecordData {
    return Object.fromEntries(this.fields().map((field) => [field.key, EMPTY_VALUE]));
  }

  /**
   * Надсилає незбережене по черзі. Чужих змін, зроблених тим часом, не затирає:
   * - таблиця (опис і структура): свіжа з `GET`, на неї — лише свої зміни (`mergeFields`: видалені,
   *   перейменовані й нові стовпчики; опис — якщо змінювали тут), тоді `PUT`; після нього —
   *   справжні `id` нових полів;
   * - видалені записи — `DELETE`;
   * - нові записи — `POST` між своїми сусідами (`placeOf`); змінені — `PATCH` лише змінених тут
   *   клітинок.
   * Таблицю чи запис уже видалили (404) — повторювати нема чого: таблиця — повідомлення, запис —
   * рядок зникає. Інша невдача лишається незбереженою і піде з наступною зміною (чи повтором з
   * тегу). Повертає, чи вдалося все.
   */
  private async saveChanges(): Promise<boolean> {
    const state = this.state();
    if (state.status !== 'ready') {
      return true;
    }
    let ok = true;
    if (this.descriptionDirty || this.fieldsDirty) {
      const saved = await this.saveTable(state.table.id);
      if (saved === 'gone') {
        this.tableGone();
        return true;
      }
      ok = saved;
    }
    for (const id of [...this.deletedRecords]) {
      this.deletedRecords.delete(id);
      const result = await this.recordsService.remove(id);
      // Уже видалений деінде (404) — те саме, що видалили.
      if (result !== 'done' && result !== 'gone') {
        this.deletedRecords.add(id);
        ok = false;
      }
    }
    for (const key of [...this.dirtyRecords]) {
      // З черги — щойно перед запитом: якщо сторінку закриють посеред збереження, решту черги
      // надішле `sendRemainingOnClose` (а вже надіслане чи надіслане ним тут пропускається).
      const record = this.recordByKey().get(key);
      if (!this.dirtyRecords.delete(key) || !record) {
        continue;
      }
      const cells = this.dirtyCells.get(key) ?? new Set<string>();
      this.dirtyCells.delete(key);
      if (record.serverId === null) {
        const created = await this.recordsService.create(state.table.id, this.recordData(record), this.placeOf(key));
        if (typeof created === 'string' || !created.id) {
          this.requeue(key, cells);
          ok = false;
          continue;
        }
        this.records.update((records) => records.map((candidate) => (candidate.key === key ? { ...candidate, serverId: created.id } : candidate)));
      } else if (!(await this.saveRecord(key, record.serverId, cells))) {
        ok = false;
      }
    }
    return ok;
  }

  /**
   * Таблиця: свіжа структура з сервера й на неї — свої зміни (див. `saveChanges`). `gone` — таблицю
   * видалили; `false` — не вдалося (зміни лишаються незбереженими).
   */
  private async saveTable(id: string): Promise<boolean | 'gone'> {
    const fieldsChanged = this.fieldsDirty;
    const descriptionChanged = this.descriptionDirty;
    this.fieldsDirty = false;
    this.descriptionDirty = false;
    const restore = () => {
      this.fieldsDirty ||= fieldsChanged;
      this.descriptionDirty ||= descriptionChanged;
    };
    const fresh = await this.tables.get(id);
    if (fresh === 'gone') {
      return 'gone';
    }
    const local = this.currentTable();
    if (typeof fresh === 'string' || !local) {
      restore();
      return false;
    }
    // Попереднє збереження не дізналося справжніх `id` своїх нових полів — спершу вони.
    if (this.unadoptedFields) {
      this.adoptFieldIds(this.unadoptedFields, fresh.fields);
      this.unadoptedFields = null;
      this.baseFields = fresh.fields;
    }
    const merged = fieldsChanged ? mergeFields(this.baseFields, this.fields(), fresh.fields) : fresh.fields;
    const result = await this.tables.update(this.tableUpdate({ ...fresh, description: descriptionChanged ? local.description : fresh.description, fields: merged }));
    if (result === 'gone') {
      return 'gone';
    }
    if (result !== 'done') {
      restore();
      return false;
    }
    await this.afterTableSaved(id, merged);
    return true;
  }

  /**
   * Запис: лише змінені тут клітинки (`cells`) — `PATCH` (решту запису сервер лишає як є, тож чужі
   * зміни інших клітинок того самого рядка не затираються). Клітинки нових стовпчиків, яких на
   * сервері ще нема, лишаються незбереженими до збереження структури. Запис уже видалили — рядок
   * зникає. Повертає, чи вдалося.
   */
  private async saveRecord(key: string, serverId: string, cells: ReadonlySet<string>): Promise<boolean> {
    const saved = new Set(this.fields().filter((field) => !isNewField(field)).map((field) => field.key));
    const sending = [...cells].filter((field) => saved.has(field));
    const waiting = [...cells].filter((field) => !saved.has(field) && this.fields().some((candidate) => candidate.key === field));
    if (waiting.length) {
      this.requeue(key, new Set(waiting));
    }
    if (!sending.length) {
      return !waiting.length;
    }
    const own = this.recordByKey().get(key)?.data ?? {};
    const result = await this.recordsService.update(serverId, Object.fromEntries(sending.map((field) => [field, own[field] ?? null])));
    if (result === 'gone') {
      this.dropRecord(key);
      return true;
    }
    if (result !== 'done') {
      this.requeue(key, new Set(sending));
      return false;
    }
    return !waiting.length;
  }

  /** Незбережене — назад у чергу (клітинки, змінені тим часом, теж лишаються). */
  private requeue(key: string, cells: ReadonlySet<string>): void {
    this.dirtyRecords.add(key);
    for (const field of cells) {
      this.markCell(key, field);
    }
  }

  /** Запис уже видалили деінде: рядок зникає, його зміни — теж (повторювати нема чого). */
  private dropRecord(key: string): void {
    this.records.update((records) => records.filter((record) => record.key !== key));
    this.dirtyRecords.delete(key);
    this.dirtyCells.delete(key);
    if (this.selectedCell()?.key === key) {
      this.select(null);
    }
  }

  /** Таблицю вже видалили деінде: замість неї — повідомлення, незбережене відкидається. */
  private tableGone(): void {
    this.descriptionDirty = false;
    this.fieldsDirty = false;
    this.dirtyRecords.clear();
    this.dirtyCells.clear();
    this.deletedRecords.clear();
    this.select(null);
    this.state.set({ status: 'failed', errorKey: 'knowledgeBase.failure.gone' });
  }

  /**
   * Нові стовпчики отримали справжні `id` на сервері: тимчасовий `id` (`new:N`) нового поля з
   * надісланого (`sent`) — справжній із `fresh`. Зіставляються за позицією (сервер зберігає порядок,
   * який надіслали); якщо тим часом структуру змінив ще хтось і позиції не збігаються — за назвою
   * серед полів, яких у надісланому не було. Новий `id` — у структурі, у ключах даних записів, у
   * змінених клітинках, у вибраній клітинці й у сортуванні.
   */
  private adoptFieldIds(sent: readonly CustomTableField[], fresh: readonly CustomTableField[]): void {
    const ordered = [...fresh].sort((a, b) => Number(a.order) - Number(b.order));
    const known = new Set(sent.filter((field) => !isNewField(field)).map((field) => field.id));
    const samePlaces = ordered.length === sent.length && sent.every((field, index) => isNewField(field) || ordered[index].id === field.id);
    const unclaimed = ordered.filter((field) => !known.has(field.id));
    const ids = new Map<string, string>();
    sent.forEach((field, index) => {
      if (!isNewField(field)) {
        return;
      }
      const match = samePlaces ? ordered[index] : unclaimed.find((candidate) => candidate.name === field.name);
      if (match) {
        ids.set(field.key, match.id);
        unclaimed.splice(unclaimed.indexOf(match), 1);
      }
    });
    if (!ids.size) {
      return;
    }
    const renamed = (key: string) => ids.get(key) ?? key;
    const state = this.state();
    if (state.status === 'ready') {
      this.state.set({
        status: 'ready',
        table: { ...state.table, fields: state.table.fields.map((field) => (ids.has(field.key) ? { ...field, id: renamed(field.key), key: renamed(field.key) } : field)) },
      });
    }
    this.records.update((records) =>
      records.map((record) => ({ ...record, data: Object.fromEntries(Object.entries(record.data).map(([key, value]) => [renamed(key), value])) })),
    );
    for (const [record, cells] of this.dirtyCells) {
      this.dirtyCells.set(record, new Set([...cells].map(renamed)));
    }
    const selected = this.selectedCell();
    if (selected && ids.has(selected.field.key)) {
      const key = renamed(selected.field.key);
      this.selectedCell.set({ ...selected, field: { ...selected.field, id: key, key } });
    }
    const sort = this.sort();
    if (sort && ids.has(sort.orderBy)) {
      this.sort.set({ ...sort, orderBy: renamed(sort.orderBy) });
    }
  }

  /**
   * Після збереження структури: справжні `id` нових полів (`adoptFieldIds`; без них наступний `PUT`
   * створив би їх знову) і структура сервера як основа для наступних змін. Стовпчики тут тим часом
   * не змінювали — показується структура сервера (з чужими змінами); змінювали — лишаються свої.
   * Свіжої структури отримати не вдалося — `id` нових полів дізнаємося перед наступним збереженням.
   */
  private async afterTableSaved(id: string, sent: readonly CustomTableField[]): Promise<void> {
    const fresh = await this.tables.get(id);
    if (typeof fresh === 'string' || this.state().status !== 'ready') {
      this.unadoptedFields = sent.some(isNewField) ? sent : null;
      this.baseFields = sent;
      return;
    }
    this.adoptFieldIds(sent, fresh.fields);
    this.baseFields = fresh.fields;
    const current = this.state();
    if (current.status === 'ready' && !this.fieldsDirty) {
      this.state.set({ status: 'ready', table: { ...current.table, fields: fresh.fields } });
    }
  }

  /**
   * Сторінку закривають (`pagehide`): записи, які ще не пішли, — одразу й паралельно, а не по черзі
   * (після закриття наступного запиту в черзі вже ніхто не надіслав би). Сторінка вже схована, тож
   * запити йдуть із `keepalive` (`writeRequestOptions`) і доходять до сервера й після закриття.
   * Браузер дає таким запитам разом до 64 КБ — надсилаємо, доки вміщається (`CLOSE_BUDGET_BYTES`).
   * Свіжого запису чи таблиці тут уже не запитати, тож:
   * - структура й опис таблиці при закритті не надсилаються: зі старим списком полів `PUT` міг би
   *   видалити чужі колонки (браузер і так спитав, чи залишити сайт, — `beforeunload`);
   * - змінений запис — лише змінені клітинки (`PATCH`, як і звичайне збереження); новий — на своєму
   *   місці (`placeOf`).
   * Якщо сторінку не закрили, а відновили з кешу браузера, `id` нових записів приходять як завжди.
   */
  private sendRemainingOnClose(): void {
    const state = this.state();
    if (state.status !== 'ready') {
      return;
    }
    let budget = CLOSE_BUDGET_BYTES;
    const fits = (body: unknown) => {
      budget -= JSON.stringify(body).length * 2;
      return budget >= 0;
    };
    for (const id of [...this.deletedRecords]) {
      if (!fits(id)) {
        return;
      }
      this.deletedRecords.delete(id);
      void this.recordsService.remove(id);
    }
    for (const key of [...this.dirtyRecords]) {
      const record = this.recordByKey().get(key);
      if (!record) {
        continue;
      }
      const saved = new Set(this.fields().filter((field) => !isNewField(field)).map((field) => field.key));
      const cells = [...(this.dirtyCells.get(key) ?? [])].filter((field) => saved.has(field));
      const data = record.serverId === null ? this.recordData(record) : Object.fromEntries(cells.map((field) => [field, record.data[field] ?? null]));
      if (!fits(data)) {
        return;
      }
      this.dirtyRecords.delete(key);
      this.dirtyCells.delete(key);
      if (record.serverId === null) {
        void this.recordsService.create(state.table.id, data, this.placeOf(key)).then((created) => {
          if (typeof created !== 'string' && created.id) {
            this.records.update((records) => records.map((candidate) => (candidate.key === key ? { ...candidate, serverId: created.id } : candidate)));
          }
        });
      } else if (cells.length) {
        void this.recordsService.update(record.serverId, data);
      }
    }
  }

  /** Таблиця для `PUT`: назва й опис як у `table` і всі поля (нові — з `id: null`). */
  private tableUpdate(table: CustomTableDetails): CustomTableUpdate {
    return {
      id: table.id,
      name: table.name,
      description: table.description?.trim() || null,
      fields: table.fields.map((field) => ({
        ...field,
        id: isNewField(field) ? null : field.id,
        // Порожньої назви сервер не прийме (поки людина ще пише в заголовку).
        name: richTextToPlain(field.name).trim() ? field.name : (this.translate.instant('knowledgeBase.table.newColumn') as string),
      })),
    };
  }

  /**
   * Дані нового запису для сервера — лише поля, що вже є на сервері (значень видаленого чи ще не
   * збереженого стовпчика сервер не прийняв би).
   */
  private recordData(record: LocalRecord): CustomRecordData {
    return Object.fromEntries(this.fields().filter((field) => !isNewField(field)).map((field) => [field.key, record.data[field.key] ?? null]));
  }

  /**
   * Місце нового запису на сервері — між найближчими збереженими сусідами в показаному порядку
   * (новий рядок під вибраним лишається там і після перезавантаження).
   */
  private placeOf(key: string): CustomRecordPlace {
    const records = this.records();
    const index = records.findIndex((record) => record.key === key);
    const previous = records.slice(0, Math.max(index, 0)).reverse().find((record) => record.serverId !== null);
    const next = index < 0 ? undefined : records.slice(index + 1).find((record) => record.serverId !== null);
    return { previousRecordId: previous?.serverId ?? null, nextRecordId: next?.serverId ?? null };
  }

  private currentTable(): CustomTableDetails | null {
    const state = this.state();
    return state.status === 'ready' ? state.table : null;
  }

  private async load(id: string): Promise<void> {
    this.state.set({ status: 'loading' });
    this.records.set([]);
    this.recordsError.set(null);
    const table = await this.tables.get(id);
    if (id !== this.id()) {
      return;
    }
    if (typeof table === 'string') {
      this.state.set({ status: 'failed', errorKey: `knowledgeBase.failure.${table}` });
      return;
    }
    this.baseFields = table.fields;
    this.state.set({ status: 'ready', table });
    await this.loadRecords(id);
  }

  private async loadRecords(id: string): Promise<void> {
    if (!this.currentUser.permissions().includes(PERMISSIONS.viewCustomRecord)) {
      return;
    }
    this.recordsError.set(null);
    const request = ++this.recordsRequest;
    const records = await this.recordsService.listAll(id, this.sort(), this.searchTerm());
    // Тим часом відкрили іншу таблицю чи запитали записи з іншим пошуком — ця відповідь застаріла.
    if (id !== this.id() || request !== this.recordsRequest) {
      return;
    }
    if (typeof records === 'string') {
      this.recordsError.set(`knowledgeBase.failure.${records}`);
    } else {
      this.records.set(records.map((record) => ({ key: record.id, serverId: record.id, data: record.data })));
    }
  }
}

/** Кольори комірки з тлом `background`: текст — того самого відтінку з контрастом 7:1. */
function colorsFor(background: string): DataTableCellColors {
  return { background, color: cellTextColor(background) };
}
