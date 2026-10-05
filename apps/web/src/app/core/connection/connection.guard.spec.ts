import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable } from 'rxjs';

import { connectionGuard } from './connection.guard';
import { ConnectionService } from './connection.service';

describe('connectionGuard', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  /** Викликає guard і збирає його єдину відповідь. */
  function runGuard() {
    const connection = TestBed.inject(ConnectionService);
    const request = http.expectOne(connection.apiHealthUrl());
    const results: Array<boolean | UrlTree> = [];
    const result = TestBed.runInInjectionContext(() =>
      connectionGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Observable<boolean | UrlTree>;
    result.subscribe((value) => results.push(value));
    return { connection, request, results };
  }

  it('waits for the connection check instead of deciding early', () => {
    const { results } = runGuard();
    TestBed.tick();
    expect(results).toEqual([]);
  });

  it('lets the navigation through when the API is up', () => {
    const { request, results } = runGuard();
    request.flush({ status: 'Healthy' });
    TestBed.tick();
    expect(results).toEqual([true]);
  });

  it.each([
    ['no response at all', (request: { error(e: ProgressEvent): void }) => request.error(new ProgressEvent('error'))],
    [
      'an unhealthy API',
      (request: { flush(b: unknown, o: { status: number; statusText: string }): void }) =>
        request.flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' }),
    ],
  ])('redirects to /login with the no-connection error on %s', (_name, fail) => {
    const { request, results } = runGuard();
    (fail as (r: unknown) => void)(request);
    TestBed.tick();

    expect(results).toHaveLength(1);
    expect(results[0]).toBeInstanceOf(UrlTree);
    expect((results[0] as UrlTree).toString()).toBe('/login?error=no-connection');
  });

  it('judges by the saved server address when there is one', () => {
    localStorage.setItem('yavir.serverAddress', '192.168.0.7');
    const connection = TestBed.inject(ConnectionService);
    const request = http.expectOne(`${location.protocol}//192.168.0.7:5065/health`);
    expect(connection.serverAddress()).toBe('192.168.0.7');

    const results: Array<boolean | UrlTree> = [];
    (
      TestBed.runInInjectionContext(() =>
        connectionGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
      ) as Observable<boolean | UrlTree>
    ).subscribe((value) => results.push(value));

    request.error(new ProgressEvent('error'));
    TestBed.tick();
    expect((results[0] as UrlTree).toString()).toBe('/login?error=no-connection');
  });
});
