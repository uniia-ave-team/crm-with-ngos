import { StoredAccount } from '../../core/accounts/accounts.service';
import { AccountTile, accountCaption, accountTooltip, toAccountTiles } from './account-tile';

describe('account tile helpers', () => {
  const base: AccountTile = { id: 'a', firstName: 'Перший', lastName: 'Другий', avatarUrl: null };
  const named: AccountTile = { ...base, patronymic: 'Третій', ngoName: 'Організація' };
  const unavailableText = 'сервер не відповідає';
  const outdatedText = 'сеанс закінчився';

  describe('accountCaption', () => {
    it('puts the surname, the name and the patronymic, a dash and the NGO into the heading', () => {
      expect(accountCaption(named, unavailableText, outdatedText)).toEqual({ heading: 'Другий Перший Третій — Організація', status: '' });
    });

    it.each([
      [{ outdated: true }, outdatedText],
      [{ unavailable: true }, unavailableText],
      [{ outdated: true, unavailable: true }, unavailableText],
    ])('gives the status of an account in trouble (%o), the server problem first', (flags, status) => {
      expect(accountCaption({ ...named, ...flags }, unavailableText, outdatedText).status).toBe(status);
    });

    it('leaves out what is missing', () => {
      expect(accountCaption({ ...base, ngoName: 'Організація' }, '', '').heading).toBe('Другий Перший — Організація');
      expect(accountCaption({ ...base, patronymic: ' ' }, '', '').heading).toBe('Другий Перший');
      expect(accountCaption({ ...base, firstName: ' ', lastName: '', ngoName: 'Організація' }, '', '').heading).toBe('Організація');
      expect(accountCaption({ ...base, firstName: ' ', lastName: ' ' }, '', '').heading).toBe('');
    });
  });

  describe('accountTooltip', () => {
    it('joins the heading and the status with a line break, and gives null when there is nothing to show', () => {
      expect(accountTooltip({ ...named, outdated: true }, unavailableText, outdatedText)).toBe(`Другий Перший Третій — Організація\n${outdatedText}`);
      expect(accountTooltip(named, unavailableText, outdatedText)).toBe('Другий Перший Третій — Організація');
      expect(accountTooltip({ id: 'a', firstName: ' ', lastName: ' ', avatarUrl: null }, unavailableText, outdatedText)).toBeNull();
    });
  });

  describe('toAccountTiles', () => {
    const record = (patch: Partial<StoredAccount>): StoredAccount =>
      ({
        id: 'a',
        firstName: 'Перший',
        lastName: 'Другий',
        patronymic: 'Третій',
        avatarUrl: 'avatar-1',
        avatarThumb: { url: 'avatar-1', data: 'data:image/webp;base64,x' },
        ngoName: 'Організація',
        ngoLogoUrl: 'logo-1',
        ngoLogoThumb: { url: 'logo-1', data: 'data:image/webp;base64,y' },
        outdated: false,
        unavailable: false,
        ...patch,
      }) as StoredAccount;

    it('takes the data of the snapshot, the images from the thumbnails, and marks the active one', () => {
      const tiles = toAccountTiles([record({}), record({ id: 'b' })], 'b');
      expect(tiles[0]).toMatchObject({
        id: 'a',
        firstName: 'Перший',
        lastName: 'Другий',
        patronymic: 'Третій',
        avatarUrl: 'data:image/webp;base64,x',
        logoUrl: 'data:image/webp;base64,y',
        ngoName: 'Організація',
        active: false,
      });
      expect(tiles.map((t) => t.active)).toEqual([false, true]);
    });

    it('does not use a thumbnail made from another address, or a missing one', () => {
      const [tile] = toAccountTiles([record({ avatarUrl: 'avatar-2', ngoLogoThumb: null })], null);
      expect(tile.avatarUrl).toBeNull();
      expect(tile.logoUrl).toBeNull();
    });

    it('carries the flags of the account', () => {
      const [tile] = toAccountTiles([record({ outdated: true, unavailable: true })], null);
      expect(tile).toMatchObject({ outdated: true, unavailable: true });
    });
  });
});
