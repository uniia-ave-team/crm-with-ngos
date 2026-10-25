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
    expect(request.request.body).toEqual({ name: 'Моя ГО' });
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

  describe('uploadLogo', () => {
    const logoUrl = () => `${url}/logo`;
    const png = () => new File([new Uint8Array(5)], 'logo.png', { type: 'image/png' });
    const ok = { status: 200, statusText: 'OK' };

    it('posts the image itself as multipart/form-data in the field "file"', async () => {
      const result = service.uploadLogo(png());
      const request = http.expectOne(logoUrl());
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toBeInstanceOf(FormData);
      const sent = (request.request.body as FormData).get('file') as File;
      expect(sent).toBeInstanceOf(File);
      expect(sent.name).toBe('logo.png');
      expect(sent.type).toBe('image/png');
      // Content-Type ставить браузер (разом із boundary).
      expect(request.request.headers.has('Content-Type')).toBe(false);
      request.flush(null, ok);
      (await vi.waitFor(() => http.expectOne(url))).flush({ name: 'Org', logoUrl: '/files/logo.png' });
      expect(await result).toBe('uploaded');
    });

    it('reads the organization again after the upload and writes the new logo link into the active account', async () => {
      const accounts = TestBed.inject(AccountsService);
      const account = accounts.add({
        server: TestBed.inject(ConnectionService).serverUrl(),
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'u',
        ngoName: 'Org',
        ngoLogoUrl: null,
      });
      const result = service.uploadLogo(png());
      http.expectOne(logoUrl()).flush(null, ok);
      const read = await vi.waitFor(() => http.expectOne(url));
      expect(read.request.method).toBe('GET');
      read.flush({ name: 'Org', logoUrl: '/files/new.png' });
      expect(await result).toBe('uploaded');
      expect(JSON.parse(localStorage.getItem(`yavir.account.${account.id}`)!)).toMatchObject({
        ngoName: 'Org',
        ngoLogoUrl: '/files/new.png',
      });
    });

    it('still counts as uploaded when the organization cannot be read afterwards', async () => {
      const result = service.uploadLogo(png());
      http.expectOne(logoUrl()).flush(null, ok);
      (await vi.waitFor(() => http.expectOne(url))).flush({}, { status: 500, statusText: 'x' });
      expect(await result).toBe('uploaded');
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('returns the failure for HTTP %i (%s) and does not read the organization', async (status, expected) => {
      const result = service.uploadLogo(png());
      http.expectOne(logoUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
      http.expectNone(url);
    });

    it('reports an unreachable server', async () => {
      const result = service.uploadLogo(png());
      http.expectOne(logoUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('update', () => {
    it('puts only the name and treats 204 as updated', async () => {
      const result = service.update('Org');
      const request = http.expectOne(url);
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ name: 'Org' });
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe('updated');
    });

    it('writes the new name into the active account and keeps its logo', async () => {
      const accounts = TestBed.inject(AccountsService);
      const account = accounts.add({
        server: TestBed.inject(ConnectionService).serverUrl(),
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'u',
        ngoName: 'Old',
        ngoLogoUrl: '/old.png',
      });
      const result = service.update('New');
      http.expectOne(url).flush(null, { status: 204, statusText: 'No Content' });
      await result;
      expect(accounts.accounts().find((a) => a.id === account.id)).toMatchObject({ ngoName: 'New', ngoLogoUrl: '/old.png' });
    });

    it('writes the new name into localStorage, in the record of the account that sent the request', async () => {
      const accounts = TestBed.inject(AccountsService);
      const server = TestBed.inject(ConnectionService).serverUrl();
      const first = accounts.add({ server, accessToken: 'a', refreshToken: 'r', identity: 'first', ngoName: 'Old', ngoLogoUrl: '/old.png' });
      const second = accounts.add({ server, accessToken: 'a2', refreshToken: 'r2', identity: 'second', ngoName: 'Other' });
      accounts.setActive(first.id);

      const result = service.update('New');
      // Поки йде запит, активним став інший акаунт.
      accounts.setActive(second.id);
      http.expectOne(url).flush(null, { status: 204, statusText: 'No Content' });
      await result;

      const record = (id: string) => JSON.parse(localStorage.getItem(`yavir.account.${id}`)!);
      expect(record(first.id)).toMatchObject({ ngoName: 'New', ngoLogoUrl: '/old.png' });
      expect(record(second.id)).toMatchObject({ ngoName: 'Other' });
    });

    it('does not touch localStorage when the request fails', async () => {
      const accounts = TestBed.inject(AccountsService);
      const account = accounts.add({
        server: TestBed.inject(ConnectionService).serverUrl(),
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'u',
        ngoName: 'Old',
        ngoLogoUrl: '/old.png',
      });
      const result = service.update('New');
      http.expectOne(url).flush({}, { status: 400, statusText: 'x' });
      await result;
      expect(JSON.parse(localStorage.getItem(`yavir.account.${account.id}`)!)).toMatchObject({
        ngoName: 'Old',
        ngoLogoUrl: '/old.png',
      });
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('maps HTTP %i to %s and leaves the account alone', async (status, expected) => {
      const accounts = TestBed.inject(AccountsService);
      accounts.add({
        server: TestBed.inject(ConnectionService).serverUrl(),
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'u',
        ngoName: 'Old',
      });
      const result = service.update('New');
      http.expectOne(url).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
      expect(accounts.active()?.ngoName).toBe('Old');
    });

    it('reports an unreachable server', async () => {
      const result = service.update('Org');
      http.expectOne(url).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
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
