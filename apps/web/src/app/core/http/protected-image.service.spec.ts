import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

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

  it('returns a link that needs no authorization as it is, without any request', async () => {
    expect(await service.resolve('blob:http://localhost/9')).toBe('blob:http://localhost/9');
    expect(await service.resolve('https://cdn.example.org/logo.png')).toBe('https://cdn.example.org/logo.png');
    http.expectNone(() => true);
  });

  it('loads a protected file through HttpClient as a blob and returns a blob link for it', async () => {
    const result = service.resolve(logo());
    const request = http.expectOne(logo());
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['x'], { type: 'image/png' }));
    expect(await result).toBe('blob:http://localhost/1');
  });

  it('asks once per address: the same one is taken from the cache, a new version is a new request', async () => {
    const first = service.resolve(logo('v1'));
    http.expectOne(logo('v1')).flush(new Blob(['x']));
    await first;

    expect(await service.resolve(logo('v1'))).toBe('blob:http://localhost/1');
    http.expectNone(logo('v1'));

    const next = service.resolve(logo('v2'));
    http.expectOne(logo('v2')).flush(new Blob(['y']));
    expect(await next).toBe('blob:http://localhost/2');
  });

  it('shares one request between calls that overlap', async () => {
    const first = service.resolve(logo());
    const second = service.resolve(logo());
    http.expectOne(logo()).flush(new Blob(['x']));
    expect(await first).toBe(await second);
  });

  it.each([401, 403, 404, 500])('returns null for HTTP %i, and tries again next time', async (status) => {
    const failed = service.resolve(logo());
    http.expectOne(logo()).flush(new Blob([]), { status, statusText: 'x' });
    expect(await failed).toBeNull();

    const again = service.resolve(logo());
    http.expectOne(logo()).flush(new Blob(['x']));
    expect(await again).toBe('blob:http://localhost/1');
  });

  it('returns null when the server does not answer', async () => {
    const result = service.resolve(logo());
    http.expectOne(logo()).error(new ProgressEvent('error'));
    expect(await result).toBeNull();
  });

  it('frees the oldest images when there are too many', async () => {
    for (let i = 0; i < 31; i++) {
      const pending = service.resolve(logo(`v${i}`));
      http.expectOne(logo(`v${i}`)).flush(new Blob(['x']));
      await pending;
    }
    await vi.waitFor(() => expect(revoke).toHaveBeenCalledWith('blob:http://localhost/1'));
    expect(revoke).toHaveBeenCalledTimes(1);
  });
});
