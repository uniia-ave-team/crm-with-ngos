import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { NotificationsPage } from './notifications/notifications.page';
import { PasswordPage } from './password/password.page';
import { PersonalDataPage } from './personal-data/personal-data.page';
import { SettingsPageModule } from './settings.module';

describe('settings routes', () => {
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'settings', loadChildren: () => Promise.resolve(SettingsPageModule) }]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    });
    // Тексти живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    harness = await RouterTestingHarness.create();
  });

  it('opens the first tab on /settings', async () => {
    await harness.navigateByUrl('/settings');
    expect(TestBed.inject(Router).url).toBe('/settings/personal-data');
    expect(harness.routeNativeElement?.textContent).toContain(uk.settings.personal.title);
  });

  it.each([
    ['/settings/personal-data', PersonalDataPage, uk.settings.personal.title],
    ['/settings/password', PasswordPage, uk.settings.password.title],
    ['/settings/notifications', NotificationsPage, uk.settings.notifications.title],
  ] as const)('opens %s as a tab inside the settings page', async (url, page, title) => {
    await harness.navigateByUrl(url);
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe(url);
    // Вкладка відкрита всередині сторінки налаштувань, а не замість неї.
    expect(harness.routeNativeElement?.tagName.toLowerCase()).toBe('app-settings');
    expect(harness.fixture.debugElement.query(By.directive(page))).not.toBeNull();
    expect(harness.routeNativeElement?.textContent).toContain(title);
  });
});
