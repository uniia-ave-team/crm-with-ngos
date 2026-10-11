import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let http: HttpTestingController;
  let service: UsersService;
  let url: string;
  const user = { firstName: 'Олена', lastName: 'Іваненко', email: 'a@b.org', password: 'Abcde1', confirmPassword: 'Abcde1' };

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    service = TestBed.inject(UsersService);
    url = `${connection.serverUrl()}/api/v1/users`;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('posts the user data as is', async () => {
    const result = service.create(user);
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(user);
    request.flush('id', { status: 201, statusText: 'Created' });
    expect(await result).toBe('created');
  });

  it.each([
    [400, 'invalid'],
    [500, 'error'],
  ])('maps HTTP %i to %s', async (status, expected) => {
    const result = service.create(user);
    http.expectOne(url).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(expected);
  });

  it('reports an unreachable server', async () => {
    const result = service.create(user);
    http.expectOne(url).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });
});
