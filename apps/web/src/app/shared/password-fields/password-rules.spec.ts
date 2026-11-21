import { DEFAULT_PASSWORD_POLICY, PasswordPolicy, isStrongPassword, passwordRules } from './password-rules';

describe('password rules', () => {
  const met = (password: string, policy?: PasswordPolicy) =>
    passwordRules(policy)
      .filter((rule) => rule.test(password))
      .map((rule) => rule.key);

  describe('by default (the settings of the server out of the box)', () => {
    it('lists the five requirements in a fixed order, each with its icon, and the length as the parameter of its text', () => {
      expect(passwordRules().map((rule) => [rule.key, rule.icon, rule.params])).toEqual([
        ['length', '8', { count: 8 }],
        ['lowercase', 'a', {}],
        ['uppercase', 'A', {}],
        ['digit', '123', {}],
        ['special', '#', {}],
      ]);
      expect(DEFAULT_PASSWORD_POLICY.minLength).toBe(8);
    });

    it('ticks off each requirement on its own', () => {
      expect(met('')).toEqual([]);
      expect(met('abc')).toEqual(['lowercase']);
      expect(met('ABC')).toEqual(['uppercase']);
      expect(met('123')).toEqual(['digit']);
      expect(met('#!')).toEqual(['special']);
      expect(met('abcdefgh')).toEqual(['length', 'lowercase']);
    });

    it('counts only Latin letters: Cyrillic is neither a letter nor a digit for the server, so it is "special"', () => {
      expect(met('пароль')).toEqual(['special']);
    });

    it.each([
      ['Abcde1!x', true],
      ['Abcdef1!', true],
      ['Abcde1!', false], // 7 символів
      ['abcde1!x', false], // без великої
      ['ABCDE1!X', false], // без малої
      ['Abcdefg!', false], // без цифри
      ['Abcdefg1', false], // без спецсимвола
      ['', false],
    ])('isStrongPassword(%s) is %s', (password, expected) => {
      expect(isStrongPassword(password)).toBe(expected);
    });
  });

  describe('by the settings of an installation', () => {
    const relaxed: PasswordPolicy = { minLength: 12, requireLowercase: true, requireUppercase: false, requireDigit: false, requireSpecial: false };

    it('shows and checks only what the server requires, with its own length', () => {
      expect(passwordRules(relaxed).map((rule) => [rule.key, rule.icon, rule.params])).toEqual([
        ['length', '12', { count: 12 }],
        ['lowercase', 'a', {}],
      ]);
      expect(isStrongPassword('abcdefghijkl', relaxed)).toBe(true);
      expect(isStrongPassword('abcdefghijk', relaxed)).toBe(false);
      expect(isStrongPassword('ABCDEFGHIJKL', relaxed)).toBe(false);
    });

    it('always keeps the length, even when nothing else is required', () => {
      const lengthOnly: PasswordPolicy = { minLength: 1, requireLowercase: false, requireUppercase: false, requireDigit: false, requireSpecial: false };
      expect(passwordRules(lengthOnly).map((rule) => rule.key)).toEqual(['length']);
      expect(isStrongPassword('x', lengthOnly)).toBe(true);
      expect(isStrongPassword('', lengthOnly)).toBe(false);
    });
  });
});
