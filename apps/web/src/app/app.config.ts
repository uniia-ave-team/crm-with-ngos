import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { PreloadAllModules, RouteReuseStrategy, provideRouter, withComponentInputBinding, withPreloading } from '@angular/router';
import { IonicRouteStrategy, provideIonicAngular } from '@ionic/angular';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { SessionService } from './core/auth/session.service';
import { ConnectionService } from './core/connection/connection.service';
import { LanguageService } from './core/i18n/language.service';

/** Провайдери застосунку: Ionic, маршрути, HTTP з інтерцептором токенів, переклади й сервіси, що стартують разом із ним. */
export const appConfig: ApplicationConfig = {
  providers: [
    provideIonicAngular(),
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    // Усі сторінки довантажуються у фоні після старту; query-параметри (`?token=`, `?error=`)
    // привʼязуються до входів сторінок.
    provideRouter(routes, withPreloading(PreloadAllModules), withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Мова, з якою «стартує» сервіс, тут не має значення — LanguageService
    // одразу перевизначає її результатом детекції (localStorage/мова
    // пристрою) у своєму конструкторі.
    provideTranslateService({
      lang: 'en',
      fallbackLang: 'uk',
      loader: provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
    }),
    // LanguageService — providedIn: 'root', тобто Angular створює його
    // лише тоді, коли щось його вперше інжектить. Раніше це ненавмисно
    // робив лише PrimaryNavComponent (для мобільного лого), тож на
    // сторінках без бічного меню (/login) сервіс жодного разу не
    // створювався, і TranslateService лишався на жорсткому 'en' з опцій
    // вище, ігноруючи збережену мову. Явний ініціалізатор гарантує
    // створення для кожного маршруту, з оболонкою чи без.
    // Те саме для ConnectionService: на старті він визначає домен і
    // виводить його в консоль, але нічого сторінкового його не інжектить.
    provideAppInitializer(() => {
      inject(LanguageService);
      inject(ConnectionService);
      inject(SessionService);
    }),
  ],
};
