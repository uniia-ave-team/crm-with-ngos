import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { NgoService } from './ngo.service';

describe('NgoService', () => {
  let http: HttpTestingController;
  let service: NgoService;
  let url: string;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    service = TestBed.inject(NgoService);
    url = `${connection.serverUrl()}/api/v1/ngo`;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts the name with a null logoUrl', async () => {
    const result = service.create('Моя ГО');
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Моя ГО', logoUrl: null });
    request.flush(null, { status: 201, statusText: 'Created' });
    expect(await result).toBe('created');
  });

  it('stores the name of the created NGO in the active account and clears its logo', async () => {
    const accounts = TestBed.inject(AccountsService);
    const account = accounts.add({
      server: TestBed.inject(ConnectionService).serverUrl(),
      accessToken: 'a',
      refreshToken: 'r',
      identity: 'u',
      ngoLogoUrl: '/old.png',
    });
    const result = service.create('Моя ГО');
    http.expectOne(url).flush(null, { status: 201, statusText: 'Created' });
    expect(await result).toBe('created');
    expect(accounts.accounts().find((a) => a.id === account.id)).toMatchObject({ ngoName: 'Моя ГО', ngoLogoUrl: null });
  });

  describe('fetch', () => {
    it('returns the name and the logo link', async () => {
      const result = service.fetch();
      const request = http.expectOne(url);
      expect(request.request.method).toBe('GET');
      request.flush({ id: 'n', name: ' Моя ГО ', logoUrl: ' /files/logo.png ', createdAt: '2026-01-01T00:00:00Z' });
      expect(await result).toEqual({ name: 'Моя ГО', logoUrl: '/files/logo.png' });
    });

    it.each([[null], [''], [undefined]])('treats a missing logo link (%s) as no logo', async (logoUrl) => {
      const result = service.fetch();
      http.expectOne(url).flush({ name: 'Моя ГО', logoUrl });
      expect(await result).toEqual({ name: 'Моя ГО', logoUrl: null });
    });

    it('returns null without a name', async () => {
      const result = service.fetch();
      http.expectOne(url).flush({ name: '  ', logoUrl: '/x.png' });
      expect(await result).toBeNull();
    });

    it.each([403, 404, 500])('returns null on HTTP %i', async (status) => {
      const result = service.fetch();
      http.expectOne(url).flush({}, { status, statusText: 'x' });
      expect(await result).toBeNull();
    });
  });

  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [500, 'error'],
  ])('maps HTTP %i to %s', async (status, expected) => {
    const result = service.create('Моя ГО');
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(expected);
  });

  it('reports an unreachable server', async () => {
    const result = service.create('Моя ГО');
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });
});
