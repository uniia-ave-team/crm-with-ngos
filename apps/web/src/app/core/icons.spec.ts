import { DERIVED_ICON_NOTICE, ICONS, IconName, MATERIAL_FILL_NOTICE, iconSetFor, iconSvg, isSafari } from './icons';

describe('icons', () => {
  const agents = {
    safariMac:
      'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15',
    safariIphone:
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1',
    chromeMac: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36',
    chromeIphone:
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/130.0 Mobile/15E148 Safari/604.1',
    firefoxIphone:
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) FxiOS/130.0 Mobile/15E148 Safari/605.1.15',
    edgeWindows:
      'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0',
    android: 'Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36',
    firefoxLinux: 'Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0',
  };

  it('takes Lucide only in Safari (on a Mac and on an iPhone), Material Symbols in every other browser', () => {
    expect(isSafari(agents.safariMac)).toBe(true);
    expect(isSafari(agents.safariIphone)).toBe(true);
    for (const agent of [agents.chromeMac, agents.chromeIphone, agents.firefoxIphone, agents.edgeWindows, agents.android, agents.firefoxLinux]) {
      expect(isSafari(agent), agent).toBe(false);
      expect(iconSetFor(agent)).toBe('material');
    }
    expect(iconSetFor(agents.safariMac)).toBe('lucide');
  });

  it('has an SVG of both sets for every icon', () => {
    for (const name of Object.keys(ICONS) as IconName[]) {
      expect(ICONS[name].lucide, name).toContain('<svg');
      expect(ICONS[name].material, name).toContain('<svg');
    }
  });

  it('gives ion-icon the SVG as it is in a utf8 data URL, Lucide with its license comment and lines in the colour of the text', () => {
    const lucide = iconSvg('trash-outline', 'lucide');
    expect(lucide.startsWith('data:image/svg+xml;utf8,')).toBe(true);
    expect(lucide).toContain('@license lucide-static');
    expect(lucide).toContain('stroke="currentColor"');
  });

  it('fills Material Symbols with the colour of the text', () => {
    const material = iconSvg('trash-outline', 'material');
    expect(material).toContain('<svg fill="currentColor" ');
    // Кожна змінена іконка Material (додано `fill`) несе примітку про зміну (Apache 2.0, п. 4b).
    expect(material).toContain(MATERIAL_FILL_NOTICE);
    expect(new DOMParser().parseFromString(material, 'text/html').querySelector('svg')?.getAttribute('fill')).toBe('currentColor');
  });

  it('makes «delete row» and «delete column» from «add» with a minus instead of the plus', () => {
    expect(ICONS['row-remove'].lucide).not.toContain('M12 22v-6');
    expect(ICONS['row-remove'].lucide).toContain('M9 19h6');
    expect(ICONS['column-remove'].lucide).not.toContain('M19 15V9');
    expect(ICONS['row-remove'].material).toContain('M370-160v-60h220v60H370Z');
    expect(ICONS['column-remove'].material).toContain('m251 30v-60h220v60h-220Z');
    expect(ICONS['row-remove'].material).not.toBe(ICONS['row-add-below'].material);
    // Кожна змінена іконка помітно вказує на зміну (Apache 2.0, п. 4b), а Lucide зберігає свій `@license`.
    for (const name of ['row-remove', 'column-remove'] as const) {
      expect(ICONS[name].lucide).toContain(DERIVED_ICON_NOTICE);
      expect(ICONS[name].material).toContain(DERIVED_ICON_NOTICE);
      expect(ICONS[name].lucide).toContain('@license lucide-static');
    }
  });
});
