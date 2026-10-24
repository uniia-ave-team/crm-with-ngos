import { Component, TemplateRef, input, output } from '@angular/core';

/** Рядок таблиці: `id` обовʼязковий, решта полів — довільні (за ними колонки беруть значення). */
export interface DataTableRow {
  readonly id: string;
  readonly [field: string]: unknown;
}

/** Що отримує шаблон комірки: рядок (`let-row`) і колонка (`let-column="column"`). */
export interface DataTableCellContext {
  readonly $implicit: DataTableRow;
  readonly column: DataTableColumn;
}

/** Подія кнопки з трьома крапками: рядок і сама кнопка (до неї можна прив'язати спливаюче вікно). */
export interface DataTableRowMenuEvent {
  readonly row: DataTableRow;
  readonly anchor: HTMLElement;
}

/** Опис колонки. */
export interface DataTableColumn {
  /** Ідентифікатор колонки; за ним у рядку береться значення для комірки за замовчуванням. */
  readonly id: string;
  /** Ключ заголовка в мовному файлі; `null` — заголовка нема (наприклад, колонка з аватаром). */
  readonly labelKey: string | null;
  /** Ширина колонки (будь-яке значення CSS `width`); без неї колонки ділять решту порівну. */
  readonly width?: string;
  /** Свій вигляд комірки; без нього показується значення `row[id]` текстом. */
  readonly template?: TemplateRef<DataTableCellContext>;
}

/**
 * Таблиця з даних: колонки й рядки задаються кодом, тож і те, й інше можна міняти
 * без змін у розмітці.
 *
 *   <app-data-table [columns]="columns()" [rows]="rows()" labelKey="colleagues.title" (rowMenu)="open($event)" />
 *
 * Комірка за замовчуванням — текст `row[column.id]`; щоб намалювати інакше (аватар,
 * бейдж, кілька рядків), колонці дають `template` (`<ng-template let-row>`). В
 * кінці кожного рядка стоїть кнопка з трьома крапками: вона з'являється при
 * наведенні на рядок (на пристроях без наведення — завжди) і подає `rowMenu` з
 * цим рядком і самою кнопкою (до неї прив'язують спливаюче вікно); в кінці рядка заголовків — завжди видимий «+», що подає `columnAdd`. Наведений рядок виділяється акцентним кольором (`--nav-active-bg`).
 */
@Component({
  selector: 'app-data-table',
  templateUrl: './data-table.component.html',
  styleUrls: ['./data-table.component.scss'],
  standalone: false,
})
export class DataTableComponent {
  readonly columns = input.required<readonly DataTableColumn[]>();
  readonly rows = input.required<readonly DataTableRow[]>();
  /** Ключ назви таблиці в мовному файлі (для екранних читачів); `null` — без назви. */
  readonly labelKey = input<string | null>(null);
  /** Показувати «+» в кінці рядка заголовків. */
  readonly showAddColumn = input(true);
  /** Показувати кнопку з трьома крапками в кінці рядків (без неї комірка порожня). */
  readonly showRowMenu = input(true);
  /** Рядок, який лишається виділеним, як при наведенні (наприклад, поки відкрите його меню); `null` — нема. */
  readonly highlightedRowId = input<string | null>(null);
  /** Натиснули кнопку з трьома крапками в кінці рядка. */
  readonly rowMenu = output<DataTableRowMenuEvent>();
  /** Натиснули «+» в кінці рядка заголовків. */
  readonly columnAdd = output<void>();

  protected openMenu(row: DataTableRow, event: Event): void {
    this.rowMenu.emit({ row, anchor: event.currentTarget as HTMLElement });
  }

  protected text(row: DataTableRow, column: DataTableColumn): string {
    const value = row[column.id];
    return value === null || value === undefined ? '' : String(value);
  }
}
