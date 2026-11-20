import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { AccountsService } from '../../accounts/accounts.service';
import { ConnectionService } from '../../connection/connection.service';
import { LayoutModule } from '../layout.module';
import { DemoBannerComponent } from './demo-banner.component';

describe('DemoBannerComponent', () => {
  const server = 'https://demo.example.org';
  const status = { hasAdmin: true, hasNgo: true, isSetupComplete: true };
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [IonicModule.forRoot(), LayoutModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    }).compileComponents();
    // Тексти живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  /** Користувач уже ввійшов (сторінку перезавантажено): сервер відповідає таким статусом системи. */
  function render(systemStatus: object) {
    TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'user-1' });
    const connection = TestBed.inject(ConnectionService);
    const fixture = TestBed.createComponent(DemoBannerComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const answer = () => {
      http.expectOne(`${server}/health`).flush({ status: 'Healthy' });
      http.expectOne(`${server}/api/v1/system/status`).flush(systemStatus);
      fixture.detectChanges();
    };
    return { fixture, element, connection, answer };
  }

  it('says it is a demo, that only the visitor sees the changes and when they are reset', () => {
    const { element, answer } = render({ ...status, demo: { accounts: [], sessionIdleMinutes: 45, sessionMaxMinutes: 180 } });
    // Статус системи ще не отримано — банера нема.
    expect(element.querySelector('.demo-banner')).toBeNull();

    answer();
    const banner = element.querySelector('.demo-banner')!;
    expect(banner.textContent?.trim()).toBe(uk.demo.banner.replace('{{minutes}}', '45'));
    expect(banner.getAttribute('role')).toBe('status');
  });

  it('cannot be dismissed: there is nothing to press in it', () => {
    const { element, answer } = render({ ...status, demo: { accounts: [], sessionIdleMinutes: 30, sessionMaxMinutes: 120 } });
    answer();
    expect(element.querySelector('.demo-banner button, .demo-banner ion-button, .demo-banner a')).toBeNull();
  });

  it('stays while the connection is checked again', () => {
    const { fixture, element, connection, answer } = render({
      ...status,
      demo: { accounts: [], sessionIdleMinutes: 30, sessionMaxMinutes: 120 },
    });
    answer();

    void connection.checkApi();
    fixture.detectChanges();
    expect(element.querySelector('.demo-banner')).not.toBeNull();
  });

  it.each([
    ['has no demo field', status],
    ['says demo is null', { ...status, demo: null }],
  ])('shows nothing when the status %s', (_name, systemStatus) => {
    const { element, answer } = render(systemStatus);
    answer();
    expect(element.querySelector('.demo-banner')).toBeNull();
    expect(element.textContent?.trim()).toBe('');
  });
});
