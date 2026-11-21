import { readJwtDemoExpiry, readJwtExpiry, readJwtSubject } from './jwt';

/** JWT без підпису з таким payload. */
function jwt(claims: object): string {
  return `h.${btoa(JSON.stringify(claims)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.s`;
}

describe('jwt helpers', () => {
  it('reads exp and sub from the payload', () => {
    const token = jwt({ sub: 'user-1', exp: 1795000000 });
    expect(readJwtExpiry(token)).toBe(1795000000);
    expect(readJwtSubject(token)).toBe('user-1');
  });

  describe('readJwtDemoExpiry', () => {
    it('reads demo_exp given as a number', () => {
      expect(readJwtDemoExpiry(jwt({ demo_exp: 1795003600 }))).toBe(1795003600);
    });

    it('reads demo_exp given as a numeric string', () => {
      expect(readJwtDemoExpiry(jwt({ demo_exp: '1795003600' }))).toBe(1795003600);
      expect(readJwtDemoExpiry(jwt({ demo_exp: ' 1795003600 ' }))).toBe(1795003600);
    });

    it.each([
      ['a token without the claim', jwt({ sub: 'user-1', exp: 1795000000 })],
      ['a claim that is not a time', jwt({ demo_exp: 'soon' })],
      ['an empty claim', jwt({ demo_exp: '' })],
      ['a zero claim', jwt({ demo_exp: 0 })],
      ['a negative claim', jwt({ demo_exp: -1 })],
      ['a boolean claim', jwt({ demo_exp: true })],
      ['a null claim', jwt({ demo_exp: null })],
      ['something that is not a JWT', 'opaque-token'],
      ['no token', null],
    ])('gives null for %s', (_name, token) => {
      expect(readJwtDemoExpiry(token)).toBeNull();
    });
  });
});
