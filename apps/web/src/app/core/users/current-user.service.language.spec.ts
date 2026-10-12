import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { LanguageService } from '../i18n/language.service';
import { CurrentUserService } from './current-user.service';

/** Мова інтерфейсу за `preferredLanguage` профілю: перший запит, оновлення й збереження. */
describe('CurrentUserService and the interface language', () => {
  let http: HttpTestingController;
  let service: CurrentUserService;
  let language: LanguageService;
  let server: string;
  const profile = (preferredLanguage: string | null) => ({
    id: 'u1',
    email: 'a@b.org',
    firstName: 'Олена',
    lastName: 'Іваненко',
    preferredLanguage,
    roles: [],
    customFields: [],
  });

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('yavir.language', 'uk');
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1' });
    TestBed.tick();
    language = TestBed.inject(LanguageService);
    service = TestBed.inject(CurrentUserService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  async function load(preferredLanguage: string | null): Promise<void> {
    const result = service.ensure();
    http.expectOne(`${server}/api/v1/users/me`).flush(profile(preferredLanguage));
    await result;
    http.match(`${server}/api/v1/ngo`);
  }

  it('switches the interface to the preferred language of /users/me on the first request', async () => {
    await load('en');
    expect(language.language()).toBe('en');
    expect(localStorage.getItem('yavir.language')).toBe('en');
  });

  it('switches the interface when the detailed profile is refreshed', async () => {
    await load('uk');
    expect(language.language()).toBe('uk');

    const refreshing = service.refresh();
    http.expectOne(`${server}/api/v1/users/u1`).flush(profile('crh'));
    await refreshing;
    expect(language.language()).toBe('crh');
  });

  it('switches the interface right after the changes of the profile are saved', async () => {
    await load('uk');
    service.applyLocal({ preferredLanguage: 'be' });
    expect(language.language()).toBe('be');
  });

  it('keeps the current language when the profile has none, or one without a translation file', async () => {
    await load(null);
    expect(language.language()).toBe('uk');

    service.applyLocal({ preferredLanguage: 'pl' });
    expect(language.language()).toBe('uk');

    service.applyLocal({ preferredLanguage: null });
    expect(language.language()).toBe('uk');
  });

  it('does not touch the language service when the profile has no language', async () => {
    const created = vi.fn();
    const original = TestBed.inject(LanguageService);
    const spy = vi.spyOn(original, 'applyPreferred').mockImplementation((...args) => {
      created(...args);
      return false;
    });
    await load(null);
    expect(spy).not.toHaveBeenCalled();
  });
});
