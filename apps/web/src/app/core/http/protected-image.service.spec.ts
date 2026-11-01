import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { ProtectedImageService } from './protected-image.service';

describe('ProtectedImageService', () => {
  let http: HttpTestingController;
  let service: ProtectedImageService;
  let server: string;
  let counter: number;
  const originalCreate = (URL as unknown as Record<string, unknown>)['createObjectURL'];
  const originalRevoke = (URL as unknown as Record<string, unknown>)['revokeObjectURL'];
  let revoke: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    counter = 0;
    revoke = vi.fn();
    (URL as unknown as Record<string, unknown>)['createObjectURL'] = vi.fn(() => `blob:http://localhost/${++counter}`);
    (URL as unknown as Record<string, unknown>)['revokeObjectURL'] = revoke;
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    service = TestBed.inject(ProtectedImageService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
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

  const logo = (version = 'v1') => `${server}/api/v1/ngo/logo?v=${version}`;

  describe('which links need the authorization', () => {
    it('needs it for the protected files of the API of the current server', () => {
      expect(service.needsAuth(logo())).toBe(true);
      expect(service.needsAuth(`${server}/api/v1/users/me/avatar`)).toBe(true);
    });

    it.each([
      ['a blob link', 'blob:http://localhost/1'],
      ['a data link', 'data:image/png;base64,AAAA'],
      ['another site', 'https://cdn.example.org/logo.png'],
      ['a file of the app', 'assets/logo.svg'],
    ])('does not need it for %s', (_name, url) => {
      expect(service.needsAuth(url)).toBe(false);
    });

    it('does not need it for the public images of the login page', () => {
      expect(service.needsAuth(`${server}/api/v1/loginpageimages/abc/image?v=1`)).toBe(false);
    });
  });

  /** Завантажує зображення до кінця (відповідь сервера) і віддає його користування. */
  async function load(version: string, content = 'x') {
    const lease = service.acquire(logo(version));
    http.expectOne(logo(version)).flush(new Blob([content]));
    const src = await lease.src;
    return { lease, src };
  }

  /** Входить в акаунт (він стає активним) — «сесія» змінюється. */
  function signIn(identity: string): void {
    TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity });
    TestBed.tick();
  }

  it('returns a link that needs no authorization as it is, without any request', async () => {
    expect(await service.acquire('blob:http://localhost/9').src).toBe('blob:http://localhost/9');
    expect(await service.acquire('https://cdn.example.org/logo.png').src).toBe('https://cdn.example.org/logo.png');
    http.expectNone(() => true);
  });

  it('loads a protected file through HttpClient as a blob and returns a blob link for it', async () => {
    const lease = service.acquire(logo());
    const request = http.expectOne(logo());
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['x'], { type: 'image/png' }));
    expect(await lease.src).toBe('blob:http://localhost/1');
  });

  it('asks once per address: the same one is taken from the cache, a new version is a new request', async () => {
    await load('v1');

    expect(await service.acquire(logo('v1')).src).toBe('blob:http://localhost/1');
    http.expectNone(logo('v1'));

    const next = await load('v2', 'y');
    expect(next.src).toBe('blob:http://localhost/2');
  });

  it('shares one request between calls that overlap', async () => {
    const first = service.acquire(logo());
    const second = service.acquire(logo());
    http.expectOne(logo()).flush(new Blob(['x']));
    expect(await first.src).toBe(await second.src);
  });

  it.each([401, 403, 404, 500])('returns null for HTTP %i, and tries again next time', async (status) => {
    const failed = service.acquire(logo());
    http.expectOne(logo()).flush(new Blob([]), { status, statusText: 'x' });
    expect(await failed.src).toBeNull();
    failed.release();

    const again = service.acquire(logo());
    http.expectOne(logo()).flush(new Blob(['x']));
    expect(await again.src).toBe('blob:http://localhost/1');
  });

  it('returns null when the server does not answer', async () => {
    const lease = service.acquire(logo());
    http.expectOne(logo()).error(new ProgressEvent('error'));
    expect(await lease.src).toBeNull();
  });

  describe('when there are too many images', () => {
    it('frees the one that is not used and was not needed for the longest time', async () => {
      for (let i = 0; i < 31; i++) {
        const { lease } = await load(`v${i}`);
        lease.release();
      }
      await vi.waitFor(() => expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1'));
      expect(revoke).toHaveBeenCalledTimes(1);
    });

    it('does not free the image that is still shown: it frees the next one that is not used instead', async () => {
      const shown = await load('v0');
      for (let i = 1; i < 31; i++) {
        const { lease } = await load(`v${i}`);
        lease.release();
      }
      expect(revoke).not.toHaveBeenCalledWith(shown.src);
      expect(revoke).toHaveBeenCalledTimes(1);
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/2');
    });

    it('treats the one that was asked for again as the freshest (the order of the last use, not of the first load)', async () => {
      for (let i = 0; i < 30; i++) {
        const { lease } = await load(`v${i}`);
        lease.release();
      }
      // v0 знову потрібен: найдавнішим стає v1.
      service.acquire(logo('v0')).release();
      const { lease } = await load('v30');
      lease.release();

      expect(revoke).toHaveBeenCalledTimes(1);
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/2');
    });

    it('keeps more than the limit while all of them are shown, and frees the extra ones after they are given back', async () => {
      const leases = [];
      for (let i = 0; i < 32; i++) {
        leases.push((await load(`v${i}`)).lease);
      }
      expect(revoke).not.toHaveBeenCalled();

      leases.forEach((lease) => lease.release());
      const { lease } = await load('v32');
      lease.release();
      // Над лімітом було 3 (33 − 30): усі три звільнено, найдавніші.
      expect(revoke).toHaveBeenCalledTimes(3);
      expect(revoke).toHaveBeenNthCalledWith(1, 'blob:http://localhost/1');
      expect(revoke).toHaveBeenNthCalledWith(2, 'blob:http://localhost/2');
      expect(revoke).toHaveBeenNthCalledWith(3, 'blob:http://localhost/3');
    });
  });

  describe('giving the image back', () => {
    it('counts only the first call of release of the same lease', async () => {
      const first = await load('v1');
      const second = service.acquire(logo('v1'));
      await second.src;

      first.lease.release();
      first.lease.release();
      service.clear();
      // Друге користування ще тримає зображення: посилання не звільнено.
      expect(revoke).not.toHaveBeenCalled();

      second.release();
      expect(revoke).toHaveBeenCalledTimes(1);
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1');
    });
  });

  describe('the cache belongs to the session', () => {
    it('is cleared when the active account changes: what is not shown is freed at once, nothing is taken from the old cache', async () => {
      signIn('u1');
      const { lease } = await load('v1');
      lease.release();

      signIn('u2');
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1');

      // Те саме зображення тепер завантажується знову (токеном нового акаунта).
      const again = service.acquire(logo('v1'));
      http.expectOne(logo('v1')).flush(new Blob(['x']));
      expect(await again.src).toBe('blob:http://localhost/2');
    });

    it('is cleared when the user signs out (the accounts are wiped)', async () => {
      signIn('u1');
      const { lease } = await load('v1');
      lease.release();

      TestBed.inject(AccountsService).wipe();
      TestBed.tick();
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1');
    });

    it('does not take an image from the cache even before the clearing has run: the check is made at once', async () => {
      signIn('u1');
      const { lease } = await load('v1');
      lease.release();

      TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u2' });
      // Без TestBed.tick(): ефект ще не спрацював.
      const again = service.acquire(logo('v1'));
      http.expectOne(logo('v1')).flush(new Blob(['x']));
      expect(await again.src).not.toBe('blob:http://localhost/1');
    });

    it('does not free what is still shown until it is given back', async () => {
      signIn('u1');
      const { lease } = await load('v1');

      signIn('u2');
      expect(revoke).not.toHaveBeenCalled();

      lease.release();
      expect(revoke).toHaveBeenCalledTimes(1);
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1');
    });

    it('gives null for a file that was still loading when the session changed, and frees it', async () => {
      signIn('u1');
      const lease = service.acquire(logo('v1'));
      const request = http.expectOne(logo('v1'));

      signIn('u2');
      request.flush(new Blob(['x']));
      expect(await lease.src).toBeNull();
      expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1');
    });

    it('keeps the cache while the same account stays active (refresh of the tokens and so on)', async () => {
      signIn('u1');
      const { lease } = await load('v1');
      lease.release();

      TestBed.inject(AccountsService).update(TestBed.inject(AccountsService).activeId()!, { accessToken: 'new' });
      TestBed.tick();
      expect(await service.acquire(logo('v1')).src).toBe('blob:http://localhost/1');
      expect(revoke).not.toHaveBeenCalled();
    });
  });
});
