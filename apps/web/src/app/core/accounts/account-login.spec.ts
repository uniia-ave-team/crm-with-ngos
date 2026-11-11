import { RequestFailure } from '../http/request-failure';
import { accountToReplace, loginErrorKey, sameEmail } from './account-login';
import { StoredAccount } from './accounts.service';

describe('account login helpers', () => {
  describe('loginErrorKey', () => {
    it.each<[RequestFailure, string]>([
      ['invalid', 'invalid'],
      ['unauthorized', 'unauthorized'],
      ['unreachable', 'unreachable'],
      ['forbidden', 'error'],
      ['error', 'error'],
    ])('gives the text for %s', (failure, key) => {
      expect(loginErrorKey(failure)).toBe(key);
    });
  });

  describe('sameEmail', () => {
    it('ignores the case and the spaces around', () => {
      expect(sameEmail(' A@Organization.org ', 'a@organization.org')).toBe(true);
      expect(sameEmail('a@organization.org', 'b@organization.org')).toBe(false);
    });
  });

  describe('accountToReplace', () => {
    const accounts = [{ id: 'a', email: 'a@organization.org' }, { id: 'b', email: '' }] as StoredAccount[];

    it('gives the key only when the key points to a stored account with the same e-mail', () => {
      expect(accountToReplace(accounts, 'a', 'A@organization.org')).toBe('a');
      expect(accountToReplace(accounts, 'a', 'other@organization.org')).toBeUndefined();
      expect(accountToReplace(accounts, 'ghost', 'a@organization.org')).toBeUndefined();
      expect(accountToReplace(accounts, undefined, 'a@organization.org')).toBeUndefined();
      expect(accountToReplace(accounts, null, 'a@organization.org')).toBeUndefined();
    });
  });
});
