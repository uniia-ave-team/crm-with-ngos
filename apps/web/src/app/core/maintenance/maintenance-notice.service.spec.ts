import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { LanguageService } from '../i18n/language.service';
import { MaintenanceNoticeService } from './maintenance-notice.service';

describe('MaintenanceNoticeService', () => {
  let http: HttpTestingController;
  const HOUR = 60 * 60_000;
  const at = (offset: number) => new Date(Date.now() + offset).toISOString();

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const statusUrl = () => `${TestBed.inject(ConnectionService).serverUrl()}/api/v1/system/status`;

  /** Сервіс, сервер відповів «Healthy»; повертає запит статусу, який сервіс після цього надіслав. */
  function create() {
    const service = TestBed.inject(MaintenanceNoticeService);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl()).flush({ status: 'Healthy' });
    TestBed.tick();
    return { service, connection };
  }

  function answer(notice: unknown) {
    http.expectOne(statusUrl()).flush({ hasAdmin: true, hasNgo: true, isSetupComplete: true, version: '1.0.0', notice });
    TestBed.tick();
  }

  it('asks for the status of the system once the server is confirmed, and shows nothing without a notice', () => {
    const { service } = create();
    answer(null);
    expect(service.view()).toBeNull();
  });

  it('does not ask for the status while the saved accounts are being searched, and asks after the search', () => {
    const accounts = TestBed.inject(AccountsService);
    accounts.setSwitching(true);
    create();
    http.expectNone(statusUrl());

    accounts.setSwitching(false);
    TestBed.tick();
    answer(null);
  });

  it('shows the planned works with their start and end and the message of the server', () => {
    const { service } = create();
    const [startsAt, endsAt] = [at(HOUR), at(2 * HOUR)];
    answer({ message: '  Оновлення сервера.  ', startsAt, endsAt });

    const view = service.view();
    expect(view?.titleKey).toBe('common.maintenance.scheduled');
    expect(view?.message).toBe('Оновлення сервера.');
    // Дати — мовою інтерфейсу.
    const format = new Intl.DateTimeFormat(TestBed.inject(LanguageService).language(), { dateStyle: 'medium', timeStyle: 'short' });
    expect(view?.params).toEqual({ start: format.format(Date.parse(startsAt)), end: format.format(Date.parse(endsAt)) });
  });

  it('says the works are going on once they have started', () => {
    const { service } = create();
    answer({ message: 'Оновлення.', startsAt: at(-HOUR), endsAt: at(HOUR) });
    expect(service.view()?.titleKey).toBe('common.maintenance.ongoing');
  });

  it.each([
    ['the works are over', { message: 'Оновлення.', startsAt: at(-2 * HOUR), endsAt: at(-HOUR) }],
    ['the dates cannot be read', { message: 'Оновлення.', startsAt: 'soon', endsAt: 'later' }],
  ])('shows nothing when %s', (_name, notice) => {
    const { service } = create();
    answer(notice);
    expect(service.view()).toBeNull();
  });

  it('hides a dismissed notice, but shows a different one again', () => {
    const { service, connection } = create();
    answer({ message: 'Оновлення.', startsAt: at(HOUR), endsAt: at(2 * HOUR) });
    service.dismiss();
    expect(service.view()).toBeNull();

    // Нова перевірка підключення (інший акаунт, сервер) — статус запитується заново.
    void connection.checkApi();
    http.expectOne(connection.apiHealthUrl()).flush({ status: 'Healthy' });
    TestBed.tick();
    answer({ message: 'Оновлення бази.', startsAt: at(HOUR), endsAt: at(2 * HOUR) });
    expect(service.view()?.message).toBe('Оновлення бази.');
  });
});
