import { contrast, parseHex } from './contrast';
import { CELL_TEXT_CONTRAST, cellTextColor, normalizeHex } from './cell-colors';

describe('cell colours', () => {
  it.each(['#fde2e4', '#cdeaf5', '#e2f0cb', '#fff1b6', '#ff6b6b', '#1d3557', '#000000', '#ffffff'])(
    'gives the text on %s a contrast of at least 7:1',
    (background) => {
      expect(contrast(parseHex(cellTextColor(background)), parseHex(background))).toBeGreaterThanOrEqual(CELL_TEXT_CONTRAST);
    },
  );

  it('gives the best possible text where 7:1 cannot be reached (pure red: black, about 5.3:1)', () => {
    expect(cellTextColor('#ff0000')).toBe('#000000');
  });

  it('keeps the hue of the background: dark pink on light pink, light blue on dark blue', () => {
    const [r, g, b] = parseHex(cellTextColor('#fde2e4'));
    expect(r).toBeGreaterThan(b);
    const [r2, g2, b2] = parseHex(cellTextColor('#1d3557'));
    expect(b2).toBeGreaterThan(r2);
    expect(g + g2).toBeGreaterThanOrEqual(0);
  });

  it('takes a HEX colour with or without # and refuses anything else', () => {
    expect(normalizeHex(' #ABCDEF ')).toBe('#abcdef');
    expect(normalizeHex('abcdef')).toBe('#abcdef');
    expect(normalizeHex('#abc')).toBeNull();
    expect(normalizeHex('червоний')).toBeNull();
  });
});
