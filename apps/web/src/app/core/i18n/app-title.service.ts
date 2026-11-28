import { Injectable, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { NavigationEnd, Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { filter, map } from 'rxjs';

import { AccountsService } from '../accounts/accounts.service';

/** Сторінки без акаунта (вхід, повторний вхід, запрошення, новий пароль): у заголовку лише назва застосунку. */
const BARE_PAGES = ['/login', '/relogin', '/join', '/reset-password'];

/** Сторінка без акаунта: адреса — одна з `BARE_PAGES` (з параметрами чи якорем після неї). */
export function isBarePage(url: string): boolean {
  const path = url.split(/[?#]/)[0];
  return BARE_PAGES.some((page) => path === page || path.startsWith(`${page}/`));
}

/**
 * Заголовок вкладки браузера: «{назва ГО} — {Явір}» (`common.pageTitle`; назва ГО — з активного
 * акаунта, назва застосунку — `common.appName` мовою користувача); на сторінках без акаунта
 * (`BARE_PAGES`) і поки назви ГО нема — лише назва застосунку. Оновлюється з переходами, зміною акаунта й мови.
 */
@Injectable({ providedIn: 'root' })
export class AppTitleService {
  private readonly title = inject(Title);
  private readonly router = inject(Router);
  private readonly accounts = inject(AccountsService);
  private readonly translate = inject(TranslateService);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );
  private readonly appName = toSignal(this.translate.stream('common.appName'), { initialValue: '' });

  /** Заголовок для поточних адреси, акаунта й мови. */
  readonly text = computed(() => {
    const app = (this.appName() as string) || '';
    const ngo = this.accounts.active()?.ngoName?.trim();
    return ngo && !isBarePage(this.url()) ? (this.translate.instant('common.pageTitle', { ngo, app }) as string) : app;
  });

  constructor() {
    effect(() => {
      const text = this.text();
      if (text) {
        this.title.setTitle(text);
      }
    });
  }
}
