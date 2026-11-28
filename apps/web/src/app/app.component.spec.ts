import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';

import { Router, provideRouter } from '@angular/router';
import { MenuController, provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../assets/i18n/uk.json';

import { AppComponent } from './app.component';
import { SuccessNoticeService } from './core/notice/success-notice.service';
import { AccountsService } from './core/accounts/accounts.service';
import { ConnectionService } from './core/connection/connection.service';
import { KnowledgeBaseService } from './core/knowledge-base/knowledge-base.service';
import { ProjectAddDialogComponent } from './core/projects/project-add-dialog/project-add-dialog.component';
import { CurrentUserService } from './core/users/current-user.service';

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
        provideRouter([
          { path: 'calendar', children: [] },
          { path: 'knowledge-base', children: [], data: { layoutSection: 'knowledge-base' } },
          { path: 'planner', children: [], data: { layoutSection: 'planner' } },
        ]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
      imports: [AppComponent],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    await TestBed.inject(Router).navigateByUrl('/calendar');
  });

  describe('«+» of the knowledge base (to the root of the contents)', () => {
    async function renderAt(url: string, canCreate = true, permissions: string[] = []) {
      // Право створювати (`KnowledgeBaseService.canCreate`, з прав акаунта) і права акаунта тут підмінені.
      (TestBed.inject(KnowledgeBaseService) as { canCreate: Signal<boolean> }).canCreate = signal(canCreate);
      (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
      await TestBed.inject(Router).navigateByUrl(url);
      const fixture = TestBed.createComponent(AppComponent);
      fixture.detectChanges();
      return { fixture, element: fixture.nativeElement as HTMLElement };
    }

    it('stands next to the collapse button in the header of the side menu', async () => {
      const { element } = await renderAt('/knowledge-base');
      const add = element.querySelector('ion-menu ion-buttons .shell__menu-add') as HTMLElement;
      expect(add.getAttribute('aria-label')).toBe(uk.knowledgeBase.addHere);
      // «+» стоїть перед кнопкою згортання.
      expect(add.nextElementSibling?.getAttribute('aria-label')).toBe(uk.common.collapsePanel);
    });

    it('floats in the corner of the screen (outside the menu) while the side menu is open on a phone', async () => {
      const { fixture, element } = await renderAt('/knowledge-base');
      expect(element.querySelector('.shell__menu-fab')).toBeNull();

      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionWillOpen'));
      fixture.detectChanges();
      const fab = element.querySelector('.shell__menu-fab') as HTMLButtonElement;
      expect(fab.closest('ion-menu')).toBeNull();
      expect(fab.getAttribute('aria-label')).toBe(uk.knowledgeBase.addHere);
      fab.click();
      expect(TestBed.inject(KnowledgeBaseService).insertRequest()).toEqual({ folder: null });

      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionDidClose'));
      fixture.detectChanges();
      expect(element.querySelector('.shell__menu-fab')).toBeNull();
    });

    it('asks to add a page to the root of the contents by the header button', async () => {
      const { element } = await renderAt('/knowledge-base');
      (element.querySelector('.shell__menu-add') as HTMLElement).click();
      expect(TestBed.inject(KnowledgeBaseService).insertRequest()).toEqual({ folder: null });
    });

    it('is followed by the button of the template manager with Permissions.ViewTableTemplate, every header button with a hint', async () => {
      const { element } = await renderAt('/knowledge-base', true, ['Permissions.ViewTableTemplate']);
      const buttons = Array.from(element.querySelectorAll('ion-menu ion-buttons ion-button'));
      expect(buttons.map((button) => button.getAttribute('title'))).toEqual([
        uk.knowledgeBase.addHere,
        uk.knowledgeBase.openTemplates,
        uk.common.collapsePanel,
      ]);
    });

    it('has no button of the template manager without Permissions.ViewTableTemplate', async () => {
      const { element } = await renderAt('/knowledge-base', true, ['Permissions.CreateCustomTable']);
      expect(element.querySelector('.shell__menu-templates')).toBeNull();
    });

    it('floats the button of the template manager above «+» on a phone; it closes the menu and opens the manager', async () => {
      const { fixture, element } = await renderAt('/knowledge-base', true, ['Permissions.ViewTableTemplate']);
      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionWillOpen'));
      fixture.detectChanges();
      const fabs = Array.from(element.querySelectorAll('.shell__menu-fabs > button'));
      expect(fabs.map((fab) => fab.getAttribute('aria-label'))).toEqual([uk.knowledgeBase.openTemplates, uk.knowledgeBase.addHere]);

      const close = vi.spyOn(TestBed.inject(MenuController), 'close').mockResolvedValue(true);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      (fabs[0] as HTMLButtonElement).click();
      await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/knowledge-base/templates'));
      expect(close).toHaveBeenCalledWith('secondary');
    });

    it('is not there without the right to create', async () => {
      const { fixture, element } = await renderAt('/knowledge-base', false);
      expect(element.querySelector('.shell__menu-add')).toBeNull();
      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionWillOpen'));
      fixture.detectChanges();
      expect(element.querySelector('.shell__menu-fab')).toBeNull();
    });

    it('is not there in other sections', async () => {
      const { element } = await renderAt('/planner');
      expect(element.querySelector('.shell__menu-add')).toBeNull();
      expect(element.querySelector('.shell__menu-fab')).toBeNull();
    });
  });

  describe('«+» of the planner (a new project)', () => {
    async function renderAt(permissions: string[]) {
      (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
      await TestBed.inject(Router).navigateByUrl('/planner');
      const fixture = TestBed.createComponent(AppComponent);
      fixture.detectChanges();
      return { fixture, element: fixture.nativeElement as HTMLElement };
    }

    it('stands before the collapse button with Permissions.CreateProject, and floats on a phone while the menu is open', async () => {
      const { fixture, element } = await renderAt(['Permissions.CreateProject']);
      const buttons = Array.from(element.querySelectorAll('ion-menu ion-buttons ion-button'));
      expect(buttons.map((button) => button.getAttribute('title'))).toEqual([uk.planner.addProject, uk.common.collapsePanel]);
      expect(element.querySelector('.shell__menu-fab')).toBeNull();
      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionWillOpen'));
      fixture.detectChanges();
      expect(element.querySelector('.shell__menu-fab')?.getAttribute('aria-label')).toBe(uk.planner.addProject);
    });

    it('opens the window «New project» by the header button and by the floating one', async () => {
      const { fixture, element } = await renderAt(['Permissions.CreateProject']);
      const dialog = fixture.debugElement.query(By.directive(ProjectAddDialogComponent)).componentInstance as ProjectAddDialogComponent;
      const open = vi.spyOn(dialog, 'open').mockImplementation(() => undefined);
      (element.querySelector('.shell__menu-add') as HTMLElement).click();
      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionWillOpen'));
      fixture.detectChanges();
      (element.querySelector('.shell__menu-fab') as HTMLElement).click();
      expect(open).toHaveBeenCalledTimes(2);
    });

    it('is not there without Permissions.CreateProject', async () => {
      const { fixture, element } = await renderAt(['Permissions.ViewProject']);
      expect(element.querySelector('.shell__menu-add')).toBeNull();
      element.querySelector('ion-menu')!.dispatchEvent(new CustomEvent('ionWillOpen'));
      fixture.detectChanges();
      expect(element.querySelector('.shell__menu-fab')).toBeNull();
    });
  });

  describe('the notice of a success that outlives the page (SuccessNoticeService)', () => {
    it('shows the text from the language file at the top and closes by itself or by the cross', () => {
      const fixture = TestBed.createComponent(AppComponent);
      fixture.detectChanges();
      const toast = (fixture.nativeElement as HTMLElement).querySelector('ion-toast.success-notice') as HTMLElement & {
        isOpen: boolean;
        message: string;
        duration: number;
        position: string;
      };
      expect(toast.isOpen).toBe(false);

      TestBed.inject(SuccessNoticeService).show('resetPassword.done');
      fixture.detectChanges();
      expect(toast.isOpen).toBe(true);
      expect(toast.message).toBe(uk.resetPassword.done);
      expect(toast.position).toBe('top');
      expect(toast.duration).toBeGreaterThan(0);

      toast.dispatchEvent(new CustomEvent('didDismiss'));
      fixture.detectChanges();
      expect(TestBed.inject(SuccessNoticeService).key()).toBeNull();
      expect(toast.isOpen).toBe(false);
    });
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

  it('has the demo banner above the shell, and makes room for it only in demo mode', () => {
    // Сам банер перевіряється в його спеку; тут — що він стоїть у шаблоні перед оболонкою
    // й що оболонка поступається йому місцем лише в демо-режимі.
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
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
      version: '1.0.0',
      notice: null,
      demo: { accounts: [], sessionMinutes: 60 },
    });
    fixture.detectChanges();

    expect(shell.classList.contains('shell--below-banner')).toBe(true);
    vi.restoreAllMocks();
  });
});
