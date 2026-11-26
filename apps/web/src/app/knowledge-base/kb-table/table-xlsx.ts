import { cellTextColor } from '../../shared/color/cell-colors';
import { sanitizeRichText, splitBackground } from '../../shared/rich-text/rich-text';
import { storeZip } from '../../shared/zip/store-zip';

/**
 * Експорт таблиці в Excel (`.xlsx`, Office Open XML) з форматуванням, як у застосунку: напівжирний,
 * курсив, підкреслення, закреслення частини тексту (рядок із частинами в спільних рядках), колір тла
 * клітинки й колір тексту того самого відтінку з контрастом 7:1 (`cellTextColor`), переноси рядків
 * усередині клітинки. Рядок заголовків — напівжирний і закріплений угорі; стовпчики — однакової
 * ширини, текст переноситься. Усе — текстом (формул нема). Файл — zip із XML (`storeZip`).
 */

/** Таблиця для експорту: назва аркуша, заголовки й рядки значень — як зберігаються (HTML з `b`, `i`,
 *  `u`, `s`, `br` і, можливо, тегом кольору тла). */
export interface XlsxTable {
  readonly sheetName: string;
  readonly headers: readonly string[];
  readonly rows: readonly (readonly string[])[];
}

/** Тип файлу `.xlsx`. */
export const XLSX_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const MAIN_NS = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
const REL_NS = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
const PACKAGE_REL_NS = 'http://schemas.openxmlformats.org/package/2006/relationships';
const FONT = 'Calibri';
const FONT_SIZE = 11;
/** Ширина стовпчика (у символах), як 220px у застосунку. */
const COLUMN_WIDTH = 32;
/** Аркуш Excel: до 31 символу, без `\ / ? * : [ ]`. */
const SHEET_NAME_MAX = 31;
const SHEET_NAME_FORBIDDEN = /[\\/?*:[\]]/g;

/** Частина тексту з однаковим форматуванням. */
interface Run {
  readonly text: string;
  readonly bold: boolean;
  readonly italic: boolean;
  readonly underline: boolean;
  readonly strike: boolean;
}

/** Символ, який XML 1.0 дозволяє (табуляція, переноси й видимі символи). */
const isXmlChar = (code: number) => code === 0x9 || code === 0xa || code === 0xd || (code >= 0x20 && code !== 0xfffe && code !== 0xffff);

/** Текст для XML: без недозволених символів, з екранованими `& < > "`. */
function xml(text: string): string {
  let clean = '';
  for (const char of text) {
    if (isXmlChar(char.codePointAt(0)!)) {
      clean += char;
    }
  }
  return clean.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** HTML-сутності, які лишає `sanitizeRichText` (і нерозривний пробіл, який вставляє редактор). */
function decodeEntities(text: string): string {
  return text
    .replace(/&nbsp;/g, ' ')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&amp;/g, '&');
}

/** Очищений HTML клітинки → частини тексту з форматуванням (`<br>` — перенос рядка). */
export function richTextRuns(html: string): Run[] {
  const state = { b: 0, i: 0, u: 0, s: 0 };
  const runs: Run[] = [];
  const push = (text: string) => {
    const run = { text, bold: state.b > 0, italic: state.i > 0, underline: state.u > 0, strike: state.s > 0 };
    const last = runs.at(-1);
    if (last && last.bold === run.bold && last.italic === run.italic && last.underline === run.underline && last.strike === run.strike) {
      runs[runs.length - 1] = { ...last, text: last.text + text };
    } else {
      runs.push(run);
    }
  };
  for (const [token, closing, tag, text] of sanitizeRichText(html).matchAll(/<(\/?)([bius])>|<br>|([^<]+)/g)) {
    if (text !== undefined) {
      push(decodeEntities(text));
    } else if (tag) {
      state[tag as keyof typeof state] += closing ? -1 : 1;
    } else if (token === '<br>') {
      push('\n');
    }
  }
  return runs;
}

/** Колір для XML (`FFrrggbb`). */
const argb = (hex: string) => `FF${hex.slice(1).toUpperCase()}`;

