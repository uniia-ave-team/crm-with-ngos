import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { SignInFormComponent } from '../../core/auth/sign-in-form/sign-in-form.component';
import { ConnectionService } from '../../core/connection/connection.service';
import { DEFAULT_LOGIN_IMAGE } from '../../core/login-page-images/login-page-images.service';
import { ReloginPageModule } from '../relogin.module';
import { ReloginFormComponent } from './relogin-form.component';

interface FormInternals {
  onServerChange(origin: string | null): Promise<void>;
  openApp(): Promise<void>;
}

describe('ReloginFormComponent', () => {
  const server = 'https://first.example:5065';
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let connection: ConnectionService;
  let images: string[];
  let outputs: string[];

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      imports: [ReloginPageModule],
      providers: [provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }), provideHttpClient(), provideHttpClientTesting()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('uk', uk);
    translate.use('uk');
    http = TestBed.inject(HttpTestingController);
    connection = TestBed.inject(ConnectionService);
    // Перша перевірка підключення, яку ConnectionService запускає сам при створенні, до форми не належить.
    http.match(connection.apiHealthUrl());
    // Сервер перевіряє сама форма входу (її спек); тут він «недоступний», щоб не було запитів.
    vi.spyOn(connection, 'probeApi').mockResolvedValue(false);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    images = [];
    outputs = [];
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  const randomImage = (origin: string) => `${origin}/api/v1/loginpageimages/random`;

  function open(inputs: { accountId?: string | null; server?: string; email?: string } = {}): ComponentFixture<ReloginFormComponent> {
    const fixture = TestBed.createComponent(ReloginFormComponent);
    fixture.componentRef.setInput('accountId', inputs.accountId ?? null);
    fixture.componentRef.setInput('initialServer', inputs.server ?? server);
    fixture.componentRef.setInput('initialEmail', inputs.email ?? '');
    fixture.componentInstance.imageChange.subscribe((url) => images.push(url));
    fixture.componentInstance.back.subscribe(() => outputs.push('back'));
    fixture.componentInstance.remove.subscribe(() => outputs.push('remove'));
    fixture.detectChanges();
    return fixture;
  }

  const internals = (fixture: ComponentFixture<ReloginFormComponent>) => fixture.componentInstance as unknown as FormInternals;
  const element = (fixture: ComponentFixture<ReloginFormComponent>) => fixture.nativeElement as HTMLElement;
  const signInForm = (fixture: ComponentFixture<ReloginFormComponent>) =>
    fixture.debugElement.query(By.directive(SignInFormComponent)).componentInstance as SignInFormComponent;

  describe('the title row', () => {
    it('has the title, the back button and the trash button for a saved account', () => {
      const fixture = open({ accountId: 'a' });
      expect(element(fixture).querySelector('.relogin-form__title')!.textContent!.trim()).toBe(uk.relogin.form.title);
      expect(element(fixture).querySelector('.relogin-form__back')!.textContent!.trim()).toBe(uk.common.back);
      const trash = element(fixture).querySelector<HTMLElement>('.relogin-form__remove')!;
      expect(trash.querySelector('ion-icon')!.getAttribute('name')).toBe('trash-outline');
      expect(trash.getAttribute('title')).toBe(uk.nav.account.remove);
      expect(trash.getAttribute('aria-label')).toBe(uk.nav.account.remove);
    });

    it('has no trash button when an account is being added', () => {
      expect(element(open({ accountId: null })).querySelector('.relogin-form__remove')).toBeNull();
    });

    it('reports the clicks on the back and the trash buttons', () => {
      const fixture = open({ accountId: 'a' });
      element(fixture).querySelector<HTMLElement>('.relogin-form__back')!.click();
      element(fixture).querySelector<HTMLElement>('.relogin-form__remove')!.click();
      expect(outputs).toEqual(['back', 'remove']);
    });
  });

  describe('the sign-in form', () => {
    it('is the shared one, with the field of the server address, the given server, e-mail and key', () => {
      const form = signInForm(open({ accountId: 'a', server, email: 'first@organization.org' }));
      expect(form.withServer()).toBe(true);
      expect(form.initialServer()).toBe(server);
      expect(form.initialEmail()).toBe('first@organization.org');
      expect(form.accountId()).toBe('a');
    });

    it('checks the connection of the new active account and opens the planner again after a sign-in', async () => {
      const fixture = open();
      const checkApi = vi.spyOn(connection, 'checkApi').mockResolvedValue('healthy');
      const order: string[] = [];
      checkApi.mockImplementation(async () => {
        order.push('check');
        return 'healthy';
      });
      navigate.mockImplementation(async () => {
        order.push('navigate');
        return true;
      });

      await signInForm(fixture).afterSignIn()!();

      expect(order).toEqual(['check', 'navigate']);
      expect(navigate).toHaveBeenCalledWith('/planner', { onSameUrlNavigation: 'reload' });
    });
  });

  describe('the side image', () => {
    it('comes from the server of the form when the form reports it healthy', async () => {
      const fixture = open();
      // Спершу форма сама повідомляє про свій сервер (тут він «недоступний») — чекаємо на це.
      await vi.waitFor(() => expect(images).toEqual([DEFAULT_LOGIN_IMAGE]));
      images.length = 0;
      signInForm(fixture).serverChange.emit(server);
      http.expectOne(randomImage(server)).flush({ id: 'abc', url: '/files/login/abc.jpg' });
      await vi.waitFor(() => expect(images).toEqual([`${server}/files/login/abc.jpg`]));
    });

    it('is the default one when the server has no image', async () => {
      const fixture = open();
      images.length = 0;
      const loading = internals(fixture).onServerChange(server);
      http.expectOne(randomImage(server)).flush({}, { status: 404, statusText: 'Not Found' });
      await loading;
      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
    });

    it('is the default one, with no request, when the form reports no healthy server', async () => {
      const fixture = open();
      images.length = 0;
      await internals(fixture).onServerChange(null);
      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
      http.expectNone(() => true);
    });

    it('drops the image of a server that was changed while the image was loading', async () => {
      const fixture = open();
      images.length = 0;
      const first = internals(fixture).onServerChange(server);
      const stale = http.expectOne(randomImage(server));
      const second = internals(fixture).onServerChange(null);
      stale.flush({ id: 'abc', url: '/files/login/abc.jpg' });
      await Promise.all([first, second]);
      expect(images).toEqual([DEFAULT_LOGIN_IMAGE]);
    });
  });
});
