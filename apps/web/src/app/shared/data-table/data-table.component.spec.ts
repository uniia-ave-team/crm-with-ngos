import { Component, TemplateRef, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { DataTableCellContext, DataTableColumn, DataTableRow } from './data-table.component';
import { DataTableModule } from './data-table.module';

@Component({
  template: `
    <app-data-table [columns]="columns()" [rows]="rows()" [labelKey]="label()" [showRowMenu]="rowMenuShown()" [showAddColumn]="addShown()" (rowMenu)="opened.push($event)" (columnAdd)="columnAdds = columnAdds + 1" />
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
  readonly opened: DataTableRow[] = [];
  columnAdds = 0;
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

    it('emits the row of the button that was clicked', () => {
      const { fixture, host, element } = render(base, rows);
      (element.querySelectorAll('tbody .data-table__menu')[1] as HTMLButtonElement).click();
      fixture.detectChanges();
      expect(host.opened).toEqual([rows[1]]);
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
