import { EMAIL_MAX_LENGTH, isEmail } from './email';

describe('isEmail', () => {
  it.each([
    ['a@b.org'],
    ['first.second@organization.org'],
    ['a+tag@b.co'],
    ['name@localhost'], // сервер не вимагає крапки в домені
    ['  a@b.org  '], // краї обрізаються, як і в запиті
  ])('takes %j: the server takes it too', (email) => {
    expect(isEmail(email)).toBe(true);
  });

  it.each([
    [''],
    ['   '],
    ['organization.org'], // без @
    ['@organization.org'], // @ перший
    ['a@'], // @ останній
    ['a@@b.org'], // два @
    ['a@b@c.org'],
  ])('rejects %j, as the server does', (email) => {
    expect(isEmail(email)).toBe(false);
  });

  it('rejects an address with a space inside, which the server would take but no mail reaches', () => {
    expect(isEmail('a b@c.org')).toBe(false);
  });

  it('rejects an address longer than the server allows', () => {
    const local = 'a'.repeat(EMAIL_MAX_LENGTH - '@b.org'.length);
    expect(isEmail(`${local}@b.org`)).toBe(true);
    expect(isEmail(`${local}a@b.org`)).toBe(false);
  });
});
