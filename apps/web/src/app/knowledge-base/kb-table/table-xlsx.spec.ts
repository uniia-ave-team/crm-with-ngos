import { columnName, richTextRuns, sheetName, xlsxFileName, xlsxParts } from './table-xlsx';

describe('table XLSX', () => {
  const parse = (text: string) => new DOMParser().parseFromString(text, 'application/xml');

  it('splits the formatted text of a cell into parts, with line breaks and plain characters', () => {
    expect(richTextRuns('<b>Фонд</b> &amp; <i><u>дім</u></i><br>2')).toEqual([
      { text: 'Фонд', bold: true, italic: false, underline: false, strike: false },
      { text: ' & ', bold: false, italic: false, underline: false, strike: false },
      { text: 'дім', bold: false, italic: true, underline: true, strike: false },
      { text: '\n2', bold: false, italic: false, underline: false, strike: false },
    ]);
  });

  it('names columns as Excel does and makes the sheet and file names allowed', () => {
    expect([0, 25, 26, 701, 702].map(columnName)).toEqual(['A', 'Z', 'AA', 'ZZ', 'AAA']);
    expect(sheetName('Звіт: квітень/травень [2026]', 'Таблиця')).toBe('Звіт квітень травень 2026');
    expect(sheetName('  ', 'Таблиця')).toBe('Таблиця');
    expect(xlsxFileName('Донори 2026', 'Таблиця')).toBe('Донори 2026.xlsx');
    expect(xlsxFileName('Звіт: квітень?', 'Таблиця')).toBe('Звіт квітень.xlsx');
  });

  it('keeps the formatting, the background colour and its text colour, with a bold header row fixed at the top', () => {
    const parts = xlsxParts({
      sheetName: 'Донори',
      headers: ['<div style="background-color: #d4e4ff">Назва</div>', 'Нотатка'],
      rows: [
        ['<b>Фонд</b> <s>старий</s>', ''],
        ['<div style="background-color: #fde2e4">☑</div>', 'a < b'],
      ],
    });
    for (const content of Object.values(parts)) {
      expect(parse(content).querySelector('parsererror')).toBeNull();
    }
    const strings = parse(parts['xl/sharedStrings.xml']);
    const items = Array.from(strings.getElementsByTagName('si'));
    // Заголовок — напівжирний; частини тексту — зі своїм форматуванням.
    expect(items[0].getElementsByTagName('b')).toHaveLength(1);
    const fund = items[2].getElementsByTagName('r');
    expect(fund[0].getElementsByTagName('b')).toHaveLength(1);
    expect(fund[2].getElementsByTagName('strike')).toHaveLength(1);
    expect(items.map((item) => item.textContent)).toEqual(['Назва', 'Нотатка', 'Фонд старий', '☑', 'a < b']);

    const styles = parts['xl/styles.xml'];
    expect(styles).toContain('<fgColor rgb="FFD4E4FF"/>');
    expect(styles).toContain('<fgColor rgb="FFFDE2E4"/>');
    expect(styles).toContain('wrapText="1"');
    const sheet = parse(parts['xl/worksheets/sheet1.xml']);
    expect(sheet.getElementsByTagName('pane')[0].getAttribute('state')).toBe('frozen');
    // Порожня клітинка без кольору не записується.
    expect(sheet.querySelector('c[r="B2"]')).toBeNull();
    expect(parse(parts['xl/workbook.xml']).getElementsByTagName('sheet')[0].getAttribute('name')).toBe('Донори');
  });
});
