import { crc32, storeZip } from './store-zip';

describe('storeZip', () => {
  const encode = (text: string) => new TextEncoder().encode(text);

  it('computes CRC-32 as zip does', () => {
    expect(crc32(encode('123456789'))).toBe(0xcbf43926);
  });

  it('stores the files as they are, with local headers, a central directory and its end record', () => {
    const zip = storeZip({ 'a.txt': encode('Перший'), 'теки/b.xml': encode('<b/>') }, new Date(2026, 10, 26, 14, 30));
    const view = new DataView(zip.buffer);
    expect(view.getUint32(0, true)).toBe(0x04034b50);
    // Без стиснення: вміст — як є.
    expect(new TextDecoder().decode(zip)).toContain('Перший');
    const end = zip.length - 22;
    expect(view.getUint32(end, true)).toBe(0x06054b50);
    expect(view.getUint16(end + 10, true)).toBe(2);
    const centralStart = view.getUint32(end + 16, true);
    expect(view.getUint32(centralStart, true)).toBe(0x02014b50);
    expect(view.getUint32(end + 12, true)).toBe(end - centralStart);
    // Назви — у UTF-8 (прапорець 11).
    expect(view.getUint16(6, true) & 0x0800).toBe(0x0800);
  });
});
