import { TestBed } from '@angular/core/testing';

import { AccountsService } from './accounts.service';
import { addAccountWithSnapshot } from './accounts.testing';

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
    it('keeps an account as its own record plus an index, and the active pointer in the storage of the tab', () => {
      const account = addAccountWithSnapshot(service, { server, ...tokens, identity: 'user-1', userId: 'user-1' }, { firstName: 'Перший', lastName: 'Другий', ngoName: 'Організація', ngoLogoUrl: '/files/logo.png' });

      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([account.id]);
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe(account.id);
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

    it('puts the server domain, reduced to its letters, into the id', () => {
      expect(AccountsService.accountId(server, 'u1')).toMatch(/^localhost-[0-9a-f]+$/);
      expect(AccountsService.accountId(other, 'u1')).toMatch(/^crmexampleorg-[0-9a-f]+$/);
      expect(AccountsService.accountId('https://My-Server2.test:8443', 'u1')).toMatch(/^myservertest-[0-9a-f]+$/);
    });

    it('keeps the Ukrainian letters of a Cyrillic domain (the .укр zone), however the address is written', () => {
      expect(AccountsService.accountId('https://приклад.укр:5065', 'u1')).toMatch(/^прикладукр-[0-9a-f]+$/);
      expect(AccountsService.accountId('https://xn--80aikifvh.xn--j1amh:5065', 'u1')).toMatch(/^прикладукр-[0-9a-f]+$/);
      expect(AccountsService.accountId('https://crm.Їжак-Ґанок.укр', 'u1')).toMatch(/^crmїжакґанокукр-[0-9a-f]+$/);
    });

    it('falls back to "server" when the domain has no letters, e.g. an IP address', () => {
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
      const first = addAccountWithSnapshot(service, { server, ...tokens, identity: 'a' }, { firstName: 'Перший', ngoName: 'Організація' });
      const again = service.add({ server, accessToken: 'new', refreshToken: 'new-r', identity: 'a' });
      expect(again.id).toBe(first.id);
      expect(service.accounts()).toHaveLength(1);
      expect(service.active()).toMatchObject({ accessToken: 'new', firstName: 'Перший', ngoName: 'Організація', addedAt: first.addedAt });
    });

    describe('overwriting the account of a key', () => {
      it('puts the new data into that record and keeps its id, order and moment of adding', () => {
        const first = service.add({ server, ...tokens, identity: 'user-1', email: 'a@b.org' });
        const second = service.add({ server: other, ...tokens, identity: 'user-2' });

        const replaced = service.add({ server, accessToken: 'new', refreshToken: 'new-refresh', identity: 'user-9', email: 'a@b.org' }, first.id);

        expect(replaced).toMatchObject({ id: first.id, identity: 'user-9', accessToken: 'new', refreshToken: 'new-refresh', addedAt: first.addedAt });
        expect(service.accounts().map((account) => account.id)).toEqual([first.id, second.id]);
        expect(service.activeId()).toBe(first.id);
        expect(record(first.id)).toMatchObject({ identity: 'user-9', refreshToken: 'new-refresh' });
      });

      it('moves the record to another server when the sign-in was on another one', () => {
        const account = service.add({ server, ...tokens, identity: 'user-1' });
        const replaced = service.add({ server: other, ...tokens, identity: 'user-1' }, account.id);

        expect(replaced).toMatchObject({ id: account.id, server: other });
        expect(service.accounts()).toHaveLength(1);
      });

      it('keeps the snapshot of the profile for the same user on the same server', () => {
        const account = addAccountWithSnapshot(service, { server, ...tokens, identity: 'user-1' }, { firstName: 'Перший', permissions: ['A'] });
        const replaced = service.add({ server, accessToken: 'new', refreshToken: 'new-refresh', identity: 'user-1' }, account.id);

        expect(replaced).toMatchObject({ firstName: 'Перший', permissions: ['A'] });
      });

      it('drops the snapshot of another user, it was not his', () => {
        const account = addAccountWithSnapshot(service, { server, ...tokens, identity: 'user-1' }, { firstName: 'Перший', ngoName: 'Організація', permissions: ['A'] });
        const replaced = service.add({ server, ...tokens, identity: 'user-2' }, account.id);

        expect(replaced).toMatchObject({ id: account.id, firstName: '', ngoName: null, permissions: [] });
      });

      it('leaves one account for a user on a server, when another record already has this user', () => {
        const target = service.add({ server, ...tokens, identity: 'user-1' });
        const duplicate = service.add({ server, ...tokens, identity: 'user-2' });

        service.add({ server, accessToken: 'new', refreshToken: 'new-refresh', identity: 'user-2' }, target.id);

        expect(service.accounts().map((account) => account.id)).toEqual([target.id]);
        expect(localStorage.getItem(`yavir.account.${duplicate.id}`)).toBeNull();
        expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([target.id]);
        expect(service.activeId()).toBe(target.id);
      });

      it('adds an account as usual for a key that no account has', () => {
        service.add({ server, ...tokens, identity: 'user-1' });
        service.add({ server: other, ...tokens, identity: 'user-2' }, 'unknown-key');

        expect(service.accounts()).toHaveLength(2);
      });
    });

    it('updates the fields of a record, in memory and in storage', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      service.update(account.id, { firstName: 'Перший', ngoName: 'Організація', refreshToken: 'r2' });
      expect(service.active()).toMatchObject({ firstName: 'Перший', ngoName: 'Організація', refreshToken: 'r2' });
      expect(record(account.id)).toMatchObject({ firstName: 'Перший', ngoName: 'Організація', refreshToken: 'r2' });
    });

    it('keeps the NGO logo when the account is added again, and updates it with the record', () => {
      const first = addAccountWithSnapshot(service, { server, ...tokens, identity: 'a' }, { ngoName: 'Організація', ngoLogoUrl: 'https://x/logo.png' });
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

    it('marks an account outdated without deleting it; it stops being active and no other account is picked instead', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });

      service.markOutdated(second.id);
      expect(service.accounts()).toHaveLength(2);
      expect(record(second.id).outdated).toBe(true);
      expect(record(second.id).refreshToken).toBe('refresh');
      expect(record(first.id).outdated).toBe(false);
      expect(service.activeId()).toBeNull();
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe('');
    });

    it('marks a rejected session as outdated, and an account whose server did not answer as unavailable: independent flags', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.add({ server, ...tokens, identity: 'c' });

      service.markOutdated(first.id);
      expect(record(first.id)).toMatchObject({ outdated: true, unavailable: false });

      service.markUnavailable(second.id);
      expect(record(second.id)).toMatchObject({ outdated: false, unavailable: true });

      // Токени відхилили й сервер мовчить: обидва прапорці.
      service.markUnavailable(first.id);
      expect(record(first.id)).toMatchObject({ outdated: true, unavailable: true });
    });

    it('resets unavailable when the server answered but rejected the tokens', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      service.add({ server, ...tokens, identity: 'b' });
      service.markUnavailable(account.id);
      service.markOutdated(account.id);
      expect(record(account.id)).toMatchObject({ outdated: true, unavailable: false });
    });

    it('signing in again to an outdated or unavailable account makes it valid and clears both flags', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.add({ server, ...tokens, identity: 'c' });
      service.markOutdated(first.id);
      service.markUnavailable(first.id);
      service.markUnavailable(second.id);

      service.add({ server, accessToken: 'new', refreshToken: 'r2', identity: 'a' });
      service.add({ server, accessToken: 'new', refreshToken: 'r3', identity: 'b' });
      expect(record(first.id)).toMatchObject({ outdated: false, unavailable: false });
      expect(record(second.id)).toMatchObject({ outdated: false, unavailable: false });
    });

    it('also leaves an unavailable active account, with no other one picked', () => {
      service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.setActive(second.id);

      service.markUnavailable(second.id);
      expect(service.activeId()).toBeNull();
    });

    it('keeps the active account when another one is marked outdated', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.setActive(first.id);
      service.markOutdated(second.id);
      expect(service.activeId()).toBe(first.id);
    });

    it('keeps every account, with no active one, and does not touch the rest of the memory once none is available', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      localStorage.setItem('yavir.language', 'uk');

      service.markOutdated(first.id);
      service.markUnavailable(second.id);

      expect(service.accounts().map((a) => a.id)).toEqual([first.id, second.id]);
      expect(service.activeId()).toBeNull();
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe('');
      expect(localStorage.getItem('yavir.language')).toBe('uk');
      expect(record(first.id).refreshToken).toBe('refresh');
    });

    it('ignores marking an unknown account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      service.markOutdated('ghost');
      service.markUnavailable('ghost');
      expect(service.accounts()[0]).toMatchObject({ outdated: false, unavailable: false });
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

    it('reads records without a patronymic as having an empty one, and keeps the snapshot patronymic on a repeated sign-in', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r' }));
      expect(reopen().accounts()[0].patronymic).toBe('');

      const account = addAccountWithSnapshot(service, { server, ...tokens, identity: 'a' }, { patronymic: 'Третій' });
      service.add({ server, accessToken: 'new', refreshToken: 'r2', identity: 'a' });
      expect(record(account.id).patronymic).toBe('Третій');
    });

    it('reads records without the new fields as valid accounts with no e-mail', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r' }));
      expect(reopen().accounts()[0]).toMatchObject({ outdated: false, email: '' });
    });
  });

  describe('removing and switching', () => {
    it('removes the record and the index entry; the removed active account is replaced by nothing', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });

      service.remove(second.id);
      expect(service.accounts().map((a) => a.id)).toEqual([first.id]);
      expect(localStorage.getItem(`yavir.account.${second.id}`)).toBeNull();
      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([first.id]);
      expect(service.activeId()).toBeNull();
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe('');
    });

    it('keeps the others, with no active account, when the last available one is removed', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      localStorage.setItem('yavir.language', 'uk');
      service.markOutdated(first.id);

      service.remove(second.id);
      expect(service.accounts().map((a) => a.id)).toEqual([first.id]);
      expect(service.activeId()).toBeNull();
      expect(localStorage.getItem('yavir.language')).toBe('uk');
    });

    it('leaves no accounts and no active one when the only account is removed, but does not wipe the rest of the memory', () => {
      const only = service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem('yavir.language', 'uk');
      service.remove(only.id);
      expect(service.accounts()).toHaveLength(0);
      expect(service.activeId()).toBeNull();
      expect(localStorage.getItem('yavir.language')).toBe('uk');
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
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe(first.id);

      service.setActive('unknown');
      expect(service.activeId()).toBe(first.id);

      service.setActive(null);
      expect(service.active()).toBeNull();
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe('');
    });

    it('wipes the whole storage, the shared one and the one of the tab, and forgets every account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem('yavir.language', 'uk');
      service.wipe();
      expect(localStorage.length).toBe(0);
      expect(sessionStorage.length).toBe(0);
      expect(service.accounts()).toHaveLength(0);
      expect(service.activeId()).toBeNull();
    });
  });

  describe('reading', () => {
    it('restores the accounts and the active one when the page is opened again', () => {
      addAccountWithSnapshot(service, { server, ...tokens, identity: 'a' }, { firstName: 'Перший' });
      const second = service.add({ server: other, ...tokens, identity: 'b' });
      service.setActive(second.id);

      const reopened = reopen();
      expect(reopened.accounts()).toHaveLength(2);
      expect(reopened.activeId()).toBe(second.id);
      expect(reopened.accounts()[0].firstName).toBe('Перший');
    });

    it('drops index entries without a record, and removes broken records from the storage too (they may hold tokens)', () => {
      const good = service.add({ server, ...tokens, identity: 'a' });
      const broken = service.add({ server, ...tokens, identity: 'b' });
      localStorage.setItem(`yavir.account.${broken.id}`, '{not json');
      localStorage.setItem('yavir.accounts', JSON.stringify([good.id, broken.id, 'ghost']));

      const reopened = reopen();
      expect(reopened.accounts().map((a) => a.id)).toEqual([good.id]);
      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([good.id]);
      expect(localStorage.getItem(`yavir.account.${broken.id}`)).toBeNull();
    });

    it.each([
      ['has no tokens', { id: 'x', server }],
      ['has the id of another account', { id: 'y', server, accessToken: 'a', refreshToken: 'r' }],
      ['is not an object', 'x'],
    ])('removes a record that %s', (_name, damaged) => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify(damaged));
      expect(reopen().accounts()).toEqual([]);
      expect(localStorage.getItem('yavir.account.x')).toBeNull();
      expect(JSON.parse(localStorage.getItem('yavir.accounts')!)).toEqual([]);
    });

    it('replaces the fields of a wrong type with defaults instead of trusting them', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem(
        'yavir.account.x',
        JSON.stringify({
          id: 'x',
          server,
          accessToken: 'a',
          refreshToken: 'r',
          identity: 1,
          userId: {},
          email: ['a'],
          outdated: 'yes',
          unavailable: 1,
          firstName: 5,
          lastName: null,
          patronymic: false,
          avatarUrl: 7,
          ngoName: {},
          ngoLogoUrl: [],
          tokensIssuedAt: 'now',
          addedAt: null,
          lastUsedAt: {},
          extra: 'ignored',
        }),
      );
      expect(reopen().accounts()[0]).toEqual({
        id: 'x',
        server,
        accessToken: 'a',
        refreshToken: 'r',
        identity: '',
        userId: '',
        email: '',
        outdated: false,
        unavailable: false,
        firstName: '',
        lastName: '',
        patronymic: '',
        avatarUrl: null,
        ngoName: null,
        ngoLogoUrl: null,
        avatarThumb: null,
        ngoLogoThumb: null,
        permissions: [],
        roles: [],
        tokensIssuedAt: 0,
        addedAt: 0,
        lastUsedAt: 0,
      });
    });

    it('forgets an active pointer to a missing account', () => {
      service.add({ server, ...tokens, identity: 'a' });
      sessionStorage.setItem('yavir.activeAccount', 'ghost');
      expect(reopen().activeId()).toBeNull();
    });

    it('fills fields missing in an older or partial record with defaults', () => {
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r' }));
      sessionStorage.setItem('yavir.activeAccount', 'x');
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
      sessionStorage.setItem('yavir.activeAccount', 'x');
      expect(reopen().active()?.permissions).toEqual(['ViewUser']);

      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r', permissions: 'ViewUser' }));
      expect(reopen().active()?.permissions).toEqual([]);
    });

    it('stores the roles in the record, keeps them when the account is added again and starts with none', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      expect(first.roles).toEqual([]);

      service.update(first.id, { roles: [{ id: 'r1', name: 'Перша' }] });
      expect(record(first.id).roles).toEqual([{ id: 'r1', name: 'Перша' }]);

      service.add({ server, accessToken: 'new', refreshToken: 'r2', identity: 'a' });
      expect(reopen().active()?.roles).toEqual([{ id: 'r1', name: 'Перша' }]);
    });

    it('ignores a damaged list of roles in a record', () => {
      const damaged = [{ id: 'r1', name: 'Перша' }, { id: 'r2' }, { name: 'Друга' }, { id: 3, name: 'Третя' }, null, 'r4'];
      localStorage.setItem('yavir.accounts', JSON.stringify(['x']));
      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r', roles: damaged }));
      sessionStorage.setItem('yavir.activeAccount', 'x');
      expect(reopen().active()?.roles).toEqual([{ id: 'r1', name: 'Перша' }]);

      localStorage.setItem('yavir.account.x', JSON.stringify({ id: 'x', server, accessToken: 'a', refreshToken: 'r', roles: 'r1' }));
      expect(reopen().active()?.roles).toEqual([]);
    });

    it('copes with a broken index', () => {
      localStorage.setItem('yavir.accounts', 'garbage');
      expect(reopen().accounts()).toEqual([]);
    });

    describe('when the browser gives no storage (the data of sites is blocked)', () => {
      /** Сервіс, створений уже без сховища: читання й запис кидають помилку. */
      function withoutStorage(): AccountsService {
        vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
          throw new Error('denied');
        });
        vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
          throw new Error('denied');
        });
        return reopen();
      }

      it('keeps the accounts in memory: adds, updates and switches them', () => {
        const blocked = withoutStorage();
        const first = blocked.add({ server, ...tokens, identity: 'a' });
        const second = blocked.add({ server, ...tokens, identity: 'b' });
        expect(blocked.activeId()).toBe(second.id);

        blocked.update(first.id, { ngoName: 'Організація' });
        blocked.setActive(first.id);
        expect(blocked.active()).toMatchObject({ id: first.id, accessToken: 'access', ngoName: 'Організація' });
      });

      it('does not forget them when the storage is read again (the guards do it on every page change, another tab sends an event)', () => {
        const blocked = withoutStorage();
        const account = blocked.add({ server, ...tokens, identity: 'a' });

        blocked.reload();
        window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.accounts' }));

        expect(blocked.accounts().map((a) => a.id)).toEqual([account.id]);
        expect(blocked.activeId()).toBe(account.id);
      });

      it('does not warn that the storage is full: there is no storage at all', () => {
        const blocked = withoutStorage();
        blocked.add({ server, ...tokens, identity: 'a' });
        expect(blocked.storageFull()).toBe(false);
      });
    });

    describe('when the storage is full (it is read, but the browser does not let write)', () => {
      const fillStorage = () =>
        vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
          throw new DOMException('full', 'QuotaExceededError');
        });

      it('is not full while the writes go through', () => {
        service.add({ server, ...tokens, identity: 'a' });
        expect(service.storageFull()).toBe(false);
      });

      it('says that it is full once a write fails during the work (new tokens of an account)', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        fillStorage();
        service.update(account.id, { accessToken: 'new', refreshToken: 'new-r' });
        expect(service.storageFull()).toBe(true);
        // У памʼяті зміна є, у сховищі — ні.
        expect(service.active()?.accessToken).toBe('new');
        expect(record(account.id).accessToken).toBe('access');
      });

      it('stops saying so when the warning is closed, and says it again on the next failed write', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        fillStorage();
        service.update(account.id, { ngoName: 'Організація' });
        service.dismissStorageFull();
        expect(service.storageFull()).toBe(false);
        service.update(account.id, { ngoName: 'Друга' });
        expect(service.storageFull()).toBe(true);
      });
    });
  });

  describe('the active account of a tab (every tab has its own)', () => {
    const hint = () => localStorage.getItem('yavir.lastActiveAccount');
    const visibility = (state: 'visible' | 'hidden') => vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);

    /** Зовсім нова вкладка: свого вибору в неї ще нема, а спільне сховище те саме. */
    function newTab(): AccountsService {
      sessionStorage.clear();
      return reopen();
    }

    it('starts a new tab with the account of the tab the user saw last, and keeps it as its own', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      service.add({ server, ...tokens, identity: 'b' });
      service.setActive(first.id);

      const tab = newTab();
      expect(tab.activeId()).toBe(first.id);
      expect(sessionStorage.getItem('yavir.activeAccount')).toBe(first.id);
    });

    it('keeps its own account after the page is reloaded, whatever the other tabs have chosen since', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.setActive(first.id);
      // Інша вкладка перемкнулась на другий акаунт.
      localStorage.setItem('yavir.lastActiveAccount', second.id);
      window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.lastActiveAccount' }));

      expect(service.activeId()).toBe(first.id);
      expect(reopen().activeId()).toBe(first.id);
    });

    it('stays without an active account when the tab has none on purpose, instead of taking the one of another tab', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      service.setActive(null);
      localStorage.setItem('yavir.lastActiveAccount', first.id);

      service.reload();
      expect(service.activeId()).toBeNull();
      expect(reopen().activeId()).toBeNull();
    });

    it('starts a new tab with no active account when the remembered one is gone or outdated', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      service.markOutdated(first.id);
      localStorage.setItem('yavir.lastActiveAccount', first.id);
      expect(newTab().activeId()).toBeNull();

      localStorage.setItem('yavir.lastActiveAccount', 'ghost');
      expect(newTab().activeId()).toBeNull();
    });

    it('remembers the account for new tabs only while the tab is seen, and again when the user comes back to it', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      expect(hint()).toBe(second.id);

      const state = visibility('hidden');
      service.setActive(first.id);
      expect(hint()).toBe(second.id);

      state.mockReturnValue('visible');
      document.dispatchEvent(new Event('visibilitychange'));
      expect(hint()).toBe(first.id);
    });

    it('does not remember the accounts that are active only while one is being searched for, but the one that is left after it', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      const second = service.add({ server, ...tokens, identity: 'b' });
      service.setActive(first.id);

      service.setSwitching(true);
      service.setActive(second.id);
      expect(hint()).toBe(first.id);

      service.setSwitching(false);
      expect(hint()).toBe(second.id);
    });

    it('keeps the remembered account when the tab is left without an active one', () => {
      const first = service.add({ server, ...tokens, identity: 'a' });
      service.setActive(null);
      expect(hint()).toBe(first.id);
    });

    describe('when another tab changes the account that is active here', () => {
      /** Те, що робить інша вкладка: пише запис акаунта, і сюди приходить подія `storage`. */
      function otherTabWrites(id: string, patch: Record<string, unknown>): void {
        localStorage.setItem(`yavir.account.${id}`, JSON.stringify({ ...record(id), ...patch }));
        window.dispatchEvent(new StorageEvent('storage', { key: `yavir.account.${id}` }));
      }

      it('leaves this tab without an active account once the other tab has marked it outdated', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        otherTabWrites(account.id, { outdated: true });
        expect(service.activeId()).toBeNull();
        expect(service.accounts()[0].outdated).toBe(true);
        expect(sessionStorage.getItem('yavir.activeAccount')).toBe('');
      });

      it('keeps it active when the other tab has only found its server silent: every tab finds that out itself', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        otherTabWrites(account.id, { unavailable: true });
        expect(service.activeId()).toBe(account.id);
      });

      it('keeps an outdated account active while it is being checked here (a switch to it)', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        service.setSwitching(true);
        otherTabWrites(account.id, { outdated: true });
        expect(service.activeId()).toBe(account.id);
      });

      it('does not take the account back when the other tab signed out of it and then signed in again', () => {
        const account = service.add({ server, ...tokens, identity: 'a' });
        const saved = record(account.id);
        // Вихід в іншій вкладці.
        localStorage.removeItem(`yavir.account.${account.id}`);
        localStorage.setItem('yavir.accounts', '[]');
        window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.accounts' }));
        expect(service.activeId()).toBeNull();
        // Вхід у той самий акаунт там само: запис із тим самим `id` зʼявився знову.
        localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify(saved));
        localStorage.setItem('yavir.accounts', JSON.stringify([account.id]));
        window.dispatchEvent(new StorageEvent('storage', { key: 'yavir.accounts' }));

        expect(service.accounts()).toHaveLength(1);
        expect(service.activeId()).toBeNull();
      });
    });
  });

  describe('other tabs', () => {
    it('follows the storage events of the records and the index', () => {
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

    it('does not write the old tokens back over the ones another tab has just refreshed, when it updates the record', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      // Інша вкладка оновила токени, а подія `storage` до цієї ще не дійшла.
      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...record(account.id), refreshToken: 'newer', accessToken: 'newer-a' }));

      service.update(account.id, { ngoName: 'Організація' });

      expect(record(account.id)).toMatchObject({ refreshToken: 'newer', accessToken: 'newer-a', ngoName: 'Організація' });
      expect(service.active()).toMatchObject({ refreshToken: 'newer', accessToken: 'newer-a', ngoName: 'Організація' });
    });

    it('still writes the tokens that the update itself brings', () => {
      const account = service.add({ server, ...tokens, identity: 'a' });
      localStorage.setItem(`yavir.account.${account.id}`, JSON.stringify({ ...record(account.id), refreshToken: 'newer' }));

      service.update(account.id, { refreshToken: 'newest', accessToken: 'newest-a' });

      expect(record(account.id)).toMatchObject({ refreshToken: 'newest', accessToken: 'newest-a' });
    });
  });
});
