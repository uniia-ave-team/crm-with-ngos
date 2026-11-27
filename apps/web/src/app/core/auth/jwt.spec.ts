import { readJwtDemoExpiry, readJwtExpiry, readJwtPayload, readJwtRoleIds, readJwtSubject } from './jwt';

/** JWT без підпису з таким payload (base64url, без доповнення). */
function jwt(payload: unknown): string {
  const body = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `header.${body}.signature`;
}

describe('jwt', () => {
  describe('readJwtPayload', () => {
    it('reads the payload of a token', () => {
      expect(readJwtPayload(jwt({ sub: 'user-1', exp: 100 }))).toEqual({ sub: 'user-1', exp: 100 });
    });

    it('reads a payload written in the URL-safe alphabet (with "-" and "_")', () => {
      // Ці значення дають у звичайному base64 символи «+» і «/», які в JWT замінено.
      const payload = { a: '>>>???', b: '~~~~' };
      const token = jwt(payload);
      expect(token.split('.')[1]).toMatch(/[-_]/);
      expect(readJwtPayload(token)).toEqual(payload);
    });

    it.each([
      ['no token', null],
      ['an undefined token', undefined],
      ['an empty string', ''],
      ['a string without dots', 'not-a-jwt'],
      ['an empty payload part', 'header..signature'],
      ['a payload that is not base64', 'header.%%%.signature'],
      ['a payload that is not JSON', `header.${btoa('not json')}.signature`],
      ['a payload that is not an object', jwt('text')],
      ['a null payload', jwt(null)],
    ])('gives null for %s', (_name, token) => {
      expect(readJwtPayload(token)).toBeNull();
    });
  });

  describe('readJwtExpiry', () => {
    it('gives the exp of the token in seconds', () => {
      expect(readJwtExpiry(jwt({ exp: 1_700_000_000 }))).toBe(1_700_000_000);
    });

    it.each([
      ['there is no exp', jwt({ sub: 'user-1' })],
      ['exp is not a number', jwt({ exp: '1700000000' })],
      ['the token cannot be read', 'broken'],
      ['there is no token', null],
    ])('gives null when %s', (_name, token) => {
      expect(readJwtExpiry(token)).toBeNull();
    });
  });

  describe('readJwtSubject', () => {
    it('gives the sub of the token', () => {
      expect(readJwtSubject(jwt({ sub: 'user-1' }))).toBe('user-1');
    });

    it.each([
      ['there is no sub', jwt({ exp: 1 })],
      ['sub is empty', jwt({ sub: '' })],
      ['sub is not a string', jwt({ sub: 5 })],
      ['the token cannot be read', 'broken'],
      ['there is no token', undefined],
    ])('gives null when %s', (_name, token) => {
      expect(readJwtSubject(token)).toBeNull();
    });
  });

  describe('readJwtRoleIds', () => {
    it.each([
      ['one role (a string)', { role_id: 'r1' }, ['r1']],
      ['several roles (an array)', { role_id: ['r1', 'r2'] }, ['r1', 'r2']],
      ['no roles (no field)', { sub: 'u1' }, []],
      ['values that are not ids', { role_id: ['r1', 7, ''] }, ['r1']],
    ])('gives the ids for %s', (_name, payload, expected) => {
      expect(readJwtRoleIds(jwt(payload))).toEqual(expected);
    });

    it('gives null for a token that cannot be read', () => {
      expect(readJwtRoleIds('not-a-jwt')).toBeNull();
    });
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
