import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

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
