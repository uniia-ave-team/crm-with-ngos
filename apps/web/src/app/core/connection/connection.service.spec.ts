import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService, parseServerAddress, serverAddressCandidates } from './connection.service';

describe('ConnectionService', () => {
  let http: HttpTestingController;

  /** Origin самої сторінки: тут API шукаємо першим (зворотний проксі). */
  const sameOrigin = location.origin;
  /** Той самий домен із портом API за замовчуванням (локальний `docker compose`). */
  const apiPort = `${location.protocol}//${location.hostname}:5065`;
  const health = (server: string) => `${server}/health`;
  const statusUrl = (server: string) => `${server}/api/v1/system/status`;
  const status = { hasAdmin: true, hasNgo: true, isSetupComplete: true };
  const demo = {
    accounts: [{ name: 'Олена', email: 'olena@demo.example', password: 'Demo-1a' }],
    sessionMinutes: 60,
  };

  /** Відповіді «тут нашого API нема». */
  const noResponse = (request: TestRequest) => request.error(new ProgressEvent('error'));
  const notFound = (request: TestRequest) => request.flush('Not found', { status: 404, statusText: 'Not Found' });
  /** SPA-фолбек веб-контейнера: `index.html` зі статусом 200 — як JSON він не розбирається. */
  const appShell = (request: TestRequest) => request.error(new ProgressEvent('error'), { status: 200, statusText: 'OK' });

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  // Сервіс сам стартує пошук у конструкторі: обидва кандидати домену сторінки запитуються одразу.
  function create() {
    const service = TestBed.inject(ConnectionService);
    const origin = http.expectOne(health(sameOrigin));
    const port = http.expectOne(health(apiPort));
    return { service, origin, port };
  }

  /** За origin сторінки нічого нашого (404), тож лишається кандидат із портом 5065. */
  function createOnApiPort() {
    const { service, origin, port } = create();
    notFound(origin);
    return { service, request: port };
  }

  /** API знайдено на порту 5065 і воно здорове; запит статусу системи, що йде слідом, повертається. */
  function connect() {
    const { service, request } = createOnApiPort();
    request.flush({ status: 'Healthy' });
    return { service, systemStatus: http.expectOne(statusUrl(apiPort)) };
  }

  /** Жодного API за доменом сторінки. */
  function createDisconnected() {
    const { service, origin, port } = create();
    noResponse(origin);
    noResponse(port);
    return service;
  }

  describe('without a provided server address', () => {
    it('takes the domain from the current page and looks for the API on the page origin and on port 5065', () => {
      const { service, origin, port } = create();
      expect(service.domain).toBe(location.hostname);
      expect(origin.request.method).toBe('GET');
      expect(port.request.method).toBe('GET');
      // Поки нічого не знайдено, ціль — перший кандидат.
      expect(service.serverUrl()).toBe(sameOrigin);
      expect(service.apiHealthUrl()).toBe(health(sameOrigin));
    });

    it('is "checking" until the responses arrive', () => {
      const { service } = create();
      expect(service.apiStatus()).toBe('checking');
      expect(service.connected()).toBe(false);
    });

    it('finds the API on the origin of the page itself: it becomes the server and the page server', () => {
      const { service, origin, port } = create();
      origin.flush({ status: 'Healthy' });

      expect(service.apiStatus()).toBe('healthy');
      expect(service.connected()).toBe(true);
      expect(service.serverUrl()).toBe(sameOrigin);
      expect(service.pageServerUrl).toBe(sameOrigin);
      // Менш пріоритетний кандидат більше не потрібен.
      expect(port.cancelled).toBe(true);
      http.expectOne(statusUrl(sameOrigin));
    });

    it('uses port 5065 when the page origin answers with the app shell (HTML, 200)', () => {
      const { service, origin, port } = create();
      appShell(origin);
      expect(service.apiStatus()).toBe('checking');
      port.flush({ status: 'Healthy' });

      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe(apiPort);
      expect(service.pageServerUrl).toBe(apiPort);
      expect(service.apiHealthUrl()).toBe(health(apiPort));
      http.expectOne(statusUrl(apiPort));
    });

    it.each<[string, (request: TestRequest) => void]>([
      ['a 404', notFound],
      ['a 404 with problem JSON (its numeric "status" is not ours)', (request) =>
        request.flush({ title: 'Not Found', status: 404 }, { status: 404, statusText: 'Not Found' })],
      ['text that is not JSON', (request) => request.flush('OK')],
      ['JSON without a status field', (request) => request.flush({ ok: true })],
      ['JSON with a status that is not a string', (request) => request.flush({ status: 200 })],
      ['an HTML error page', (request) => request.flush('<html></html>', { status: 502, statusText: 'Bad Gateway' })],
      ['no response', noResponse],
    ])('moves on to port 5065 when the page origin answers with %s', (_name, answer) => {
      const { service, origin, port } = create();
      answer(origin);
      expect(service.apiStatus()).toBe('checking');
      port.flush({ status: 'Healthy' });

      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe(apiPort);
      http.expectOne(statusUrl(apiPort));
    });

    it('respects the priority: a faster answer from port 5065 waits for the page origin', () => {
      const { service, origin, port } = create();
      port.flush({ status: 'Healthy' });
      expect(service.apiStatus()).toBe('checking');

      origin.flush({ status: 'Healthy' });
      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe(sameOrigin);
      http.expectOne(statusUrl(sameOrigin));
    });

    it('takes the answer port 5065 gave earlier once the page origin turns out not to be the API', () => {
      const { service, origin, port } = create();
      port.flush({ status: 'Healthy' });
      notFound(origin);

      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe(apiPort);
      http.expectOne(statusUrl(apiPort));
    });

    it('reports "unhealthy" when /health answers 200 but not Healthy', () => {
      const { service, request } = createOnApiPort();
      request.flush({ status: 'Degraded' });
      expect(service.apiStatus()).toBe('unhealthy');
      expect(service.serverUrl()).toBe(apiPort);
      expect(console.warn).toHaveBeenCalled();
    });

    it('reports "unhealthy" when /health answers an HTTP error such as 503', () => {
      const { service, request } = createOnApiPort();
      request.flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
      expect(service.apiStatus()).toBe('unhealthy');
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('HTTP 503'));
    });

    it('lets an unhealthy API on the page origin win: it is still our API', () => {
      const { service, origin, port } = create();
      origin.flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });

      expect(service.apiStatus()).toBe('unhealthy');
      expect(service.serverUrl()).toBe(sameOrigin);
      expect(port.cancelled).toBe(true);
    });

    it('reports "unhealthy" when something else answers on the last candidate (404)', () => {
      const { service, request } = createOnApiPort();
      notFound(request);
      expect(service.apiStatus()).toBe('unhealthy');
      // Нічого не знайдено — ціль лишається першим кандидатом.
      expect(service.serverUrl()).toBe(sameOrigin);
      expect(service.pageServerUrl).toBe(sameOrigin);
    });

    it('reports "unreachable" when the last candidate gives no response at all', () => {
      const { service, origin, port } = create();
      appShell(origin);
      noResponse(port);
      expect(service.apiStatus()).toBe('unreachable');
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('API недоступне'));
    });

    it('can re-check the API on demand', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);
      expect(service.apiStatus()).toBe('healthy');

      service.checkApi();
      expect(service.apiStatus()).toBe('checking');
      // Знайдений сервер лишається ціллю, поки нова перевірка не скаже інше.
      expect(service.serverUrl()).toBe(apiPort);
      noResponse(http.expectOne(health(sameOrigin)));
      noResponse(http.expectOne(health(apiPort)));
      expect(service.apiStatus()).toBe('unreachable');
      // Сервер, що замовк, нікуди не переїхав: ціль — там, де його знаходили.
      expect(service.serverUrl()).toBe(apiPort);
      expect(service.pageServerUrl).toBe(apiPort);
    });
  });

  describe('the server of the page domain and the one-off probes', () => {
    it('keeps the server of the page domain even when an active account points elsewhere', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);
      TestBed.inject(AccountsService).add({
        server: 'https://other.example:5065',
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'someone',
      });

      expect(service.serverUrl()).toBe('https://other.example:5065');
      expect(service.pageServerUrl).toBe(apiPort);
    });

    it('probes the given server without touching the state of the service', async () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);

      const probe = service.probeApi('https://invited.example:5065');
      noResponse(http.expectOne('https://invited.example:5065/health'));

      expect(await probe).toBe(false);
      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe(apiPort);
    });

    it('probes the current server by default', async () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);

      const probe = service.probeApi();
      http.expectOne(health(apiPort)).flush({ status: 'Healthy' });
      expect(await probe).toBe(true);
    });

    it('finds the server of the page domain for an invitation while an account of another server is active', async () => {
      TestBed.inject(AccountsService).add({
        server: 'https://other.example:5065',
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'someone',
      });
      const service = TestBed.inject(ConnectionService);
      const accountCheck = http.expectOne('https://other.example:5065/health');

      const found = service.findPageServer();
      appShell(http.expectOne(health(sameOrigin)));
      http.expectOne(health(apiPort)).flush({ status: 'Healthy' });

      expect(await found).toBe(apiPort);
      expect(service.pageServerUrl).toBe(apiPort);
      expect(service.serverUrl()).toBe('https://other.example:5065');
      expect(service.apiStatus()).toBe('checking');
      noResponse(accountCheck);
    });

    it('finds no page server when neither candidate is a healthy API', async () => {
      const service = createDisconnected();

      const found = service.findPageServer();
      notFound(http.expectOne(health(sameOrigin)));
      http.expectOne(health(apiPort)).flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
      expect(await found).toBeNull();
    });

    it('resolves a manual address without a port: as typed first', async () => {
      const service = createDisconnected();

      const resolved = service.resolveServer('https://crm.example.org');
      http.expectOne('https://crm.example.org/health').flush({ status: 'Healthy' });
      expect(http.expectOne('https://crm.example.org:5065/health').cancelled).toBe(true);

      expect(await resolved).toBe('https://crm.example.org');
      // Стан сервісу не змінився.
      expect(service.apiStatus()).toBe('unreachable');
      expect(service.serverAddress()).toBeNull();
    });

    it('resolves a manual address without a port: then with port 5065', async () => {
      const service = createDisconnected();

      const resolved = service.resolveServer('https://crm.example.org');
      appShell(http.expectOne('https://crm.example.org/health'));
      http.expectOne('https://crm.example.org:5065/health').flush({ status: 'Healthy' });

      expect(await resolved).toBe('https://crm.example.org:5065');
    });

    it('resolves a manual address with a port: only that port is tried', async () => {
      const service = createDisconnected();

      const resolved = service.resolveServer('https://crm.example.org:8443');
      noResponse(http.expectOne('https://crm.example.org:8443/health'));

      expect(await resolved).toBeNull();
      http.expectNone('https://crm.example.org:5065/health');
    });

    it('resolves nothing, without a request, for something that is not an address', async () => {
      const service = createDisconnected();
      expect(await service.resolveServer('not a host')).toBeNull();
    });
  });

  describe('with a provided server address', () => {
    it('re-runs the whole check against the provided address, not the page domain', () => {
      const service = createDisconnected();
      expect(service.apiStatus()).toBe('unreachable');

      expect(service.setServerAddress('crm.example.org')).toBe(true);
      expect(service.serverAddress()).toBe('crm.example.org');
      expect(service.apiStatus()).toBe('checking');

      // Без порту: спершу адреса як введена (стандартний порт).
      const typed = `${location.protocol}//crm.example.org`;
      http.expectOne(health(typed)).flush({ status: 'Healthy' });
      expect(http.expectOne(health(`${typed}:5065`)).cancelled).toBe(true);
      expect(service.apiStatus()).toBe('healthy');
      expect(service.connected()).toBe(true);
      expect(service.serverUrl()).toBe(typed);
      // Сервер домену сторінки від наданої адреси не залежить.
      expect(service.pageServerUrl).toBe(sameOrigin);
      http.expectOne(statusUrl(typed));
      // Надана адреса живе лише в памʼяті: у сховище вона потрапляє разом з акаунтом.
      expect(localStorage.length).toBe(0);
    });

    it('falls back to port 5065 of the provided address when nothing of ours answers as typed', () => {
      const service = createDisconnected();

      service.setServerAddress('crm.example.org');
      const typed = `${location.protocol}//crm.example.org`;
      expect(service.serverUrl()).toBe(typed);
      noResponse(http.expectOne(health(typed)));
      http.expectOne(health(`${typed}:5065`)).flush({ status: 'Healthy' });

      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe(`${typed}:5065`);
      http.expectOne(statusUrl(`${typed}:5065`));
    });

    it('keeps an explicit scheme and port and drops the path: only that port is tried', () => {
      const service = createDisconnected();

      service.setServerAddress('https://crm.example.org:8443/some/path?x=1');
      expect(service.serverUrl()).toBe('https://crm.example.org:8443');
      noResponse(http.expectOne('https://crm.example.org:8443/health'));
      expect(service.apiStatus()).toBe('unreachable');
    });

    it('accepts a bare IP address', () => {
      const service = createDisconnected();

      service.setServerAddress('192.168.0.5');
      noResponse(http.expectOne(`${location.protocol}//192.168.0.5/health`));
      noResponse(http.expectOne(`${location.protocol}//192.168.0.5:5065/health`));
      expect(service.apiStatus()).toBe('unreachable');
      expect(service.serverUrl()).toBe(`${location.protocol}//192.168.0.5`);
    });

    it.each(['', '   ', 'http://', 'ftp://crm.example.org', 'not a host'])(
      'rejects "%s" without any request and without changing state',
      (value) => {
        const service = createDisconnected();

        expect(service.setServerAddress(value)).toBe(false);
        expect(service.serverAddress()).toBeNull();
        expect(localStorage.length).toBe(0);
        http.expectNone(() => true);
      },
    );

    it('uses only the saved connection address of the active account (an old account with :5065 keeps working)', () => {
      TestBed.inject(AccountsService).add({
        server: 'http://192.168.0.7:5065',
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'user-1',
      });
      const service = TestBed.inject(ConnectionService);
      expect(service.serverUrl()).toBe('http://192.168.0.7:5065');

      // Жодних інших кандидатів: ні origin сторінки, ні адреси без порту.
      const requests = http.match(() => true);
      expect(requests.map((request) => request.request.url)).toEqual(['http://192.168.0.7:5065/health']);
      requests[0].flush({ status: 'Healthy' });

      expect(service.apiStatus()).toBe('healthy');
      expect(service.serverUrl()).toBe('http://192.168.0.7:5065');
      // Статус системи запитується й тоді, коли користувач уже ввійшов.
      http.expectOne(statusUrl('http://192.168.0.7:5065'));
    });

    it('learns the page server from an active account that lives on it, and stays there when the account is gone', async () => {
      const accounts = TestBed.inject(AccountsService);
      const account = accounts.add({ server: apiPort, accessToken: 'a', refreshToken: 'r', identity: 'user-1' });
      const service = TestBed.inject(ConnectionService);
      http.expectOne(health(apiPort)).flush({ status: 'Healthy' });
      http.expectOne(statusUrl(apiPort)).flush(status);
      expect(service.pageServerUrl).toBe(apiPort);

      accounts.remove(account.id);
      expect(service.serverUrl()).toBe(apiPort);
      // Сервер той самий, що його щойно перевіряли: нової перевірки не треба.
      expect(await service.checkApiIfServerChanged()).toBe('healthy');
      expect(service.systemStatus()).toEqual(status);
      http.expectNone(() => true);
    });

    it('checks the connection again when the server changed because the active account is gone', () => {
      const accounts = TestBed.inject(AccountsService);
      const account = accounts.add({ server: 'https://other.example:5065', accessToken: 'a', refreshToken: 'r', identity: 'u' });
      const service = TestBed.inject(ConnectionService);
      http.expectOne('https://other.example:5065/health').flush({ status: 'Healthy' });
      http.expectOne(statusUrl('https://other.example:5065')).flush(status);

      accounts.remove(account.id);
      expect(service.serverUrl()).toBe(sameOrigin);
      void service.checkApiIfServerChanged();

      expect(service.apiStatus()).toBe('checking');
      expect(service.systemStatus()).toBeNull();
      http.expectOne(health(sameOrigin));
      http.expectOne(health(apiPort));
    });

    it('prefers the active account over a typed address, and falls back to the typed one without an account', () => {
      const { service } = create();
      service.setServerAddress('typed.example.org');
      const typed = `${location.protocol}//typed.example.org`;
      expect(service.serverUrl()).toBe(typed);
      http.expectOne(health(typed));
      http.expectOne(health(`${typed}:5065`));

      const accounts = TestBed.inject(AccountsService);
      accounts.add({ server: 'http://account.example.org:5065', accessToken: 'a', refreshToken: 'r', identity: 'u' });
      expect(service.serverUrl()).toBe('http://account.example.org:5065');

      accounts.setActive(null);
      expect(service.serverUrl()).toBe(typed);
    });

    it('returns the final status of the check from checkApi()', async () => {
      const service = createDisconnected();
      const result = service.checkApi();
      notFound(http.expectOne(health(sameOrigin)));
      http.expectOne(health(apiPort)).flush({ status: 'Healthy' });
      expect(await result).toBe('healthy');
      http.expectOne(statusUrl(apiPort));
    });

    it('cancels a check still in flight when a new address is provided', () => {
      const { service, origin, port } = create();
      service.setServerAddress('crm.example.org:5065');
      expect(origin.cancelled).toBe(true);
      expect(port.cancelled).toBe(true);
      http.expectOne(`${location.protocol}//crm.example.org:5065/health`);
    });

    it('gives the one who waited for a cancelled check the result of the check that replaced it, not "checking"', async () => {
      const service = createDisconnected();

      const first = service.checkApi();
      const second = service.checkApi();
      const requests = http.match(health(sameOrigin));
      expect(requests.map((r) => r.cancelled)).toEqual([true, false]);
      expect(http.match(health(apiPort)).map((r) => r.cancelled)).toEqual([true, false]);
      requests[1].flush({ status: 'Healthy' });

      expect(await second).toBe('healthy');
      expect(await first).toBe('healthy');
      http.expectOne(statusUrl(sameOrigin));
    });

    it('goes back to the page domain when the address is cleared', () => {
      const service = createDisconnected();
      service.setServerAddress('crm.example.org:5065');
      noResponse(http.expectOne(`${location.protocol}//crm.example.org:5065/health`));

      service.clearServerAddress();
      expect(service.serverAddress()).toBeNull();
      expect(service.serverUrl()).toBe(sameOrigin);
      http.expectOne(health(sameOrigin));
      http.expectOne(health(apiPort));
    });
  });

  describe('system status', () => {
    it('requests /api/v1/system/status as soon as the API is connected', () => {
      const { service, systemStatus } = connect();
      expect(service.systemStatusState()).toBe('loading');
      expect(systemStatus.request.method).toBe('GET');
      systemStatus.flush(status);

      expect(service.systemStatus()).toEqual(status);
      expect(service.systemStatusState()).toBe('loaded');
    });

    it('does not request the status of an API that is not healthy', () => {
      const { service, request } = createOnApiPort();
      request.flush({ status: 'Degraded' });
      expect(service.systemStatusState()).toBe('idle');
      http.expectNone(statusUrl(apiPort));
    });

    it('requests the status again on demand once the previous request has finished', async () => {
      const { service, systemStatus } = connect();
      systemStatus.flush({ ...status, hasNgo: false, isSetupComplete: false });

      const again = service.loadSystemStatus();
      expect(service.systemStatus()).toBeNull();
      http.expectOne(statusUrl(apiPort)).flush(status);
      expect(await again).toEqual(status);
      expect(service.systemStatus()?.isSetupComplete).toBe(true);
    });

    it('uses the provided server address for the request', () => {
      const service = createDisconnected();
      service.setServerAddress('https://crm.example.org:8443');
      http.expectOne('https://crm.example.org:8443/health').flush({ status: 'Healthy' });

      http.expectOne(statusUrl('https://crm.example.org:8443')).flush({
        hasAdmin: false,
        hasNgo: false,
        isSetupComplete: false,
      });
      expect(service.systemStatus()?.isSetupComplete).toBe(false);
    });

    it('warns and keeps no status when the request fails', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush('boom', { status: 500, statusText: 'Server Error' });

      expect(service.systemStatus()).toBeNull();
      expect(service.systemStatusState()).toBe('failed');
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('HTTP 500'));
    });

    it.each([
      ['has no fields', {}],
      ['misses a field', { hasAdmin: true, hasNgo: true }],
      ['has a field of another type', { hasAdmin: 'yes', hasNgo: true, isSetupComplete: true }],
      ['is not an object', null],
    ])('fails when the answer %s: it is not the status of the contract, and nothing may be decided by it', async (_name, body) => {
      const { service, systemStatus } = connect();
      const result = service.loadSystemStatus();
      expect(systemStatus.cancelled).toBe(true);
      http.expectOne(statusUrl(apiPort)).flush(body);

      expect(await result).toBeNull();
      expect(service.systemStatus()).toBeNull();
      expect(service.systemStatusState()).toBe('failed');
    });

    it('gives the one who waited for a cancelled request the answer of the request that replaced it', async () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);

      const first = service.loadSystemStatus();
      const second = service.loadSystemStatus();
      const requests = http.match(statusUrl(apiPort));
      expect(requests.map((r) => r.cancelled)).toEqual([true, false]);
      requests[1].flush({ hasAdmin: true, hasNgo: false, isSetupComplete: false });

      expect(await first).toEqual({ hasAdmin: true, hasNgo: false, isSetupComplete: false });
      expect(await second).toEqual({ hasAdmin: true, hasNgo: false, isSetupComplete: false });
    });

    it('gives null for a request that a new check of the connection has cancelled: the target has changed', async () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);

      const result = service.loadSystemStatus();
      service.checkApi();

      expect(await result).toBeNull();
      expect(http.match(statusUrl(apiPort)).map((r) => r.cancelled)).toEqual([true]);
      http.expectOne(health(sameOrigin));
      http.expectOne(health(apiPort));
    });

    it('forgets a stale status when the connection is checked again', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush(status);
      expect(service.systemStatus()).not.toBeNull();

      service.checkApi();
      expect(service.systemStatus()).toBeNull();
      expect(service.systemStatusState()).toBe('idle');
      http.expectOne(health(sameOrigin));
      http.expectOne(health(apiPort));
    });
  });

  describe('demo mode', () => {
    it('is off when the status has no demo field or it is null', () => {
      const { service, systemStatus } = connect();
      expect(service.demo()).toBeNull();
      systemStatus.flush(status);
      expect(service.demo()).toBeNull();
      expect(service.isDemo()).toBe(false);

      service.loadSystemStatus();
      http.expectOne(statusUrl(apiPort)).flush({ ...status, demo: null });
      expect(service.isDemo()).toBe(false);
      expect(service.systemStatus()).toEqual(status);
    });

    it('exposes the demo accounts and session limits from the status', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush({ ...status, demo });

      expect(service.isDemo()).toBe(true);
      expect(service.demo()).toEqual(demo);
      expect(service.systemStatus()?.demo).toEqual(demo);
    });

    it('takes the length of the session given as a numeric string (the contract allows it)', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush({ ...status, demo: { ...demo, sessionMinutes: '45' } });
      expect(service.demo()?.sessionMinutes).toBe(45);
    });

    it.each([
      ['the accounts are not a list', { accounts: 'olena', sessionMinutes: 60 }],
      ['an account misses its password', { accounts: [{ name: 'Олена', email: 'olena@demo.example' }], sessionMinutes: 60 }],
      ['the length of the session is not a number', { accounts: [], sessionMinutes: 'soon' }],
      ['the length of the session is zero', { accounts: [], sessionMinutes: 0 }],
      ['it is not an object', 'yes'],
    ])('treats a demo that is not of the contract (%s) as no demo, but keeps the status', (_name, broken) => {
      const { service, systemStatus } = connect();
      systemStatus.flush({ ...status, demo: broken });

      expect(service.isDemo()).toBe(false);
      expect(service.systemStatus()).toEqual(status);
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('Демо-режим'), broken);
    });

    it('knows about the demo for a signed-in user after a reload (active account from the start)', () => {
      TestBed.inject(AccountsService).add({ server: apiPort, accessToken: 'a', refreshToken: 'r', identity: 'user-1' });
      const service = TestBed.inject(ConnectionService);
      http.expectOne(health(apiPort)).flush({ status: 'Healthy' });
      http.expectOne(statusUrl(apiPort)).flush({ ...status, demo });

      expect(service.demo()?.sessionMinutes).toBe(60);
    });

    it('keeps the demo while the connection is re-checked and the status is requested again', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush({ ...status, demo });

      service.checkApi();
      expect(service.systemStatus()).toBeNull();
      expect(service.isDemo()).toBe(true);
      notFound(http.expectOne(health(sameOrigin)));
      http.expectOne(health(apiPort)).flush({ status: 'Healthy' });
      expect(service.isDemo()).toBe(true);

      // Демо-режим на сервері вимкнули — новий статус це прибирає.
      http.expectOne(statusUrl(apiPort)).flush(status);
      expect(service.isDemo()).toBe(false);
    });

    it('does not carry the demo of one server over to another', () => {
      const { service, systemStatus } = connect();
      systemStatus.flush({ ...status, demo });
      expect(service.isDemo()).toBe(true);

      TestBed.inject(AccountsService).add({
        server: 'https://other.example:5065',
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'someone',
      });
      expect(service.isDemo()).toBe(false);
    });
  });
});

