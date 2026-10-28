import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { ThumbnailService } from '../images/thumbnail.service';
import { CurrentUserService } from './current-user.service';

describe('CurrentUserService', () => {
  let http: HttpTestingController;
  let service: CurrentUserService;
  let auth: AuthService;
  let url: string;
  // Імена в тестах — пробіли: «старе» значення один пробіл, «нове» (після
  // оновлення чи збереження) — два, щоб їх можна було відрізнити.
  const profile = { id: 'u1', email: 'a@b.org', firstName: ' ', lastName: ' ', roles: [], customFields: [] };

  function signIn(): void {
    const server = TestBed.inject(ConnectionService).serverUrl();
    TestBed.inject(AccountsService).add({ server, accessToken: 'access', refreshToken: 'refresh', identity: 'u1' });
  }

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(CurrentUserService);
    url = `${connection.serverUrl()}/api/v1/users/me`;
    signIn();
    TestBed.tick();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('requests the profile once, keeps it and does not ask again', async () => {
    const first = service.ensure();
    http.expectOne(url).flush(profile);
    expect(await first).toBe('ok');
    expect(service.user()).toEqual(profile);

    expect(await service.ensure()).toBe('ok');
    http.expectNone(url);
  });

  it('shares one request between simultaneous checks', async () => {
    const first = service.ensure();
    const second = service.ensure();
    http.expectOne(url).flush(profile);
    expect(await first).toBe('ok');
    expect(await second).toBe('ok');
  });

  it('reports unauthorized when the server rejects the token', async () => {
    const result = service.ensure();
    http.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await result).toBe('unauthorized');
    expect(service.user()).toBeNull();
  });

  it.each([
    ['a server error', 500],
    ['a missing profile', 404],
  ])('reports unavailable (not unauthorized) for %s, and asks again next time', async (_name, status) => {
    const result = service.ensure();
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe('unavailable');

    const again = service.ensure();
    http.expectOne(url).flush(profile);
    expect(await again).toBe('ok');
  });

  it('reports unavailable when the server does not answer', async () => {
    const result = service.ensure();
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unavailable');
  });

  it('forgets the profile on sign-out and on a new sign-in', async () => {
    const first = service.ensure();
    http.expectOne(url).flush(profile);
    await first;
    expect(service.user()).not.toBeNull();

    auth.clearTokens();
    TestBed.tick();
    expect(service.user()).toBeNull();

    signIn();
    const login = auth.login('a@b.org', 'x');
    http.expectOne(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/auth/login`).flush({ accessToken: 'a2', refreshToken: 'r2' });
    await login;
    const second = service.ensure();
    http.expectOne(url).flush({ ...profile, id: 'u2' });
    await second;
    expect(service.user()?.id).toBe('u2');
  });

  it('does not take a profile that arrived after the user signed out', async () => {
    const result = service.ensure();
    const request = http.expectOne(url);
    auth.clearTokens();
    request.flush(profile);
    expect(await result).toBe('unauthorized');
    expect(service.user()).toBeNull();
  });

  it('keeps a profile that arrived before the first reset check ran', async () => {
    // Сервіс уже створено в beforeEach, але ефекти ще не прокручувались.
    const fresh = TestBed.inject(CurrentUserService);
    const result = fresh.ensure();
    http.expectOne(url).flush(profile);
    await result;
    TestBed.tick();
    expect(fresh.user()).toEqual(profile);
  });

  describe('permissions (GET /users/me/permissions together with /users/me)', () => {
    const permissionsUrl = () => `${TestBed.inject(ConnectionService).serverUrl()}/api/v1/users/me/permissions`;
    const stored = () => JSON.parse(localStorage.getItem(`yavir.account.${TestBed.inject(AccountsService).activeId()}`)!);

    async function loaded(permissions: string[] = ['ViewUser']): Promise<void> {
      const first = service.ensure();
      http.expectOne(url).flush(profile);
      await first;
      http.match(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/ngo`);
      http.expectOne(permissionsUrl()).flush(permissions);
      await vi.waitFor(() => expect(service.permissions()).toEqual(permissions));
    }

    it('asks for the permissions after the profile and writes them to the account record in localStorage', async () => {
      await loaded(['ViewUser', 'UpdateUser']);
      expect(stored().permissions).toEqual(['ViewUser', 'UpdateUser']);
    });

    it('asks again on every request of the profile and replaces the list', async () => {
      await loaded(['ViewUser', 'UpdateUser']);

      const result = service.reload();
      http.expectOne(url).flush(profile);
      await result;
      http.match(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/ngo`);
      http.expectOne(permissionsUrl()).flush(['ViewUser']);

      await vi.waitFor(() => expect(service.permissions()).toEqual(['ViewUser']));
      expect(stored().permissions).toEqual(['ViewUser']);
    });

    it('keeps the saved permissions when they could not be received', async () => {
      await loaded(['ViewUser']);

      const result = service.reload();
      http.expectOne(url).flush(profile);
      await result;
      http.match(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/ngo`);
      http.expectOne(permissionsUrl()).flush({}, { status: 500, statusText: 'x' });

      await vi.waitFor(() => expect(console.warn).toHaveBeenCalled());
      expect(service.permissions()).toEqual(['ViewUser']);
      expect(stored().permissions).toEqual(['ViewUser']);
    });

    it('does not write the permissions to an account that is not the current one any more', async () => {
      const first = service.ensure();
      http.expectOne(url).flush(profile);
      await first;
      http.match(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/ngo`);
      const request = http.expectOne(permissionsUrl());
      const id = TestBed.inject(AccountsService).activeId()!;

      auth.clearTokens();
      request.flush(['ViewUser']);
      await Promise.resolve();
      expect(JSON.parse(localStorage.getItem(`yavir.account.${id}`) ?? 'null')?.permissions ?? []).toEqual([]);
    });
  });

  describe('reload (GET /users/me on every page change)', () => {
    async function loaded(): Promise<void> {
      const first = service.ensure();
      http.expectOne(url).flush(profile);
      await first;
      http.match(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/ngo`);
    }

    it('asks again although the profile is already known, and replaces it with the answer', async () => {
      await loaded();
      const result = service.reload();
      const request = http.expectOne(url);
      expect(request.request.method).toBe('GET');
      request.flush({ ...profile, firstName: '  ', lastName: '  ' });

      expect(await result).toBe('ok');
      expect(service.user()).toMatchObject({ id: 'u1', firstName: '  ', lastName: '  ' });
      const record = JSON.parse(localStorage.getItem(`yavir.account.${TestBed.inject(AccountsService).activeId()}`)!);
      expect(record).toMatchObject({ firstName: '  ', lastName: '  ' });
    });

    it('shares one request between simultaneous calls', async () => {
      await loaded();
      const first = service.reload();
      const second = service.reload();
      http.expectOne(url).flush(profile);
      expect(await first).toBe('ok');
      expect(await second).toBe('ok');
    });

    it.each([500, 404])('keeps the profile when the server answers %i', async (status) => {
      await loaded();
      const result = service.reload();
      http.expectOne(url).flush({}, { status, statusText: 'x' });
      expect(await result).toBe('unavailable');
      expect(service.user()).toEqual(profile);
    });

    it('sends nothing without tokens', async () => {
      auth.clearTokens();
      expect(await service.reload()).toBe('unauthorized');
      http.expectNone(url);
    });
  });

  describe('the thumbnails of the avatar and of the NGO logo in the snapshot of the account', () => {
    const THUMB = 'data:image/webp;base64,THUMB';
    let server: string;
    let create: ReturnType<typeof vi.spyOn>;
    let counter: number;
    const originalCreate = (URL as unknown as Record<string, unknown>)['createObjectURL'];
    const originalRevoke = (URL as unknown as Record<string, unknown>)['revokeObjectURL'];

    beforeEach(() => {
      counter = 0;
      (URL as unknown as Record<string, unknown>)['createObjectURL'] = vi.fn(() => `blob:http://localhost/${++counter}`);
      (URL as unknown as Record<string, unknown>)['revokeObjectURL'] = vi.fn();
      server = TestBed.inject(ConnectionService).serverUrl();
      create = vi.spyOn(TestBed.inject(ThumbnailService), 'create').mockResolvedValue(THUMB);
    });

    afterEach(() => {
      for (const [name, original] of [
        ['createObjectURL', originalCreate],
        ['revokeObjectURL', originalRevoke],
      ] as const) {
        if (original === undefined) {
          delete (URL as unknown as Record<string, unknown>)[name];
        } else {
          (URL as unknown as Record<string, unknown>)[name] = original;
        }
      }
    });

    const avatar = (version = 'v1') => `${server}/api/v1/users/me/avatar?v=${version}`;
    const logo = (version = 'v1') => `${server}/api/v1/ngo/logo?v=${version}`;
    const ngoUrl = () => `${server}/api/v1/ngo`;
    const active = () => TestBed.inject(AccountsService).active()!;

    /** Профіль завантажено; повертає, коли запити назад отримано (організацію віддаємо як є). */
    async function loadProfile(extra: Record<string, unknown>, ngo: Record<string, unknown> = { name: 'Org', logoUrl: null }): Promise<void> {
      const first = service.ensure();
      http.expectOne(url).flush({ ...profile, ...extra });
      await first;
      http.match(ngoUrl()).forEach((request) => request.flush(ngo));
    }

    it('makes the thumbnail of a new avatar from the file that it loads with the token, and stores it with the address it was made from', async () => {
      await loadProfile({ avatarUrl: avatar() });

      http.expectOne(avatar()).flush(new Blob(['x']));
      await vi.waitFor(() => expect(active().avatarThumb).toEqual({ url: avatar(), data: THUMB }));
      expect(create).toHaveBeenCalledWith('blob:http://localhost/1', 96, 'cover');
    });

    it('resolves a relative link against the server of the account', async () => {
      await loadProfile({ avatarUrl: '/api/v1/users/me/avatar?v=v9' });
      http.expectOne(avatar('v9')).flush(new Blob(['x']));
      await vi.waitFor(() => expect(active().avatarThumb?.url).toBe('/api/v1/users/me/avatar?v=v9'));
    });

    it('stores the thumbnail in the record in localStorage', async () => {
      await loadProfile({ avatarUrl: avatar() });
      http.expectOne(avatar()).flush(new Blob(['x']));
      await vi.waitFor(() => expect(active().avatarThumb).not.toBeNull());
      const stored = JSON.parse(localStorage.getItem(`yavir.account.${active().id}`)!);
      expect(stored.avatarThumb).toEqual({ url: avatar(), data: THUMB });
    });

    it('does not make it again, and does not ask for the file, while the address is the same', async () => {
      TestBed.inject(AccountsService).update(active().id, { avatarThumb: { url: avatar(), data: THUMB } });
      await loadProfile({ avatarUrl: avatar() });

      http.expectNone(avatar());
      expect(create).not.toHaveBeenCalled();
    });

    it('makes a new one when the avatar changes (a new version in the address)', async () => {
      TestBed.inject(AccountsService).update(active().id, { avatarThumb: { url: avatar('v1'), data: 'data:image/webp;base64,OLD' } });
      await loadProfile({ avatarUrl: avatar('v2') });

      http.expectOne(avatar('v2')).flush(new Blob(['x']));
      await vi.waitFor(() => expect(active().avatarThumb).toEqual({ url: avatar('v2'), data: THUMB }));
    });

    it('takes the thumbnail away when the user has no avatar any more', async () => {
      TestBed.inject(AccountsService).update(active().id, { avatarThumb: { url: avatar(), data: THUMB } });
      await loadProfile({ avatarUrl: null });
      await vi.waitFor(() => expect(active().avatarThumb).toBeNull());
      http.expectNone(() => false);
    });

    it('does nothing when the user has no avatar and no thumbnail', async () => {
      await loadProfile({ avatarUrl: null });
      expect(active().avatarThumb).toBeNull();
      expect(create).not.toHaveBeenCalled();
    });

    it('keeps the old thumbnail when the new file cannot be loaded', async () => {
      const old = { url: avatar('v1'), data: 'data:image/webp;base64,OLD' };
      TestBed.inject(AccountsService).update(active().id, { avatarThumb: old });
      await loadProfile({ avatarUrl: avatar('v2') });

      http.expectOne(avatar('v2')).flush(new Blob([]), { status: 403, statusText: 'Forbidden' });
      await new Promise((resolve) => setTimeout(resolve));
      expect(active().avatarThumb).toEqual(old);
    });

    it('keeps the old thumbnail when the new file cannot be drawn', async () => {
      const old = { url: avatar('v1'), data: 'data:image/webp;base64,OLD' };
      TestBed.inject(AccountsService).update(active().id, { avatarThumb: old });
      create.mockResolvedValue(null);
      await loadProfile({ avatarUrl: avatar('v2') });

      http.expectOne(avatar('v2')).flush(new Blob(['x']));
      await vi.waitFor(() => expect(create).toHaveBeenCalled());
      await new Promise((resolve) => setTimeout(resolve));
      expect(active().avatarThumb).toEqual(old);
    });

    it('does not write the thumbnail into another account when the active one changed while the file was loading', async () => {
      await loadProfile({ avatarUrl: avatar() });
      const request = http.expectOne(avatar());

      const other = TestBed.inject(AccountsService).add({ server, accessToken: 'a2', refreshToken: 'r2', identity: 'u2' });
      TestBed.tick();
      request.flush(new Blob(['x']));
      await new Promise((resolve) => setTimeout(resolve));

      expect(TestBed.inject(AccountsService).accounts().every((account) => account.avatarThumb === null)).toBe(true);
      expect(other.avatarThumb).toBeNull();
    });

    it('makes the thumbnail of the NGO logo from the logo that the server gave, fitting it whole (contain)', async () => {
      await loadProfile({}, { name: 'Org', logoUrl: logo() });

      (await vi.waitFor(() => http.expectOne(logo()))).flush(new Blob(['x']));
      await vi.waitFor(() => expect(active().ngoLogoThumb).toEqual({ url: logo(), data: THUMB }));
      expect(create).toHaveBeenCalledWith(expect.stringMatching(/^blob:/), 64, 'contain');
    });

    it('does not make the logo thumbnail again for the same address, and takes it away when there is no logo', async () => {
      TestBed.inject(AccountsService).update(active().id, { ngoLogoThumb: { url: logo(), data: THUMB } });
      await loadProfile({}, { name: 'Org', logoUrl: logo() });
      await new Promise((resolve) => setTimeout(resolve));
      http.expectNone(logo());

      const reload = service.reload();
      http.expectOne(url).flush(profile);
      await reload;
      http.match(ngoUrl()).forEach((request) => request.flush({ name: 'Org', logoUrl: null }));
      await vi.waitFor(() => expect(active().ngoLogoThumb).toBeNull());
    });
  });

  describe('applyLocal', () => {
    it('merges saved changes into the profile in memory and into the account record', async () => {
      const first = service.ensure();
      http.expectOne(url).flush(profile);
      await first;
      http.match(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/ngo`);

      const changes = { firstName: '  ', lastName: '  ', patronymic: ' ' };
      service.applyLocal(changes);

      expect(service.user()).toMatchObject({ id: 'u1', email: 'a@b.org', ...changes });
      const record = JSON.parse(localStorage.getItem(`yavir.account.${TestBed.inject(AccountsService).activeId()}`)!);
      expect(record).toMatchObject({ firstName: changes.firstName, lastName: changes.lastName, email: 'a@b.org' });
    });

    it('does nothing while the profile is not loaded', () => {
      service.applyLocal({ firstName: '  ' });
      expect(service.user()).toBeNull();
    });
  });
});
