import { PASSWORD_MIN_LENGTH, PASSWORD_RULES, isStrongPassword } from './password-rules';

describe('password rules', () => {
  const met = (password: string) => PASSWORD_RULES.filter((rule) => rule.test(password)).map((rule) => rule.key);

  it('lists the five requirements of the server in a fixed order, each with its icon', () => {
    expect(PASSWORD_RULES.map((rule) => [rule.key, rule.icon])).toEqual([
      ['length', '8'],
      ['lowercase', 'a'],
      ['uppercase', 'A'],
      ['digit', '123'],
      ['special', '#'],
    ]);
    expect(PASSWORD_MIN_LENGTH).toBe(8);
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
