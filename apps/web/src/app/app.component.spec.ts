import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Router, provideRouter } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../assets/i18n/uk.json';

import { AppComponent } from './app.component';
import { AccountsService } from './core/accounts/accounts.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      // Реальний застосунок редіректить '' на 'login' (сторінку без
      // оболонки) — щоб цей тест перевіряв звичайну сторінку з оболонкою,
      // а не впирався в той самий фолбек кореня, що й LayoutService,
      // навігуємо на нейтральний маршрут перед створенням компонента.
      providers: [
        provideIonicAngular(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'calendar', children: [] }]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
      imports: [AppComponent],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    await TestBed.inject(Router).navigateByUrl('/calendar');
  });

  describe('the warning that the storage of the browser is full', () => {
    /** Сховище читається, але браузер не дає в нього записати. */
    const fillStorage = () =>
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new DOMException('full', 'QuotaExceededError');
      });
    afterEach(() => vi.restoreAllMocks());

    function render() {
      const fixture = TestBed.createComponent(AppComponent);
      fixture.detectChanges();
      const toast = (fixture.nativeElement as HTMLElement).querySelector('ion-toast.storage-full') as HTMLElement & {
        isOpen: boolean;
        message: string;
        buttons: { text: string; role: string }[];
      };
      return { fixture, toast };
    }

    it('is a toast with the text and the button to close it from the language file, closed while the writes go through', () => {
      const { toast } = render();
      expect(toast.isOpen).toBe(false);
      expect(toast.message).toBe(uk.common.storageFull);
      expect(toast.buttons).toEqual([{ text: uk.common.close, role: 'cancel' }]);
    });

    it('opens when a write fails during the work, on any page, and is reset when it is closed', () => {
      const accounts = TestBed.inject(AccountsService);
      const account = accounts.add({ server: 'http://localhost:5065', accessToken: 'a', refreshToken: 'r', identity: 'u1' });
      const { fixture, toast } = render();
      fillStorage();
      accounts.update(account.id, { accessToken: 'b' });
      fixture.detectChanges();
      expect(toast.isOpen).toBe(true);

      toast.dispatchEvent(new CustomEvent('didDismiss'));
      fixture.detectChanges();
      expect(accounts.storageFull()).toBe(false);
      expect(toast.isOpen).toBe(false);
    });
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the primary navigation', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const app = fixture.nativeElement as HTMLElement;
    expect(app.querySelector('app-primary-nav')).toBeTruthy();
  });

  it('has the full-page preloader in the template, driven by the search for a valid account', () => {
    // Сам вигляд прелоадера перевіряється в його спеку й у браузері; тут — що
    // він є в шаблоні й що його прапорець іде від пошуку акаунта.
    const accounts = TestBed.inject(AccountsService);
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const search = (fixture.componentInstance as unknown as { accountSearch(): boolean }).accountSearch;

    expect((fixture.nativeElement as HTMLElement).querySelector('app-preloader')).not.toBeNull();
    expect(search()).toBe(false);
    accounts.setSwitching(true);
    expect(search()).toBe(true);
    accounts.setSwitching(false);
    expect(search()).toBe(false);
  });
});