describe('parseServerAddress and serverAddressCandidates', () => {
  it('keeps the address as typed: no port is added', () => {
    expect(parseServerAddress('crm.example.org')?.origin).toBe(`${location.protocol}//crm.example.org`);
    expect(parseServerAddress('https://crm.example.org:8443/some/path?x=1')?.origin).toBe('https://crm.example.org:8443');
  });

  it('without a port tries the address as typed first, then port 5065', () => {
    expect(serverAddressCandidates('https://crm.example.org')).toEqual(['https://crm.example.org', 'https://crm.example.org:5065']);
    expect(serverAddressCandidates('192.168.0.5')).toEqual([
      `${location.protocol}//192.168.0.5`,
      `${location.protocol}//192.168.0.5:5065`,
    ]);
  });

  it('with an explicit port tries only that one, also when it is the default port of the scheme', () => {
    expect(serverAddressCandidates('http://192.168.0.7:5065')).toEqual(['http://192.168.0.7:5065']);
    expect(serverAddressCandidates('crm.example.org:8443/path')).toEqual([`${location.protocol}//crm.example.org:8443`]);
    expect(serverAddressCandidates('https://crm.example.org:443')).toEqual(['https://crm.example.org']);
    expect(serverAddressCandidates('http://crm.example.org:80/')).toEqual(['http://crm.example.org']);
  });

  it.each(['', '   ', 'http://', 'ftp://crm.example.org', 'not a host', null])('gives nothing for "%s"', (value) => {
    expect(parseServerAddress(value)).toBeNull();
    expect(serverAddressCandidates(value)).toEqual([]);
  });

  it('takes a Cyrillic domain (the .укр zone): the requests go to its Latin record, as typed and then with port 5065', () => {
    const url = parseServerAddress('приклад.укр');
    expect(url?.hostname).toBe('xn--80aikifvh.xn--j1amh');
    expect(url?.port).toBe('');
    expect(serverAddressCandidates('приклад.укр')).toEqual([
      `${location.protocol}//xn--80aikifvh.xn--j1amh`,
      `${location.protocol}//xn--80aikifvh.xn--j1amh:5065`,
    ]);
    expect(parseServerAddress('https://Приклад.укр:8443/path?x=1')?.origin).toBe('https://xn--80aikifvh.xn--j1amh:8443');
  });
});
