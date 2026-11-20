import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Router, RouterModule } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';

import { AppComponent } from './app.component';
import { AccountRestoreService } from './core/accounts/account-restore.service';
import { ConnectionService } from './core/connection/connection.service';
import { LayoutModule } from './core/layout/layout.module';
import { PreloaderModule } from './shared/preloader/preloader.module';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting()],
      // Реальний застосунок редіректить '' на 'login' (сторінку без
      // оболонки) — щоб цей тест перевіряв звичайну сторінку з оболонкою,
      // а не впирався в той самий фолбек кореня, що й LayoutService,
      // навігуємо на нейтральний маршрут перед створенням компонента.
      imports: [IonicModule.forRoot(), LayoutModule, PreloaderModule, RouterModule.forRoot([{ path: 'calendar', children: [] }])],
    }).compileComponents();
    await TestBed.inject(Router).navigateByUrl('/calendar');
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
    const restore = TestBed.inject(AccountRestoreService) as unknown as { searchingSignal: { set(v: boolean): void } };
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const search = (fixture.componentInstance as unknown as { accountSearch(): boolean }).accountSearch;

    expect((fixture.nativeElement as HTMLElement).querySelector('app-preloader')).not.toBeNull();
    expect(search()).toBe(false);
    restore.searchingSignal.set(true);
    expect(search()).toBe(true);
    restore.searchingSignal.set(false);
    expect(search()).toBe(false);
  });

  it('has the demo banner above the shell, and makes room for it only in demo mode', () => {
    // Сам банер перевіряється в його спеку; тут — що він стоїть у шаблоні перед оболонкою
    // й що оболонка поступається йому місцем лише в демо-режимі.
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    const http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const app = fixture.nativeElement as HTMLElement;
    const shell = app.querySelector('.shell')!;

    const banner = app.querySelector('app-demo-banner')!;
    expect(banner.compareDocumentPosition(shell) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(shell.classList.contains('shell--below-banner')).toBe(false);

    http.expectOne(connection.apiHealthUrl()).flush({ status: 'Healthy' });
    http.expectOne(`${connection.serverUrl()}/api/v1/system/status`).flush({
      hasAdmin: true,
      hasNgo: true,
      isSetupComplete: true,
      demo: { accounts: [], sessionIdleMinutes: 30, sessionMaxMinutes: 120 },
    });
    fixture.detectChanges();

    expect(shell.classList.contains('shell--below-banner')).toBe(true);
    vi.restoreAllMocks();
  });
});
