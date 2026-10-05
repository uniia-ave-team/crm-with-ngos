import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from './connection.service';

const STORAGE_KEY = 'yavir.serverAddress';

describe('ConnectionService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
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

  // Сервіс сам стартує перевірку в конструкторі, тож запит з'являється одразу.
  function create() {
    const service = TestBed.inject(ConnectionService);
    const request = http.expectOne(service.apiHealthUrl());
    return { service, request };
  }

  describe('without a provided server address', () => {
    it('takes the domain from the current page and targets port 5065 /health', () => {
      const { service, request } = create();
      expect(service.domain).toBe(location.hostname);
      expect(service.apiHealthUrl()).toBe(`${location.protocol}//${location.hostname}:5065/health`);
      expect(request.request.method).toBe('GET');
    });

    it('is "checking" until the response arrives', () => {
      const { service } = create();
      expect(service.apiStatus()).toBe('checking');
      expect(service.connected()).toBe(false);
    });

    it('reports "healthy" when /health answers Healthy', () => {
      const { service, request } = create();
      request.flush({ status: 'Healthy' });
      expect(service.apiStatus()).toBe('healthy');
      expect(service.connected()).toBe(true);
      expect(console.log).toHaveBeenCalledWith(expect.stringContaining('API існує й працює'));
    });

    it('reports "unhealthy" when /health answers 200 but not Healthy', () => {
      const { service, request } = create();
      request.flush({ status: 'Degraded' });
      expect(service.apiStatus()).toBe('unhealthy');
      expect(console.warn).toHaveBeenCalled();
    });

    it('reports "unhealthy" when /health answers an HTTP error such as 503', () => {
      const { service, request } = create();
      request.flush({ status: 'Unhealthy' }, { status: 503, statusText: 'Service Unavailable' });
      expect(service.apiStatus()).toBe('unhealthy');
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('HTTP 503'));
    });

    it('reports "unhealthy" when something else answers on the port (404)', () => {
      const { service, request } = create();
      request.flush('Not found', { status: 404, statusText: 'Not Found' });
      expect(service.apiStatus()).toBe('unhealthy');
    });

    it('reports "unreachable" when there is no response at all', () => {
      const { service, request } = create();
      request.error(new ProgressEvent('error'));
      expect(service.apiStatus()).toBe('unreachable');
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('API недоступне'));
    });

    it('can re-check the API on demand', () => {
      const { service, request } = create();
      request.flush({ status: 'Healthy' });
      expect(service.apiStatus()).toBe('healthy');

      service.checkApi();
      expect(service.apiStatus()).toBe('checking');
      http.expectOne(service.apiHealthUrl()).error(new ProgressEvent('error'));
      expect(service.apiStatus()).toBe('unreachable');
    });
  });

  describe('with a provided server address', () => {
    it('re-runs the whole check against the provided address, not the page domain', () => {
      const { service, request } = create();
      request.error(new ProgressEvent('error'));
      expect(service.apiStatus()).toBe('unreachable');

      expect(service.setServerAddress('crm.example.org')).toBe(true);
      expect(service.serverAddress()).toBe('crm.example.org');
      expect(service.apiStatus()).toBe('checking');

      const retry = http.expectOne(`${location.protocol}//crm.example.org:5065/health`);
      retry.flush({ status: 'Healthy' });
      expect(service.apiStatus()).toBe('healthy');
      expect(service.connected()).toBe(true);
      expect(localStorage.getItem(STORAGE_KEY)).toBe('crm.example.org');
    });

    it('keeps an explicit scheme and port and drops the path', () => {
      const { service, request } = create();
      request.error(new ProgressEvent('error'));

      service.setServerAddress('https://crm.example.org:8443/some/path?x=1');
      expect(service.serverUrl()).toBe('https://crm.example.org:8443');
      http.expectOne('https://crm.example.org:8443/health').error(new ProgressEvent('error'));
    });

    it('accepts a bare IP address', () => {
      const { service, request } = create();
      request.error(new ProgressEvent('error'));

      service.setServerAddress('192.168.0.5');
      http.expectOne(`${location.protocol}//192.168.0.5:5065/health`).error(new ProgressEvent('error'));
    });

    it.each(['', '   ', 'http://', 'ftp://crm.example.org', 'not a host'])(
      'rejects "%s" without any request and without changing state',
      (value) => {
        const { service, request } = create();
        request.error(new ProgressEvent('error'));

        expect(service.setServerAddress(value)).toBe(false);
        expect(service.serverAddress()).toBeNull();
        expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
        http.expectNone(() => true);
      },
    );

    it('uses a persisted address from the very first check', () => {
      localStorage.setItem(STORAGE_KEY, '192.168.0.7');
      const service = TestBed.inject(ConnectionService);
      expect(service.serverAddress()).toBe('192.168.0.7');
      http.expectOne(`${location.protocol}//192.168.0.7:5065/health`);
    });

    it('cancels a check still in flight when a new address is provided', () => {
      const { service, request } = create();
      service.setServerAddress('crm.example.org');
      expect(request.cancelled).toBe(true);
      http.expectOne(`${location.protocol}//crm.example.org:5065/health`);
    });

    it('goes back to the page domain when the address is cleared', () => {
      const { service, request } = create();
      request.error(new ProgressEvent('error'));
      service.setServerAddress('crm.example.org');
      http.expectOne(`${location.protocol}//crm.example.org:5065/health`).error(new ProgressEvent('error'));

      service.clearServerAddress();
      expect(service.serverAddress()).toBeNull();
      expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
      http.expectOne(`${location.protocol}//${location.hostname}:5065/health`);
    });
  });

  describe('system status', () => {
    const statusUrl = (base: string) => `${base}/api/v1/system/status`;

    it('requests /api/v1/system/status on the connected server and logs the result', () => {
      const { service, request } = create();
      request.flush({ status: 'Healthy' });

      service.loadSystemStatus();
      const status = http.expectOne(statusUrl(service.serverUrl()));
      expect(status.request.method).toBe('GET');
      status.flush({ hasAdmin: true, hasNgo: true, isSetupComplete: true });

      expect(service.systemStatus()).toEqual({ hasAdmin: true, hasNgo: true, isSetupComplete: true });
      expect(console.log).toHaveBeenCalledWith(
        expect.stringContaining('Статус системи отримано'),
        { hasAdmin: true, hasNgo: true, isSetupComplete: true },
      );
    });

    it('uses the provided server address for the request', () => {
      const { service, request } = create();
      request.error(new ProgressEvent('error'));
      service.setServerAddress('https://crm.example.org:8443');
      http.expectOne('https://crm.example.org:8443/health').flush({ status: 'Healthy' });

      service.loadSystemStatus();
      http.expectOne(statusUrl('https://crm.example.org:8443')).flush({
        hasAdmin: false,
        hasNgo: false,
        isSetupComplete: false,
      });
      expect(service.systemStatus()?.isSetupComplete).toBe(false);
    });

    it('warns and keeps no status when the request fails', () => {
      const { service, request } = create();
      request.flush({ status: 'Healthy' });

      service.loadSystemStatus();
      http.expectOne(statusUrl(service.serverUrl())).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(service.systemStatus()).toBeNull();
      expect(console.warn).toHaveBeenCalledWith(expect.stringContaining('HTTP 500'));
    });

    it('forgets a stale status when the connection is checked again', () => {
      const { service, request } = create();
      request.flush({ status: 'Healthy' });
      service.loadSystemStatus();
      http.expectOne(statusUrl(service.serverUrl())).flush({ hasAdmin: true, hasNgo: true, isSetupComplete: true });
      expect(service.systemStatus()).not.toBeNull();

      service.checkApi();
      expect(service.systemStatus()).toBeNull();
      http.expectOne(service.apiHealthUrl());
    });
  });
});
