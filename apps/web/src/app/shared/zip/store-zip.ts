/**
 * Zip-архів без стиснення (метод `stored`): файли як є, з контрольними сумами CRC-32 і центральним
 * каталогом. Досить для невеликих файлів, які браузер збирає сам (наприклад, `.xlsx` — це zip із
 * XML), і не потребує бібліотеки. Назви файлів — UTF-8 (прапорець 11 загального призначення).
 *
 *   const bytes = storeZip({ 'a.txt': new TextEncoder().encode('…') });
 */

const LOCAL_HEADER = 0x04034b50;
const CENTRAL_HEADER = 0x02014b50;
const END_OF_CENTRAL_DIRECTORY = 0x06054b50;
/** Версія формату, потрібна для розпакування (2.0). */
const VERSION = 20;
/** Назви файлів — у UTF-8. */
const UTF8_NAMES = 0x0800;

let crcTable: Uint32Array | null = null;

/** CRC-32 (поліном 0xEDB88320, як у zip). */
export function crc32(bytes: Uint8Array): number {
  if (!crcTable) {
    crcTable = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) {
        c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
      }
      crcTable[n] = c >>> 0;
    }
  }
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

/** Час і дата у форматі MS-DOS (як їх зберігає zip). */
function dosDateTime(date: Date): { readonly time: number; readonly date: number } {
  return {
    time: (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2),
    date: ((Math.max(date.getFullYear(), 1980) - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate(),
  };
}

/** Zip із файлів `files` (шлях у архіві → вміст) у їхньому порядку. */
export function storeZip(files: Readonly<Record<string, Uint8Array>>, modified = new Date()): Uint8Array<ArrayBuffer> {
  const encoder = new TextEncoder();
  const { time, date } = dosDateTime(modified);
  const entries = Object.entries(files).map(([path, data]) => ({ name: encoder.encode(path), data, crc: crc32(data) }));
  const localSize = entries.reduce((sum, entry) => sum + 30 + entry.name.length + entry.data.length, 0);
  const centralSize = entries.reduce((sum, entry) => sum + 46 + entry.name.length, 0);
  const out = new Uint8Array(localSize + centralSize + 22);
  const view = new DataView(out.buffer);
  let offset = 0;
  const u16 = (value: number) => {
    view.setUint16(offset, value, true);
    offset += 2;
  };
  const u32 = (value: number) => {
    view.setUint32(offset, value, true);
    offset += 4;
  };
  const bytes = (value: Uint8Array) => {
    out.set(value, offset);
    offset += value.length;
  };

  const offsets: number[] = [];
  for (const entry of entries) {
    offsets.push(offset);
    u32(LOCAL_HEADER);
    u16(VERSION);
    u16(UTF8_NAMES);
    u16(0); // без стиснення
    u16(time);
    u16(date);
    u32(entry.crc);
    u32(entry.data.length);
    u32(entry.data.length);
    u16(entry.name.length);
    u16(0);
    bytes(entry.name);
    bytes(entry.data);
  }
  const centralStart = offset;
  entries.forEach((entry, index) => {
    u32(CENTRAL_HEADER);
    u16(VERSION);
    u16(VERSION);
    u16(UTF8_NAMES);
    u16(0);
    u16(time);
    u16(date);
    u32(entry.crc);
    u32(entry.data.length);
    u32(entry.data.length);
    u16(entry.name.length);
    u16(0); // додаткове поле
    u16(0); // коментар
    u16(0); // номер диска
    u16(0); // внутрішні атрибути
    u32(0); // зовнішні атрибути
    u32(offsets[index]);
    bytes(entry.name);
  });
  const centralLength = offset - centralStart;
  u32(END_OF_CENTRAL_DIRECTORY);
  u16(0);
  u16(0);
  u16(entries.length);
  u16(entries.length);
  u32(centralLength);
  u32(centralStart);
  u16(0);
  return out;
}
