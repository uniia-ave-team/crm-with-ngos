import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
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
    // Іконка кнопки бокового меню (`ion-menu-button`) — з нашого набору (`core/icons.ts`), а не Ionicons.
    provideIonicAngular({ menuIcon: 'menu-outline' }),
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    // Усі сторінки довантажуються у фоні після старту; query-параметри (`?token=` запрошення)
    // привʼязуються до входів сторінок.
    provideRouter(routes, withPreloading(PreloadAllModules), withComponentInputBinding()),
    // `withFetch()`: запити через `fetch` — так збереження таблиці може йти з `keepalive` і не
    // обривається, коли вкладку закривають (див. `KbTableComponent`).
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    // Мову й запасну мову тут не задано навмисно: їх визначає й виставляє LanguageService
    // (сховище браузера / мова пристрою). Задана тут мова лише завантажила б зайвий файл.
    provideTranslateService({
      loader: provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
    }),
    // LanguageService — providedIn: 'root', тобто Angular створює його лише тоді, коли щось
    // його вперше інжектить. Без явного ініціалізатора на сторінках без бічного меню (/login)
    // сервіс не створювався б, і мову ніхто б не виставив. Ініціалізатор гарантує створення
    // для кожного маршруту, з оболонкою чи без.
    // Те саме для ConnectionService (на старті запускає першу перевірку API) і SessionService
    // (у фоні підтримує сесію): на якійсь зі сторінок їх може ніхто не інжектити.
    provideAppInitializer(() => {
      inject(LanguageService);
      inject(ConnectionService);
      inject(SessionService);
    }),
  ],
};
