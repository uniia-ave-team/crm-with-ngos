import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { Router, RouterModule } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';

import { AppComponent } from './app.component';
import { LayoutModule } from './core/layout/layout.module';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      // Реальний застосунок редіректить '' на 'login' (сторінку без
      // оболонки) — щоб цей тест перевіряв звичайну сторінку з оболонкою,
      // а не впирався в той самий фолбек кореня, що й LayoutService,
      // навігуємо на нейтральний маршрут перед створенням компонента.
      imports: [IonicModule.forRoot(), LayoutModule, RouterModule.forRoot([{ path: 'calendar', children: [] }])],
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
});