/** Номер стовпчика (з 0) → літери Excel (`A`, `Z`, `AA`…). */
export function columnName(index: number): string {
  let name = '';
  for (let n = index + 1; n > 0; n = Math.floor((n - 1) / 26)) {
    name = String.fromCharCode(65 + ((n - 1) % 26)) + name;
  }
  return name;
}

/** Символи, яких не може бути в назві файлу (Windows, macOS, Linux); керівні — окремо, за кодом. */
const FILE_NAME_FORBIDDEN = /[\\/:*?"<>|]/g;
const MAX_FILE_NAME_LENGTH = 120;

/** Назва файлу `.xlsx` з назви таблиці (без недозволених символів); порожня — `fallback`. */
export function xlsxFileName(title: string, fallback: string): string {
  const visible = [...title].filter((char) => char.codePointAt(0)! >= 0x20).join('');
  const name = visible.replace(FILE_NAME_FORBIDDEN, ' ').replace(/\s+/g, ' ').trim().slice(0, MAX_FILE_NAME_LENGTH).trim();
  return `${name || fallback}.xlsx`;
}

/** Назва аркуша з назви таблиці (Excel не дозволяє деяких символів і довгих назв). */
export function sheetName(title: string, fallback: string): string {
  const name = title.replace(SHEET_NAME_FORBIDDEN, ' ').replace(/\s+/g, ' ').trim().slice(0, SHEET_NAME_MAX).trim();
  return name.replace(/^'+|'+$/g, '') || fallback;
}

/**
 * Частини файлу `.xlsx` (шлях → XML): типи вмісту, звʼязки, книга, аркуш, стилі й спільні рядки.
 * Окремо від пакування — для тестів.
 */
export function xlsxParts(table: XlsxTable): Record<string, string> {
  const strings: string[] = [];
  const fonts: string[] = [];
  const fills: string[] = [];
  const xfs: string[] = [];
  const fontIndex = new Map<string, number>();
  const fillIndex = new Map<string, number>();
  const xfIndex = new Map<string, number>();
  const font = (bold: boolean, color: string | null) => {
    const key = `${bold}|${color}`;
    if (!fontIndex.has(key)) {
      fontIndex.set(key, fonts.length);
      fonts.push(`<font>${bold ? '<b/>' : ''}<sz val="${FONT_SIZE}"/>${color ? `<color rgb="${argb(color)}"/>` : ''}<name val="${FONT}"/></font>`);
    }
    return fontIndex.get(key)!;
  };
  const fill = (background: string | null) => {
    if (!background) {
      return 0;
    }
    if (!fillIndex.has(background)) {
      fillIndex.set(background, fills.length);
      fills.push(`<fill><patternFill patternType="solid"><fgColor rgb="${argb(background)}"/><bgColor indexed="64"/></patternFill></fill>`);
    }
    return fillIndex.get(background)!;
  };
  /** Стиль клітинки: шрифт (напівжирний заголовок, колір тексту) і тло; текст переноситься, угорі. */
  const style = (bold: boolean, background: string | null) => {
    const color = background ? cellTextColor(background) : null;
    const key = `${font(bold, color)}|${fill(background)}`;
    if (!xfIndex.has(key)) {
      xfIndex.set(key, xfs.length);
      const [fontId, fillId] = key.split('|');
      xfs.push(
        `<xf numFmtId="0" fontId="${fontId}" fillId="${fillId}" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1">` +
          '<alignment vertical="top" wrapText="1"/></xf>',
      );
    }
    return { index: xfIndex.get(key)!, color };
  };
  // Обовʼязкові перші: шрифт за замовчуванням, тло «нема» й «gray125», стиль за замовчуванням.
  font(false, null);
  fills.push('<fill><patternFill patternType="none"/></fill>', '<fill><patternFill patternType="gray125"/></fill>');
  xfs.push('<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>');

  /** Клітинка: спільний рядок з частинами (форматування й колір тексту — у кожній частині). */
  const cell = (value: string, column: number, row: number, header: boolean) => {
    const { background, html } = splitBackground(value);
    const runs = richTextRuns(html);
    const empty = !runs.some((run) => run.text !== '');
    // Порожня клітинка без кольору тла — нема чого записувати.
    if (empty && !background) {
      return '';
    }
    const { index, color } = style(header, background);
    const ref = `${columnName(column)}${row}`;
    if (empty) {
      return `<c r="${ref}" s="${index}"/>`;
    }
    const plain = runs.length === 1 && !runs[0].bold && !runs[0].italic && !runs[0].underline && !runs[0].strike && !header && !color;
    const item = plain
      ? `<si><t xml:space="preserve">${xml(runs[0].text)}</t></si>`
      : '<si>' +
        runs
          .map(
            (run) =>
              '<r><rPr>' +
              (run.bold || header ? '<b/>' : '') +
              (run.italic ? '<i/>' : '') +
              (run.strike ? '<strike/>' : '') +
              (run.underline ? '<u/>' : '') +
              `<sz val="${FONT_SIZE}"/>` +
              (color ? `<color rgb="${argb(color)}"/>` : '') +
              `<rFont val="${FONT}"/>` +
              `</rPr><t xml:space="preserve">${xml(run.text)}</t></r>`,
          )
          .join('') +
        '</si>';
    strings.push(item);
    return `<c r="${ref}" s="${index}" t="s"><v>${strings.length - 1}</v></c>`;
  };

  const columns = table.headers.length;
  const sheetRows = [table.headers, ...table.rows].map(
    (values, rowIndex) => `<row r="${rowIndex + 1}">${values.map((value, column) => cell(value, column, rowIndex + 1, rowIndex === 0)).join('')}</row>`,
  );
  const lastCell = `${columnName(Math.max(columns, 1) - 1)}${table.rows.length + 1}`;

  return {
    '[Content_Types].xml':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
      '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
      '<Default Extension="xml" ContentType="application/xml"/>' +
      '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
      '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
      '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
      '<Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>' +
      '</Types>',
    '_rels/.rels':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      `<Relationships xmlns="${PACKAGE_REL_NS}">` +
      '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
      '</Relationships>',
    'xl/workbook.xml':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      `<workbook xmlns="${MAIN_NS}" xmlns:r="${REL_NS}">` +
      `<sheets><sheet name="${xml(table.sheetName)}" sheetId="1" r:id="rId1"/></sheets>` +
      '</workbook>',
    'xl/_rels/workbook.xml.rels':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      `<Relationships xmlns="${PACKAGE_REL_NS}">` +
      '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
      '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
      '<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>' +
      '</Relationships>',
    'xl/worksheets/sheet1.xml':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      `<worksheet xmlns="${MAIN_NS}" xmlns:r="${REL_NS}">` +
      `<dimension ref="A1:${lastCell}"/>` +
      '<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>' +
      `<sheetFormatPr defaultRowHeight="15"/>` +
      (columns ? `<cols><col min="1" max="${columns}" width="${COLUMN_WIDTH}" customWidth="1"/></cols>` : '') +
      `<sheetData>${sheetRows.join('')}</sheetData>` +
      '</worksheet>',
    'xl/styles.xml':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      `<styleSheet xmlns="${MAIN_NS}">` +
      `<fonts count="${fonts.length}">${fonts.join('')}</fonts>` +
      `<fills count="${fills.length}">${fills.join('')}</fills>` +
      '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>' +
      '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
      `<cellXfs count="${xfs.length}">${xfs.join('')}</cellXfs>` +
      '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>' +
      '</styleSheet>',
    'xl/sharedStrings.xml':
      '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
      `<sst xmlns="${MAIN_NS}" count="${strings.length}" uniqueCount="${strings.length}">${strings.join('')}</sst>`,
  };
}

/** Файл `.xlsx` (байти zip-архіву). */
export function toXlsx(table: XlsxTable): Uint8Array<ArrayBuffer> {
  const encoder = new TextEncoder();
  const parts = xlsxParts(table);
  return storeZip(Object.fromEntries(Object.entries(parts).map(([path, content]) => [path, encoder.encode(content)])));
}
