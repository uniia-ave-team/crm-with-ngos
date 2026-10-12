import { PHONE_MAX_LENGTH, isPhoneNumber } from './phone-number';

describe('isPhoneNumber', () => {
  it.each([
    '+380123456789',
    '+380 12 345 67 89',
    '+38 (050) 111-22-33',
    '050 111 22 33',
    '0501112233',
    '050.111.22.33',
    '+1 202 555 0147',
    '  +380501112233  ',
    '1234567', // 7 цифр — найкоротший
    '+123456789012345', // 15 цифр — найдовший
  ])('accepts %s', (value) => {
    expect(isPhoneNumber(value)).toBe(true);
  });

  it.each([
    ['letters', 'abc'],
    ['an ordinary word with digits', 'тел 0501112233'],
    ['too few digits', '123456'],
    ['too many digits', '+1234567890123456'],
    ['a plus in the middle', '050+1112233'],
    ['two pluses', '++380501112233'],
    ['only separators', '--- ()'],
    ['an email', 'a@b.org'],
    ['a letter among the digits', '050111223a'],
    ['an empty value', ''],
    ['spaces only', '   '],
  ])('rejects %s', (_name, value) => {
    expect(isPhoneNumber(value)).toBe(false);
  });

  it('rejects a value longer than the server accepts', () => {
    const long = '0'.repeat(PHONE_MAX_LENGTH + 1);
    expect(isPhoneNumber(long)).toBe(false);
  });
});
