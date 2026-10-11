import { CUSTOM_ELEMENTS_SCHEMA, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import { LanguageService } from '../../core/i18n/language.service';
import { PreloaderModule } from './preloader.module';

@Component({
  template: `<app-preloader [visible]="visible()" />`,
  imports: [PreloaderModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly visible = signal(true);
}

describe('PreloaderComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', { common: { pleaseWait: 'Будь ласка, зачекайте...' } });
  });

  afterEach(() => localStorage.clear());

  function render(visible = true) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.visible.set(visible);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element, overlay: () => element.querySelector('.preloader') };
  }

  it('renders nothing while it is not visible', () => {
    expect(render(false).overlay()).toBeNull();
  });

  it('appears and disappears with the visible flag', () => {
    const { fixture, overlay } = render(false);
    fixture.componentInstance.visible.set(true);
    fixture.detectChanges();
    expect(overlay()).not.toBeNull();
    fixture.componentInstance.visible.set(false);
    fixture.detectChanges();
    expect(overlay()).toBeNull();
  });

  it('shows the mobile logo above a crescent spinner, and the text from the language file below it', () => {
    const { overlay } = render();
    const children = Array.from(overlay()!.children);
    expect(children.map((c) => c.tagName.toLowerCase())).toEqual(['img', 'img', 'ion-spinner', 'p']);
    expect(children[2].getAttribute('name')).toBe('crescent');
    expect(children[3].textContent?.trim()).toBe('Будь ласка, зачекайте...');
  });

  it('uses the mobile logo of the language for both themes', () => {
    TestBed.inject(LanguageService).setLanguage('uk');
    const { element } = render();
    const sources = Array.from(element.querySelectorAll('.preloader img')).map((i) => i.getAttribute('src'));
    expect(sources).toEqual(['assets/logo_mobile_ukrainian.svg', 'assets/logo_mobile_ukrainian_dark.svg']);
  });

  it('switches to the Latin logo for English', () => {
    TestBed.inject(LanguageService).setLanguage('en');
    const { element } = render();
    const sources = Array.from(element.querySelectorAll('.preloader img')).map((i) => i.getAttribute('src'));
    expect(sources).toEqual(['assets/logo_mobile_latin.svg', 'assets/logo_mobile_latin_dark.svg']);
  });

  it('announces itself as a busy status for assistive technologies', () => {
    const { overlay } = render();
    expect(overlay()?.getAttribute('role')).toBe('status');
    expect(overlay()?.getAttribute('aria-busy')).toBe('true');
  });
});
