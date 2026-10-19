import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { AdminPanelPage } from './admin-panel.page';

describe('AdminPanelPage', () => {
  let component: AdminPanelPage;
  let fixture: ComponentFixture<AdminPanelPage>;

  beforeEach(() => {
    // Шаблон використовує `| translate`, тому TranslateService потрібен
    // у TestBed явно — поза застосунком немає AppModule, який зазвичай
    // його надає (там уже є provideTranslateService у app.module.ts).
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ lang: 'en', fallbackLang: 'en' })],
    });
    fixture = TestBed.createComponent(AdminPanelPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
