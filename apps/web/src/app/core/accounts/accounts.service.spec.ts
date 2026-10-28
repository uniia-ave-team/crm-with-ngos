import { TestBed } from '@angular/core/testing';

import { AccountsService } from './accounts.service';

describe('AccountsService', () => {
  const server = 'http://localhost:5065';
  const other = 'http://crm.example.org:5065';
  const tokens = { accessToken: 'access', refreshToken: 'refresh' };

  let service: AccountsService;

  beforeEach(() => {
    localStorage.clear();
    service = TestBed.inject(AccountsService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  /** Нова «вкладка»: сервіс, що читає те, що вже лежить у сховищі. */
  function reopen(): AccountsService {
    TestBed.resetTestingModule();
    return TestBed.inject(AccountsService);
  }

  const record = (id: string) => JSON.parse(localStorage.getItem(`yavir.account.${id}`)!);

  describe('storing', () => {
    it('keeps an account as its own record plus an index and an active pointer', () => {
      const account = service.add({ server, ...tokens, identity: 'user-1', userId: 'user-1', firstName: 'Перший', lastName: 'Другий', ngoName: 'Організація', ngoLogoUrl: '/files/logo.png' });

      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([account.id]);
      expect(localStorage.getItem('yavir.activeAccount')).toBe(account.id);
      expect(record(account.id)).toMatchObject({
        server,
        userId: 'user-1',
        firstName: 'Перший',
        lastName: 'Другий',
        ngoName: 'Організація',
        ngoLogoUrl: '/files/logo.png',
        accessToken: 'access',
        refreshToken: 'refresh',
      });
    });

    it('stores the connection address with every account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      service.add({ server: other, ...tokens, identity: 'b' });
      expect(service.accounts().map((a) => a.server)).toEqual([server, other]);
    });

    it('makes the added account active and keeps the order of adding', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      expect(service.activeId()).toBe(second.id);
      expect(service.accounts().map((a) => a.id)).toEqual([first.id, second.id]);
    });

    it('tells apart the same user on two servers and two users on one server', () => {
      const ids = new Set([
        AccountsService.accountId(server, 'u1'),
        AccountsService.accountId(other, 'u1'),
        AccountsService.accountId(server, 'u2'),
      ]);
      expect(ids.size).toBe(3);
    });

    it('puts the server domain, reduced to Latin letters, into the id', () => {
      expect(AccountsService.accountId(server, 'u1')).toMatch(/^localhost-[0-9a-f]+$/);
      expect(AccountsService.accountId(other, 'u1')).toMatch(/^crmexampleorg-[0-9a-f]+$/);
      expect(AccountsService.accountId('https://My-Server2.test:8443', 'u1')).toMatch(/^myservertest-[0-9a-f]+$/);
    });

    it('falls back to "server" when the domain has no Latin letters, e.g. an IP address', () => {
      expect(AccountsService.accountId('http://192.168.0.7:5065', 'u1')).toMatch(/^server-[0-9a-f]+$/);
    });

    it('keeps the id stable and safe for a storage key', () => {
      expect(AccountsService.accountId(server, 'u1')).toBe(AccountsService.accountId(server, 'u1'));
      expect(AccountsService.accountId(server, 'user/with spaces.and:colons')).toMatch(/^[a-z]+-[0-9a-f]+$/);
    });

    it('separates accounts whose ids would collide with a numeric suffix', () => {
      // Однакові `id`, але різні акаунти — імітуємо колізію відбитка.
      const spy = vi.spyOn(AccountsService, 'accountId').mockReturnValue('localhost-deadbeef');
      const first = service.add({ server, ...tokens, identity: 'u1' });
      const second = service.add({ server, ...tokens, identity: 'u2' });
      const third = service.add({ server, ...tokens, identity: 'u3' });
      spy.mockRestore();

      expect([first.id, second.id, third.id]).toEqual(['localhost-deadbeef', 'localhost-deadbeef-2', 'localhost-deadbeef-3']);
      // Повторний вхід того самого користувача оновлює його запис, а не створює новий.
      const again = service.add({ server, ...tokens, identity: 'u2' });
      expect(again.id).toBe('localhost-deadbeef-2');
      expect(service.accounts().length).toBe(3);
    });

    it('updates an existing account instead of duplicating it, keeping its snapshot', () => {
      const first = service.add({ server, ...tokens, identity: 'a', firstName: 'Перший', ngoName: 'Організація' });
      const again = service.add({ server, accessToken: 'new', refreshToken: 'new-r', identity: 'a' });
      expect(again.id).toBe(first.id);
      expect(service.accounts()).toHaveLength(1);
      expect(service.active()).toMatchObject({ accessToken: 'new', firstName: 'Перший', ngoName: 'Організація', addedAt: first.addedAt });
    });

    it('updates the fields of a record, in memory and in storage', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      service.update(account.id, { firstName: 'Перший', ngoName: 'Організація', refreshToken: 'r2' });
      expect(service.active()).toMatchObject({ firstName: 'Перший', ngoName: 'Організація', refreshToken: 'r2' });
      expect(record(account.id)).toMatchObject({ firstName: 'Перший', ngoName: 'Організація', refreshToken: 'r2' });
    });

    it('keeps the NGO logo when the account is added again, and updates it with the record', () => {
      const first = service.add({ server, ...tokens, identity: 'a', ngoName: 'Організація', ngoLogoUrl: 'https://x/logo.png' });
      service.add({ server, accessToken: 'new', refreshToken: 'r2', identity: 'a' });
      expect(service.active()).toMatchObject({ id: first.id, ngoLogoUrl: 'https://x/logo.png' });

      service.update(first.id, { ngoLogoUrl: null });
      expect(service.active()?.ngoLogoUrl).toBeNull();
      expect(record(first.id).ngoLogoUrl).toBeNull();
    });

    it('stores the permissions in the record, keeps them when the account is added again and starts with none', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      expect(first.permissions).toEqual([]);

      service.update(first.id, { permissions: ['ViewUser', 'UpdateUser'] });
      expect(service.active()?.permissions).toEqual(['ViewUser', 'UpdateUser']);
      expect(record(first.id).permissions).toEqual(['ViewUser', 'UpdateUser']);

      service.add({ server, accessToken: 'new', refreshToken: 'r2', identity: 'a' });
      expect(service.active()?.permissions).toEqual(['ViewUser', 'UpdateUser']);
      expect(reopen().active()?.permissions).toEqual(['ViewUser', 'UpdateUser']);
    });

    describe('the thumbnails of the avatar and of the NGO logo', () => {
      const thumb = (url: string) => ({ url, data: 'data:image/webp;base64,AAAA' });

      it('starts with none', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        expect(account.avatarThumb).toBeNull();
        expect(account.ngoLogoThumb).toBeNull();
      });

      it('stores them in the record with the update, in memory and in storage', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        service.update(account.id, { avatarThumb: thumb('/a.png'), ngoLogoThumb: thumb('/logo.png') });

        expect(service.active()).toMatchObject({ avatarThumb: thumb('/a.png'), ngoLogoThumb: thumb('/logo.png') });
        expect(record(account.id)).toMatchObject({ avatarThumb: thumb('/a.png'), ngoLogoThumb: thumb('/logo.png') });
      });

      it('keeps them when the account is added again (a new sign-in)', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        service.update(account.id, { avatarThumb: thumb('/a.png'), ngoLogoThumb: thumb('/logo.png') });

        const again = service.add({ server, accessToken: 'new', refreshToken: 'new', identity: 'a' });
        expect(again.avatarThumb).toEqual(thumb('/a.png'));
        expect(again.ngoLogoThumb).toEqual(thumb('/logo.png'));
      });

      it('can be taken away with null', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        service.update(account.id, { avatarThumb: thumb('/a.png') });
        service.update(account.id, { avatarThumb: null });
        expect(record(account.id).avatarThumb).toBeNull();
      });

      it('are read back in a new tab', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        service.update(account.id, { avatarThumb: thumb('/a.png') });
        expect(reopen().active()?.avatarThumb).toEqual(thumb('/a.png'));
      });
    });

    it('ignores an update of an unknown account', () => {
      service.update('nope', { firstName: 'X' });
      expect(service.accounts()).toHaveLength(0);
      expect(localStorage.length).toBe(0);
    });
  });

  describe('e-mail and outdated accounts', () => {
    it('stores the e-mail of the account, since it cannot be fetched from /users/me', () => {
      const account = service.add({ server, ...tokens, identity: 'a', email: 'a@b.org' });
      expect(record(account.id).email).toBe('a@b.org');
      // Повторний запис без пошти її не стирає.
      service.add({ server, ...tokens, identity: 'a' });
      expect(service.active()?.email).toBe('a@b.org');
    });

    it('starts as not outdated', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      expect(account.outdated).toBe(false);
      expect(record(account.id).outdated).toBe(false);
    });

    it('marks an account outdated without deleting it, and another valid account becomes the active one', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });

      service.markOutdated(second.id);
      expect(service.accounts()).toHaveLength(2);
      expect(record(second.id).outdated).toBe(true);
      expect(record(second.id).refreshToken).toBe('refresh');
      expect(record(first.id).outdated).toBe(false);
      expect(service.activeId()).toBe(first.id);
      expect(localStorage.getItem('yavir.activeAccount')).toBe(first.id);
    });

    it('marks an account outdated for a rejected session by default (the unavailable reason is false)', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });

      service.markOutdated(first.id);
      expect(record(first.id)).toMatchObject({ outdated: true, unavailable: false });

      service.markOutdated(second.id, true);
      // Чинних не лишилося (перший теж застарів), тож памʼять очищена.
      expect(service.accounts()).toHaveLength(0);
    });

    it('stores the unavailable reason while another valid account keeps the memory', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.add({ server, ...tokens, identity: 'c' });

      service.markOutdated(second.id, true);
      expect(record(second.id)).toMatchObject({ outdated: true, unavailable: true });
      expect(record(first.id)).toMatchObject({ outdated: false, unavailable: false });

      // Повторний вхід у цей акаунт повертає його в чинні й скидає причину.
      service.add({ server, accessToken: 'new', refreshToken: 'r2', identity: 'b' });
      expect(record(second.id)).toMatchObject({ outdated: false, unavailable: false });
    });

    it('takes the first valid account in the order of adding, skipping the outdated ones', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      const third = service.add({ server, ...tokens, identity: 'c' });
      const fourth = service.add({ server, ...tokens, identity: 'd' });
      service.markOutdated(first.id);
      service.markOutdated(third.id);

      service.markOutdated(fourth.id);
      expect(service.activeId()).toBe(second.id);
    });

    it('keeps the active account when another one is marked outdated', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.setActive(first.id);
      service.markOutdated(second.id);
      expect(service.activeId()).toBe(first.id);
    });

    it('clears the whole memory once every account is outdated', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      localStorage.setItem('yavir.language', 'uk');

      service.markOutdated(first.id);
      expect(localStorage.length).toBeGreaterThan(0);
      service.markOutdated(second.id);

      expect(localStorage.length).toBe(0);
      expect(service.accounts()).toHaveLength(0);
      expect(service.activeId()).toBeNull();
    });

    it('ignores marking an unknown account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      service.markOutdated('ghost');
      expect(service.accounts()[0].outdated).toBe(false);
    });

    it('makes an outdated account valid again when the user signs in to it again', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      service.add({ server, ...tokens, identity: 'b' });
      service.markOutdated(account.id);
      expect(service.accounts().find((a) => a.id === account.id)?.outdated).toBe(true);

      service.add({ server, accessToken: 'new', refreshToken: 'new-r', identity: 'a' });
      expect(service.accounts().find((a) => a.id === account.id)?.outdated).toBe(false);
      expect(service.activeId()).toBe(account.id);
    });

    it('reads records without the new fields as valid accounts with no e-mail', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r' }));
      expect(reopen().accounts()[0]).toMatchObject({ outdated: false, email: '' });
    });
  });

  describe('removing and switching', () => {
    it('removes the record and the index entry, and another valid account becomes the active one', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });

      service.remove(second.id);
      expect(service.accounts().map((a) => a.id)).toEqual([first.id]);
      expect(localStorage.getItem(`yavir.account.${second.id}`)).toBeNull();
      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([first.id]);
      expect(service.activeId()).toBe(first.id);
      expect(localStorage.getItem('yavir.activeAccount')).toBe(first.id);
    });

    it('wipes the whole memory when the removed account was the last valid one', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      localStorage.setItem('yavir.language', 'uk');
      service.markOutdated(first.id);

      service.remove(second.id);
      expect(localStorage.length).toBe(0);
      expect(service.accounts()).toHaveLength(0);
      expect(service.activeId()).toBeNull();
    });

    it('wipes the memory when the only account is removed', () => {
      const only = service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem('yavir.language', 'uk');
      service.remove(only.id);
      expect(localStorage.length).toBe(0);
      expect(service.activeId()).toBeNull();
    });

    it('keeps the active account when another one is removed', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.remove(first.id);
      expect(service.activeId()).toBe(second.id);
    });

    it('switches the active account and rejects an unknown one', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      service.add({ server, ...tokens, identity: 'b' });

      service.setActive(first.id);
      expect(service.activeId()).toBe(first.id);
      expect(localStorage.getItem('yavir.activeAccount')).toBe(first.id);

      service.setActive('unknown');
      expect(service.activeId()).toBe(first.id);

      service.setActive(null);
      expect(service.active()).toBeNull();
      expect(localStorage.getItem('yavir.activeAccount')).toBeNull();
    });

    it('wipes the whole localStorage and forgets every account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem('yavir.language', 'uk');
      service.wipe();
      expect(localStorage.length).toBe(0);
      expect(service.accounts()).toHaveLength(0);
      expect(service.activeId()).toBeNull();
    });
  });

  describe('reading', () => {
    it('restores the accounts and the active one in a new tab', () => {
      service.add({ server, ...tokens, identity: 'a', firstName: 'Перший' });
      const second = service.add({ server: other, ...tokens, identity: 'b' });
      service.setActive(second.id);

      const reopened = reopen();
      expect(reopened.accounts()).toHaveLength(2);
      expect(reopened.activeId()).toBe(second.id);
      expect(reopened.accounts()[0].firstName).toBe('Перший');
    });

    it('drops index entries without a record and ignores broken records', () => {
      const good = service.add({ server, ...tokens, identity: 'a' });
      const broken = service.add({ server, ...tokens, identity: 'b' });
      localStorage.setItem(`yavir.account.${broken.id}`, '{not json');
      localStorage.setItem('yavir.accounts', JSON.stringify([good.id, broken.id, 'ghost']));

      const reopened = reopen();
      expect(reopened.accounts().map((a) => a.id)).toEqual([good.id]);
      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([good.id]);
    });

    it('forgets an active pointer to a missing account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem('yavir.activeAccount', 'ghost');
      expect(reopen().activeId()).toBeNull();
    });

    it('fills fields missing in an older or partial record with defaults', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r' }));
      localStorage.setItem('yavir.activeAccount', 'x');
      expect(reopen().active()).toMatchObject({
        firstName: '',
        lastName: '',
        avatarUrl: null,
        ngoName: null,
        ngoLogoUrl: null,
        permissions: [],
        userId: '',
      });
    });

    it.each([
      ['a string', 'data:image/webp;base64,AAAA'],
      ['a number', 5],
      ['an object without the data', { url: '/a.png' }],
      ['an object without the address', { data: 'data:image/webp;base64,AAAA' }],
      ['data that is not an image link', { url: '/a.png', data: 'javascript:alert(1)' }],
      ['an address that is not a string', { url: 5, data: 'data:image/webp;base64,AAAA' }],
    ])('ignores a damaged thumbnail in a record (%s)', (_name, damaged) => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...record(account.id), avatarThumb: damaged, ngoLogoThumb: damaged }));
      const reopened = reopen();
      expect(reopened.active()?.avatarThumb).toBeNull();
      expect(reopened.active()?.ngoLogoThumb).toBeNull();
    });

    it('ignores a damaged list of permissions in a record', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem(
        'yavir.account.x',
        JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r', permissions: ['ViewUser', 7, null] }),
      );
      localStorage.setItem('yavir.activeAccount', 'x');
      expect(reopen().active()?.permissions).toEqual(['ViewUser']);

      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r', permissions: 'ViewUser' }));
      expect(reopen().active()?.permissions).toEqual([]);
    });

    it('copes with a broken index', () => {
      localStorage.setItem('yavir.accounts', 'garbage');
      expect(reopen().accounts()).toEqual([]);
    });

    it('works in memory when localStorage is unavailable', () => {
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('denied');
      });
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('denied');
      });
      const account = service.add({ server, ...tokens, identity: 'a' });
      expect(service.activeId()).toBe(account.id);
      expect(service.active()?.accessToken).toBe('access');
    });
  });

  describe('other tabs', () => {
    it('follows the storage events of the records, the index and the active pointer', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...record(account.id), refreshToken: 'from-other-tab' }));
      window.dispatchEvent(new StorageEvent('storage', { key: `yavir.account.${account.id}` }));
      expect(service.active()?.refreshToken).toBe('from-other-tab');

      localStorage.setItem('yavir.accounts', '[]');
      localStorage.removeItem(`yavir.account.${account.id}`);
      window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.accounts' }));
      expect(service.accounts()).toHaveLength(0);
    });

    it('ignores storage events of unrelated keys', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...record(account.id), refreshToken: 'changed' }));
      window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.language' }));
      expect(service.active()?.refreshToken).toBe('refresh');
    });

    it('takes the record that another tab refreshed, and only if it differs', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      expect(service.syncFromStorage(account.id)).toBeNull();

      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...record(account.id), refreshToken: 'newer', accessToken: 'newer-a' }));
      const synced = service.syncFromStorage(account.id);
      expect(synced?.refreshToken).toBe('newer');
      expect(service.active()?.accessToken).toBe('newer-a');
      expect(service.syncFromStorage(account.id)).toBeNull();
    });
  });
});
