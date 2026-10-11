import { avatarColor, contrastWithBlack, contrastWithWhite, relativeLuminance } from './avatar-color';

const rgb = (hex: string) =>
  [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)) as [number, number, number];

describe('avatarColor', () => {
  it('returns null when there are no letters at all', () => {
    expect(avatarColor('', '')).toBeNull();
    expect(avatarColor('  ', '- 1')).toBeNull();
  });

  it('takes the first two letters of each name, subtracts the last letter of the first name and adds the last letter of the last name', () => {
    // Євген: Є(1028)+в(1074)=2102; Вишневський: В(1042)+и(1080)=2122.
    // Поправка: −н(1085) + й(1081) = −4 → 2098 = 832 і 2118 = 846 → 832846.
    // З білим контраст 8.9:1 (ок), з чорним лише 2.4:1 → висвітлено до #a46076.
    expect(avatarColor('Євген', 'Вишневський')).toBe('#a46076');
  });

  it('shifts a too bright colour towards black (first shift) and a too dark one towards white (second shift)', () => {
    // 867846: контраст із білим 3.4:1 → затемнено до #847645 (4.5:1 з білим, 4.7:1 з чорним).
    expect(avatarColor('Олена', 'Іваненко')).toBe('#847645');
    // 864843: з білим 6.9:1, з чорним 2.4:1 → висвітлено до #9b6763.
    expect(avatarColor('Олег', 'Іваненко')).toBe('#9b6763');
    // 866847 → #8c6f4f.
    expect(avatarColor('Оксана', 'Ігнатенко')).toBe('#8c6f4f');
  });

  describe('with the name of the organization', () => {
    it('adds the 5th letter to the first-name sum and the second-to-last letter to the last-name sum', () => {
      // Добра справа: 5-та літера а(1072), передостання в(1074).
      // Олена: 2137 + 1072 + 14 = 3223 (c97); Іваненко: 2104 + 1074 + 14 = 3192 (c78) → c97c78
      // З білим контраст 3.5:1 → затемнено, з чорним 5.2:1 → #a56562 після зсувів.
      expect(avatarColor('Олена', 'Іваненко', 'Добра справа')).toBe('#a56562');
    });

    it('adds 0 for a missing 5th letter, but still takes the second-to-last one', () => {
      // Мрія: 4 літери, 5-ї нема (0); передостання і(1110) → 867 і 3228 (c9c) → 867c9c.
      expect(avatarColor('Олена', 'Іваненко', 'Мрія')).toBe('#7c7290');
    });

    it('adds 0 to both sums when the name is missing or has a single letter', () => {
      const without = avatarColor('Олена', 'Іваненко');
      expect(avatarColor('Олена', 'Іваненко', '')).toBe(without);
      expect(avatarColor('Олена', 'Іваненко', 'Я')).toBe(without);
      expect(avatarColor('Олена', 'Іваненко', ' - 1 ')).toBe(without);
    });

    it('counts letters only, like the names do', () => {
      expect(avatarColor('Олена', 'Іваненко', 'Добра справа')).toBe(avatarColor('Олена', 'Іваненко', 'Добра-справа!'));
    });

    it('still returns null for a person with no letters, whatever the organization', () => {
      expect(avatarColor('', '', 'Добра справа')).toBeNull();
    });
  });

  it('gives different colours to different people with the same initials', () => {
    expect(avatarColor('Олена', 'Іваненко')).not.toBe(avatarColor('Оксана', 'Ігнатенко'));
    expect(avatarColor('Олена', 'Іваненко')).not.toBe(avatarColor('Олег', 'Іваненко'));
  });

  it('is stable for the same name', () => {
    expect(avatarColor('Олена', 'Іваненко')).toBe(avatarColor('Олена', 'Іваненко'));
  });

  it('works with Latin names, where the sums are shorter than three hex digits', () => {
    // Ann: 175, Li: 181; поправка −n(110) + i(105) = −5 → 170 (aa) і 176 (b0) → aab0aa → #737873.
    expect(avatarColor('Ann', 'Li')).toBe('#737873');
  });

  it('appends the first-name sum again when there are fewer than six digits, and takes the modulus of a negative sum', () => {
    // Іван без прізвища: 2104 − 1085 = 1019 (3fb); прізвище: 0 − 1085 → 1085 (43d) → 3fb43d → #2f872e.
    expect(avatarColor('Іван', '')).toBe('#2f872e');
  });

  it('uses the last name alone when there is no first name', () => {
    // Іваненко: 2104; поправка 0 − 0 + о(1086) → імʼя 1086 (43e), прізвище 3190 (c76) → 43ec76 → #268743.
    expect(avatarColor('', 'Іваненко')).toBe('#268743');
  });

  it('counts letters only: spaces and hyphens are ignored', () => {
    expect(avatarColor('Марія-Анна', 'Коваль')).toBe(avatarColor('МаріяАнна', 'Коваль'));
    expect(avatarColor('Марія Анна', 'Коваль')).toBe(avatarColor('МаріяАнна', 'Коваль'));
  });

  it('copes with names of a single letter and with letters outside the basic plane', () => {
    expect(avatarColor('Я', 'Я')).toMatch(/^#[0-9a-f]{6}$/);
    expect(avatarColor('𝒜', 'B')).toMatch(/^#[0-9a-f]{6}$/);
  });

  it('keeps the contrast with both white and black at 4.5:1 or better', () => {
    const alphabet = Array.from(
      'АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюяABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz',
    );
    for (const a of alphabet) {
      for (const b of alphabet) {
        for (const [first, last] of [
          [a + b, b + a],
          [a, b + b + a],
        ]) {
          const colour = rgb(avatarColor(first, last)!);
          expect(contrastWithWhite(colour)).toBeGreaterThanOrEqual(4.5);
          expect(contrastWithBlack(colour)).toBeGreaterThanOrEqual(4.5);
        }
      }
    }
  });

  it('computes WCAG luminance and contrast', () => {
    expect(relativeLuminance([0, 0, 0])).toBe(0);
    expect(relativeLuminance([255, 255, 255])).toBeCloseTo(1, 5);
    expect(contrastWithWhite([255, 255, 255])).toBeCloseTo(1, 5);
    expect(contrastWithWhite([0, 0, 0])).toBeCloseTo(21, 5);
    expect(contrastWithBlack([0, 0, 0])).toBeCloseTo(1, 5);
    expect(contrastWithBlack([255, 255, 255])).toBeCloseTo(21, 5);
  });
});
