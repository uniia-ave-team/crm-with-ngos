import { contrastWithWhite, relativeLuminance } from '../avatar/avatar-color';
import { DARK_PAGE_BACKGROUND, roleColor, roleLetterSums } from './role-color';

const rgb = (hex: string): [number, number, number] => [
  parseInt(hex.slice(1, 3), 16),
  parseInt(hex.slice(3, 5), 16),
  parseInt(hex.slice(5, 7), 16),
];
const darkBackgroundLuminance = relativeLuminance(rgb(DARK_PAGE_BACKGROUND));
const contrastWithDarkPage = (hex: string) => (relativeLuminance(rgb(hex)) + 0.05) / (darkBackgroundLuminance + 0.05);
/** Відтінок (градуси) hex-кольору. */
function hueOf(hex: string): number {
  const [r, g, b] = rgb(hex).map((c) => c / 255);
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const d = max - min;
  if (d === 0) return 0;
  const h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
  return (h * 60 + 360) % 360;
}

// Назви ролей — загальні посади, не імена людей.
const ROLES = [
  'Член',
  'Голова ГО',
  'Куратор',
  'Керівник проєкту',
  'Позаштатний працівник',
  'Позаштатна працівниця',
  'Перекладач',
  'Перекладачка',
  'Дизайнер',
  'Бек-розробник',
  'Фронт-розробник',
  'Адміністратор',
  'Менеджер',
  'Волонтер',
];

describe('roleLetterSums', () => {
  it('adds the first letter to the last, the second to the last but one, the third to the last but two', () => {
    // A=65, b=98, c=99, d=100, e=101, f=102
    expect(roleLetterSums('Abcdef')).toMatchObject({ first: 65 + 102, second: 98 + 101, third: 99 + 100 });
  });

  it('gets the total as the first sum plus the third minus the second', () => {
    const sums = roleLetterSums('Abcdef')!;
    expect(sums.total).toBe(sums.first + sums.third - sums.second);
    expect(sums.total).toBe(167);
  });

  it('counts the missing letters of a short name as 0', () => {
    // «ab»: перша сума a+b, друга b+a, третя 0+0.
    expect(roleLetterSums('ab')).toEqual({ first: 97 + 98, second: 98 + 97, third: 0, total: 0 });
    // Одна літера: вона ж і перша, і остання; решта — 0.
    expect(roleLetterSums('a')).toEqual({ first: 194, second: 0, third: 0, total: 194 });
  });

  it('counts only the letters: spaces, hyphens and digits are ignored', () => {
    expect(roleLetterSums('Бек-розробник')).toEqual(roleLetterSums('Бекрозробник'));
    expect(roleLetterSums('  Член 2 ')).toEqual(roleLetterSums('Член'));
  });

  it('is null when the name has no letters', () => {
    expect(roleLetterSums('')).toBeNull();
    expect(roleLetterSums(' 12 - ')).toBeNull();
  });

  it('tells the capital letter from the small one, as their codes differ', () => {
    expect(roleLetterSums('Член')).not.toEqual(roleLetterSums('член'));
  });
});

describe('roleColor', () => {
  it('is null when the name has no letters', () => {
    expect(roleColor('')).toBeNull();
    expect(roleColor('123')).toBeNull();
  });

  it('gives #rrggbb colors, the same every time for the same name', () => {
    const color = roleColor('Куратор')!;
    expect(color.base).toMatch(/^#[0-9a-f]{6}$/);
    expect(color.light).toMatch(/^#[0-9a-f]{6}$/);
    expect(roleColor('Куратор')).toEqual(color);
  });

  it('ignores the characters that are not letters', () => {
    expect(roleColor('Бек-розробник')).toEqual(roleColor('Бек розробник'));
  });

  describe.each(ROLES)('the role "%s"', (role) => {
    it('has the base color with the contrast of at least 7:1 with white (the fill with the white text)', () => {
      expect(contrastWithWhite(rgb(roleColor(role)!.base))).toBeGreaterThanOrEqual(7);
    });

    it('has the light color with the contrast of at least 7:1 with the dark page background', () => {
      expect(contrastWithDarkPage(roleColor(role)!.light)).toBeGreaterThanOrEqual(7);
    });

    it('keeps the hue in the light color: it is the base color mixed with white', () => {
      const { base, light } = roleColor(role)!;
      const difference = Math.abs(hueOf(base) - hueOf(light));
      expect(Math.min(difference, 360 - difference)).toBeLessThan(8);
    });
  });

  it('does not make the colors muddy: a visible saturation stays in the base color', () => {
    for (const role of ROLES) {
      const [r, g, b] = rgb(roleColor(role)!.base);
      expect(Math.max(r, g, b) - Math.min(r, g, b)).toBeGreaterThan(40);
    }
  });

  it('gives different roles different base colors (also the ones with the same last sum)', () => {
    const colors = ROLES.map((role) => roleColor(role)!.base);
    expect(new Set(colors).size).toBe(ROLES.length);
    // «Куратор» і «Дизайнер» мають однакову загальну суму, але різну другу: кольори різні.
    expect(roleLetterSums('Куратор')!.total).toBe(roleLetterSums('Дизайнер')!.total);
    expect(roleLetterSums('Куратор')!.second).not.toBe(roleLetterSums('Дизайнер')!.second);
    expect(roleColor('Куратор')!.base).not.toBe(roleColor('Дизайнер')!.base);
  });

  it('spreads the hues of the sample roles over the whole circle, not into one corner', () => {
    const hues = ROLES.map((role) => hueOf(roleColor(role)!.base)).sort((a, b) => a - b);
    expect(hues[hues.length - 1] - hues[0]).toBeGreaterThan(200);
  });

  it('works for a one-letter and a two-letter name', () => {
    for (const name of ['а', 'Ab']) {
      const color = roleColor(name)!;
      expect(contrastWithWhite(rgb(color.base))).toBeGreaterThanOrEqual(7);
      expect(contrastWithDarkPage(color.light)).toBeGreaterThanOrEqual(7);
    }
  });
});
