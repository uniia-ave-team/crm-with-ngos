import { Component, TemplateRef, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { DataTableCellContext, DataTableColumn, DataTableRow, DataTableRowMenuEvent, DataTableSort } from './data-table.component';
import { DataTableModule } from './data-table.module';

@Component({
  template: `
    <app-data-table [columns]="columns()" [rows]="rows()" [labelKey]="label()" [showRowMenu]="rowMenuShown()" [showAddColumn]="addShown()" [highlightedRowId]="highlighted()" (rowMenu)="opened.push($event)" (columnAdd)="columnAdds = columnAdds + 1" (sortChange)="sorts.push($event)" />
    <ng-template #custom let-row let-column="column"><b class="custom">{{ column.id }}:{{ row['title'] }}</b></ng-template>
  `,
  imports: [DataTableModule],
})
class HostComponent {
  readonly columns = signal<readonly DataTableColumn[]>([]);
  readonly rows = signal<readonly DataTableRow[]>([]);
  readonly label = signal<string | null>(null);
  readonly rowMenuShown = signal(true);
  readonly addShown = signal(true);
  readonly highlighted = signal<string | null>(null);
  readonly opened: DataTableRowMenuEvent[] = [];
  columnAdds = 0;
  readonly sorts: (DataTableSort | null)[] = [];
  readonly custom = viewChild<TemplateRef<DataTableCellContext>>('custom');
}

describe('DataTableComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render(columns: DataTableColumn[], rows: DataTableRow[], label: string | null = null) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.columns.set(columns);
    fixture.componentInstance.rows.set(rows);
    fixture.componentInstance.label.set(label);
    fixture.detectChanges();
    return { fixture, host: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  const base: DataTableColumn[] = [
    { id: 'name', labelKey: 'colleagues.columns.name' },
    { id: 'email', labelKey: 'colleagues.columns.email', width: '30%' },
  ];
  const rows: DataTableRow[] = [
    { id: 'r1', name: 'A', email: 'a@example.org' },
    { id: 'r2', name: 'B', email: null },
  ];

  it('draws the headers from the language file, plus an empty one for the menu column', () => {
    const { element } = render(base, rows);
    const headers = Array.from(element.querySelectorAll('th')).map((th) => th.textContent?.trim());
    expect(headers).toEqual([uk.colleagues.columns.name, uk.colleagues.columns.email, '']);
  });

  describe('the filter icon in the headers of the columns', () => {
    it('is in the header of every column, at the end of the cell, and not in the cell of the menu column', () => {
      const { element } = render(base, rows);
      const heads = Array.from(element.querySelectorAll('th'));
      const withIcon = heads.map((th) => th.querySelector('ion-icon.data-table__filter') !== null);
      expect(withIcon).toEqual([true, true, false]);
      for (const th of heads.slice(0, 2)) {
        expect(th.classList).toContain('data-table__head');
        expect(th.querySelector('button')?.lastElementChild?.classList).toContain('data-table__filter');
      }
    });

    it('is the icon filter-outline, hidden from the screen readers, and adds no text to the header', () => {
      const { element } = render(base, rows);
      const icon = element.querySelector('th ion-icon.data-table__filter') as unknown as HTMLElement & { name: string };
      expect(icon.name).toBe('filter-outline');
      expect(icon.getAttribute('aria-hidden')).toBe('true');
      expect(element.querySelector('th')?.textContent?.trim()).toBe(uk.colleagues.columns.name);
    });

    it('is not shown (and the cell is not highlighted) until the header is hovered, only by the styles', () => {
      const { element } = render(base, rows);
      expect(getComputedStyle(element.querySelector('th ion-icon') as HTMLElement).opacity).toBe('0');
    });
  });

  describe('sorting by the headers', () => {
    const sortable: DataTableColumn[] = [
      { id: 'name', labelKey: 'colleagues.columns.name', orderBy: 'LastName' },
      { id: 'position', labelKey: 'colleagues.columns.position', orderBy: '' },
      { id: 'email', labelKey: 'colleagues.columns.email', orderBy: 'Email' },
    ];
    const head = (element: HTMLElement, index: number) => element.querySelectorAll('th')[index] as HTMLElement;
    const button = (element: HTMLElement, index: number) => head(element, index).querySelector('button') as HTMLButtonElement;
    const iconName = (element: HTMLElement, index: number) =>
      (head(element, index).querySelector('ion-icon') as unknown as { name: string }).name;
    const click = (ctx: ReturnType<typeof render>, index: number) => {
      button(ctx.element, index).click();
      ctx.fixture.detectChanges();
    };

    it('has a button in every header of the columns, with a tabindex, and none is sorted at first', () => {
      const ctx = render(sortable, rows);
      for (const index of [0, 1, 2]) {
        expect(button(ctx.element, index).getAttribute('tabindex')).toBe('0');
        expect(button(ctx.element, index).textContent?.trim()).toBe(head(ctx.element, index).textContent?.trim());
        expect(head(ctx.element, index).classList).not.toContain('data-table__head--sorted');
        expect(head(ctx.element, index).hasAttribute('aria-sort')).toBe(false);
        expect(iconName(ctx.element, index)).toBe('filter-outline');
      }
      expect(ctx.host.sorts).toEqual([]);
    });

    it('sorts from the smallest by the first click: the header stays selected, the icon is the caret down, and the column is given', () => {
      const ctx = render(sortable, rows);
      click(ctx, 0);
      expect(head(ctx.element, 0).classList).toContain('data-table__head--sorted');
      expect(head(ctx.element, 0).getAttribute('aria-sort')).toBe('ascending');
      expect(iconName(ctx.element, 0)).toBe('caret-down-outline');
      expect(ctx.host.sorts).toEqual([{ column: sortable[0], direction: 'asc' }]);
      expect(ctx.host.sorts[0]?.column.orderBy).toBe('LastName');
    });

    it('sorts the other way by the second click on the same header: the caret up', () => {
      const ctx = render(sortable, rows);
      click(ctx, 0);
      click(ctx, 0);
      expect(head(ctx.element, 0).classList).toContain('data-table__head--sorted');
      expect(head(ctx.element, 0).getAttribute('aria-sort')).toBe('descending');
      expect(iconName(ctx.element, 0)).toBe('caret-up-outline');
      expect(ctx.host.sorts[1]).toEqual({ column: sortable[0], direction: 'desc' });
    });

    it('resets the choice by the third click: the header is not selected, the icon is the filter again, and null is given', () => {
      const ctx = render(sortable, rows);
      click(ctx, 0);
      click(ctx, 0);
      click(ctx, 0);
      expect(head(ctx.element, 0).classList).not.toContain('data-table__head--sorted');
      expect(head(ctx.element, 0).hasAttribute('aria-sort')).toBe(false);
      expect(iconName(ctx.element, 0)).toBe('filter-outline');
      expect(ctx.host.sorts[2]).toBeNull();
    });

    it('starts again from the smallest after the reset', () => {
      const ctx = render(sortable, rows);
      for (let i = 0; i < 4; i++) {
        click(ctx, 0);
      }
      expect(ctx.host.sorts.map((sort) => sort?.direction ?? null)).toEqual(['asc', 'desc', null, 'asc']);
    });

    it('keeps only one header selected: another click selects it and takes the choice from the first, from the first step', () => {
      const ctx = render(sortable, rows);
      click(ctx, 0);
      click(ctx, 0);
      click(ctx, 2);
      expect(head(ctx.element, 0).classList).not.toContain('data-table__head--sorted');
      expect(iconName(ctx.element, 0)).toBe('filter-outline');
      expect(head(ctx.element, 2).classList).toContain('data-table__head--sorted');
      expect(iconName(ctx.element, 2)).toBe('caret-down-outline');
      expect(ctx.element.querySelectorAll('.data-table__head--sorted')).toHaveLength(1);
      expect(ctx.host.sorts[2]).toEqual({ column: sortable[2], direction: 'asc' });
    });

    it('selects the column with an empty orderBy too, and gives it as it is', () => {
      const ctx = render(sortable, rows);
      click(ctx, 1);
      expect(head(ctx.element, 1).classList).toContain('data-table__head--sorted');
      expect(ctx.host.sorts[0]?.column.orderBy).toBe('');
    });

    it('does not touch the menu column and the plus: no button in the last header cell besides the plus', () => {
      const { element } = render(sortable, rows);
      const last = element.querySelectorAll('th')[3] as HTMLElement;
      expect(last.querySelector('.data-table__sort')).toBeNull();
      expect(last.querySelector('.data-table__add')).not.toBeNull();
    });
  });

  describe('the plus at the end of the header row', () => {
    it('is the last cell of the header row, with a label from the language file and a tabindex', () => {
      const { element } = render(base, rows);
      const last = element.querySelector('thead tr')!.lastElementChild!;
      const button = last.querySelector('.data-table__add')!;
      expect(button).not.toBeNull();
      expect(button.getAttribute('aria-label')).toBe(uk.table.addColumn);
      expect(button.getAttribute('tabindex')).toBe('0');
      expect(button.querySelector('ion-icon')?.getAttribute('name')).toBe('add-outline');
    });

    it('is only in the header row: the rows have the three dots', () => {
      const { element } = render(base, rows);
      expect(element.querySelectorAll('.data-table__add')).toHaveLength(1);
      expect(element.querySelectorAll('tbody .data-table__add')).toHaveLength(0);
    });

    it('is hidden when showAddColumn is off, and the header cell stays', () => {
      const { fixture, host, element } = render(base, rows);
      host.addShown.set(false);
      fixture.detectChanges();
      expect(element.querySelector('.data-table__add')).toBeNull();
      expect(element.querySelectorAll('thead th')).toHaveLength(3);
    });

    it('emits when it is clicked', () => {
      const { fixture, host, element } = render(base, rows);
      (element.querySelector('.data-table__add') as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(host.columnAdds).toBe(1);
    });
  });

  it('draws one row per item, with the value of the column as text (empty for null)', () => {
    const { element } = render(base, rows);
    const cells = Array.from(element.querySelectorAll('tbody tr')).map((tr) =>
      Array.from(tr.querySelectorAll('td:not(.data-table__menu-cell)')).map((td) => td.textContent?.trim()),
    );
    expect(cells).toEqual([
      ['A', 'a@example.org'],
      ['B', ''],
    ]);
  });

  it('leaves the header empty for a column without a label key', () => {
    const { element } = render([{ id: 'name', labelKey: null }], rows);
    expect(element.querySelectorAll('th')[0].textContent?.trim()).toBe('');
  });

  it('sets the width of the column', () => {
    const { element } = render(base, rows);
    const cols = element.querySelectorAll('col');
    expect((cols[1] as HTMLElement).style.width).toBe('30%');
    expect((cols[0] as HTMLElement).style.width).toBe('');
  });

  it('draws a cell with the own template of the column, given the row and the column', () => {
    const { fixture, host, element } = render(base, [{ id: 'r1', name: 'A', title: 'T1' }]);
    host.columns.set([{ id: 'name', labelKey: 'colleagues.columns.name', template: host.custom() }]);
    fixture.detectChanges();

    expect(element.querySelector('tbody td .custom')?.textContent?.trim()).toBe('name:T1');
  });

  it('gives the table a name for screen readers when a label key is set', () => {
    const { element } = render(base, rows, 'colleagues.columns.name');
    expect(element.querySelector('table')?.getAttribute('aria-label')).toBe(uk.colleagues.columns.name);
    const unlabeled = render(base, rows).element;
    expect(unlabeled.querySelector('table')?.hasAttribute('aria-label')).toBe(false);
  });

  describe('the three dots at the end of the row', () => {
    it('is in every row, with a label from the language file and a tabindex', () => {
      const { element } = render(base, rows);
      const buttons = Array.from(element.querySelectorAll('tbody .data-table__menu'));
      expect(buttons).toHaveLength(2);
      for (const button of buttons) {
        expect(button.getAttribute('aria-label')).toBe(uk.table.rowMenu);
        expect(button.getAttribute('tabindex')).toBe('0');
        expect(button.querySelector('ion-icon')?.getAttribute('name')).toBe('ellipsis-horizontal');
      }
    });

    it('is the last cell of the row', () => {
      const { element } = render(base, rows);
      const last = element.querySelector('tbody tr')!.lastElementChild!;
      expect(last.classList).toContain('data-table__menu-cell');
    });

    it('are hidden when showRowMenu is off, and the cell of each row stays, empty', () => {
      const { fixture, host, element } = render(base, rows);
      host.rowMenuShown.set(false);
      fixture.detectChanges();
      expect(element.querySelectorAll('tbody .data-table__menu')).toHaveLength(0);
      for (const row of Array.from(element.querySelectorAll('tbody tr'))) {
        expect(row.lastElementChild?.classList).toContain('data-table__menu-cell');
        expect(row.lastElementChild?.querySelector('button')).toBeNull();
      }
    });

    it('emits the row and the button itself (to anchor a popover to it) of the click', () => {
      const { fixture, host, element } = render(base, rows);
      const button = element.querySelectorAll('tbody .data-table__menu')[1] as HTMLButtonElement;
      button.click();
      fixture.detectChanges();
      expect(host.opened).toHaveLength(1);
      expect(host.opened[0].row).toBe(rows[1]);
      expect(host.opened[0].anchor).toBe(button);
    });
  });

  describe('the highlighted row', () => {
    const active = (element: HTMLElement) =>
      Array.from(element.querySelectorAll('tbody tr')).map((tr) => tr.classList.contains('data-table__row--active'));

    it('is marked like a hovered row, only the one with that id', () => {
      const { fixture, host, element } = render(base, rows);
      expect(active(element)).toEqual([false, false]);

      host.highlighted.set('r2');
      fixture.detectChanges();
      expect(active(element)).toEqual([false, true]);

      host.highlighted.set(null);
      fixture.detectChanges();
      expect(active(element)).toEqual([false, false]);
    });
  });

  it('follows the columns and the rows when they are replaced by code', () => {
    const { fixture, host, element } = render(base, rows);
    host.columns.set([{ id: 'email', labelKey: 'colleagues.columns.phone' }]);
    host.rows.set([{ id: 'x', email: 'x@example.org' }]);
    fixture.detectChanges();

    expect(Array.from(element.querySelectorAll('th')).map((th) => th.textContent?.trim())).toEqual([uk.colleagues.columns.phone, '']);
    expect(element.querySelectorAll('tbody tr')).toHaveLength(1);
    expect(element.querySelector('tbody td')?.textContent?.trim()).toBe('x@example.org');
  });
});
