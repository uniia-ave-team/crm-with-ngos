import { avatarColor, contrastWithBlack, contrastWithWhite, relativeLuminance } from './avatar-color';

const rgb = (hex: string) =>
  [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)) as [number, number, number];

describe('avatarColor', () => {
  it('returns null when there are no letters at all', () => {
    expect(avatarColor('', '')).toBeNull();
    expect(avatarColor('  ', '- 1')).toBeNull();
  });

  it('takes the first two letters of each name, subtracts the last letter of the first name and adds the last letter of the last name', () => {
    // Пізній: П(1055)+і(1110)=2165; Другий: Д(1044)+р(1088)=2132.
    // Поправка: −й(1081) + й(1081) = 0 → 2165 = 875 і 2132 = 854 → 875854.
    // З білим контраст 5.9:1 (ок), з чорним лише 3.6:1 → висвітлено до #946a66.
    expect(avatarColor('Пізній', 'Другий')).toBe('#946a66');
  });

  it('shifts a too bright colour towards black (first shift) and a too dark one towards white (second shift)', () => {
    // 83884a: контраст із білим 3.8:1 → затемнено до #767a42 (4.5:1 з білим).
    expect(avatarColor('Єдиний', 'Вишуканий')).toBe('#767a42');
    // 854854: з білим 6.9:1, з чорним 3.1:1 → висвітлено до #9a6771.
    expect(avatarColor('Перший', 'Другий')).toBe('#9a6771');
    // 85f844: з білим лише 1.4:1 → сильно затемнено до #478524.
    expect(avatarColor('Прекрасний', 'Дальній')).toBe('#478524');
  });

  describe('with the name of the organization', () => {
    it('adds the 5th letter to the first-name sum and the second-to-last letter to the last-name sum', () => {
      // Організація: 5-та літера н(1085), передостання і(1110).
      // Перший: 2132 + 1085 + 0 = 3217 (c91); Другий: 2132 + 1110 + 0 = 3242 (caa) → c91caa.
      // З білим контраст 5.0:1 (ок), з чорним 4.2:1 → #cc29af після зсувів.
      expect(avatarColor('Перший', 'Другий', 'Організація')).toBe('#cc29af');
    });

    it('adds 0 for a missing 5th letter, but still takes the second-to-last one', () => {
      // Ідея: 4 літери, 5-ї нема (0); передостання е(1077) → 2132 (854) і 3209 (c89) → 854c89.
      expect(avatarColor('Перший', 'Другий', 'Ідея')).toBe('#956398');
    });

    it('adds 0 to both sums when the name is missing or has a single letter', () => {
      const without = avatarColor('Перший', 'Другий');
      expect(avatarColor('Перший', 'Другий', '')).toBe(without);
      expect(avatarColor('Перший', 'Другий', 'Я')).toBe(without);
      expect(avatarColor('Перший', 'Другий', ' - 1 ')).toBe(without);
    });

    it('counts letters only, like the names do', () => {
      expect(avatarColor('Перший', 'Другий', 'Організація')).toBe(avatarColor('Перший', 'Другий', 'Організ-ація!'));
    });

    it('still returns null for a person with no letters, whatever the organization', () => {
      expect(avatarColor('', '', 'Організація')).toBeNull();
    });
  });

  it('gives different colours to different people with the same initials', () => {
    expect(avatarColor('Перший', 'Другий')).not.toBe(avatarColor('Прекрасний', 'Дальній'));
    expect(avatarColor('Перший', 'Другий')).not.toBe(avatarColor('Пізній', 'Другий'));
  });

  it('is stable for the same name', () => {
    expect(avatarColor('Перший', 'Другий')).toBe(avatarColor('Перший', 'Другий'));
  });

  it('works with Latin names, where the sums are shorter than three hex digits', () => {
    // Abc: A(65)+b(98)=163, Xy: X(88)+y(121)=209; поправка −c(99) + y(121) = 22 → 185 (b9) і 231 (e7) → b9e7b9 → #647d64.
    expect(avatarColor('Abc', 'Xy')).toBe('#647d64');
  });

  it('appends the first-name sum again when there are fewer than six digits, and takes the modulus of a negative sum', () => {
    // Інший без прізвища: І(1030)+н(1085)=2115, поправка 0 − й(1081) → 1034 (40a); прізвище: 0 − 1081 → 1081 (439) → 40a439 → #35872f.
    expect(avatarColor('Інший', '')).toBe('#35872f');
  });

  it('uses the last name alone when there is no first name', () => {
    // Інший: 2115; поправка 0 − 0 + й(1081) → імʼя 1081 (439), прізвище 2115 + 1081 = 3196 (c7c) → 439c7c → #388368.
    expect(avatarColor('', 'Інший')).toBe('#388368');
  });

  it('counts letters only: spaces and hyphens are ignored', () => {
    expect(avatarColor('Мала-Ласка', 'Кінець')).toBe(avatarColor('МалаЛаска', 'Кінець'));
    expect(avatarColor('Мала Ласка', 'Кінець')).toBe(avatarColor('МалаЛаска', 'Кінець'));
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
