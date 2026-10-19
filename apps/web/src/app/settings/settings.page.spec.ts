import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { SettingsPage } from './settings.page';

describe('SettingsPage', () => {
  let component: SettingsPage;
  let fixture: ComponentFixture<SettingsPage>;

  beforeEach(() => {
    // Шаблон використовує `| translate`, тому TranslateService потрібен
    // у TestBed явно — поза застосунком немає AppModule, який зазвичай
    // його надає (там уже є provideTranslateService у app.module.ts).
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideTranslateService({ lang: 'en', fallbackLang: 'en' })],
    });
    fixture = TestBed.createComponent(SettingsPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('keeps the button that opens the side menu in the header (for phones, where the menu is a drawer)', () => {
    const button = (fixture.nativeElement as HTMLElement).querySelector('ion-header ion-menu-button');
    expect(button).not.toBeNull();
    expect(button?.getAttribute('menu')).toBe('secondary');
  });
});
