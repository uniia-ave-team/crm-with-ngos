import { linkWithToken, tokenFromFragment } from './link-token';

describe('link token', () => {
  it('puts the token into the fragment of the link (not the query), encoded, and reads it back', () => {
    const link = linkWithToken('https://ngo.example.org', '/join', 'a b+c/d=e&f?g');
    expect(link).toBe(`https://ngo.example.org/join#token=${encodeURIComponent('a b+c/d=e&f?g')}`);
    expect(new URL(link).search).toBe('');
    expect(tokenFromFragment(new URL(link).hash.slice(1))).toBe('a b+c/d=e&f?g');
  });

  it('has no token without a fragment or without the token in it', () => {
    expect(tokenFromFragment(null)).toBeUndefined();
    expect(tokenFromFragment('')).toBeUndefined();
    expect(tokenFromFragment('other=1')).toBeUndefined();
  });
});
