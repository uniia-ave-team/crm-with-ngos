import { provideHttpClient } from '@angular/common/http';
import { NgModule, inject, provideAppInitializer } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { RouteReuseStrategy } from '@angular/router';

import { IonicModule, IonicRouteStrategy } from '@ionic/angular/lazy';
import { TranslatePipe, provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { AppComponent } from './app.component';
import { AppRoutingModule } from './app-routing.module';
import { LayoutModule } from './core/layout/layout.module';
import { LanguageService } from './core/i18n/language.service';

@NgModule({
  declarations: [AppComponent],
  // TranslatePipe — стандалонний пайп (потрібен тут для {{ ... | translate }}
  // у app.component.html), а не NgModule; provideTranslateService нижче
  // лише реєструє сам TranslateService.
  imports: [BrowserModule, IonicModule.forRoot(), LayoutModule, AppRoutingModule, TranslatePipe],
  providers: [
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    provideHttpClient(),
    // Мова, з якою «стартує» сервіс, тут не має значення — LanguageService
    // одразу перевизначає її результатом детекції (localStorage/мова
    // пристрою) у своєму конструкторі.
    provideTranslateService({
      lang: 'en',
      fallbackLang: 'en',
      loader: provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
    }),
    // LanguageService — providedIn: 'root', тобто Angular створює його
    // лише тоді, коли щось його вперше інжектить. Раніше це ненавмисно
    // робив лише PrimaryNavComponent (для мобільного лого), тож на
    // сторінках без бічного меню (/login) сервіс жодного разу не
    // створювався, і TranslateService лишався на жорсткому 'en' з опцій
    // вище, ігноруючи збережену мову. Явний ініціалізатор гарантує
    // створення для кожного маршруту, з оболонкою чи без.
    provideAppInitializer(() => {
      inject(LanguageService);
    }),
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
