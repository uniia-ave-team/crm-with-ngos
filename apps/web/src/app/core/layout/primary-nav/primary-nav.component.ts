import { Component, Signal, computed, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';

import { PopoverComponent } from '../../../shared/popover/popover.component';
import { AccountTile, toAccountTiles } from '../../../shared/account-avatar/account-tile';
import { AddAccountComponent } from '../add-account/add-account.component';
import { AccountRestoreService } from '../../accounts/account-restore.service';
import { AccountsService } from '../../accounts/accounts.service';
import { AuthService, REDIRECT_URL } from '../../auth/auth.service';
import { ADMIN_PANEL_PERMISSIONS, COLLEAGUES_PERMISSIONS, hasAnyPermission } from '../../auth/permissions';
import { LanguageService } from '../../i18n/language.service';
import { mobileLogoBase } from '../../i18n/mobile-logo';
import { CurrentUserService } from '../../users/current-user.service';

interface NavItem {
  readonly labelKey: string;
  readonly url: string;
  readonly icon: string;
  readonly iconActive: string;
  /** Пункт показується, лише коли є хоча б одне з цих прав; без поля — видно всім. */
  readonly requiresAny?: readonly string[];
}

/**
 * Перша (завжди видима) панель бічного меню: лого, основна навігація,
 * аватар і кнопка «Зоря». На десктопі — вертикальна колонка зліва;
 * на телефоні — верхня панель з аватаром і нижнє меню з пунктами навігації.
 * Обидва варіанти розмітки живуть в одному шаблоні й перемикаються CSS
 * медіазапитом, щоб не дублювати логіку активного пункту.
 */
@Component({
  selector: 'app-primary-nav',
  templateUrl: './primary-nav.component.html',
  styleUrls: ['./primary-nav.component.scss'],
  standalone: false,
})
export class PrimaryNavComponent {
  private readonly languageService = inject(LanguageService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly accountsStore = inject(AccountsService);
  private readonly restore = inject(AccountRestoreService);

  private static readonly ITEMS: readonly NavItem[] = [
    { labelKey: 'nav.calendar', url: '/calendar', icon: 'calendar-outline', iconActive: 'calendar' },
    { labelKey: 'nav.planner', url: '/planner', icon: 'checkbox-outline', iconActive: 'checkbox' },
    { labelKey: 'nav.knowledgeBase', url: '/knowledge-base', icon: 'bulb-outline', iconActive: 'bulb' },
    {
      labelKey: 'nav.colleagues',
      url: '/colleagues',
      icon: 'people-outline',
      iconActive: 'people',
      requiresAny: COLLEAGUES_PERMISSIONS,
    },
    {
      labelKey: 'nav.adminPanel',
      url: '/admin-panel',
      icon: 'hammer-outline',
      iconActive: 'hammer',
      requiresAny: ADMIN_PANEL_PERMISSIONS,
    },
  ];

  /**
   * Акаунт, який показують рейл і меню: слідує за активним, але не під час пошуку акаунта й
   * перемикання по плитці (`AccountRestoreService.searching`). Там активним на мить стає
   * акаунт, на який перемикаються, і якби меню йшло за ним, аватар і пункти мигтіли б чужими,
   * а коли перемикання не вдалось — ще й лишились би порожніми. Змінюється лише після завершення.
   */
  private readonly shownAccountId = signal<string | null>(this.accountsStore.activeId());

  /** Запис акаунта, який показується (`null` — акаунта нема). */
  private readonly shownAccount = computed(
    () => this.accountsStore.accounts().find((account) => account.id === this.shownAccountId()) ?? null,
  );

  constructor() {
    effect(() => {
      const id = this.accountsStore.activeId();
      const searching = this.restore.searching();
      untracked(() => {
        if (!searching) {
          this.shownAccountId.set(id);
        }
      });
    });
  }

  /**
   * Пункти меню, доступні користувачеві: ті, для яких є хоча б одне з потрібних прав
   * (права показаного акаунта зі знімка, оновлюються при кожній зміні сторінки). Поки прав
   * не отримано, пункти з вимогою не показуються.
   */
  protected readonly navItems: Signal<readonly NavItem[]> = computed(() => {
    const granted = this.shownAccount()?.permissions ?? [];
    return PrimaryNavComponent.ITEMS.filter((item) => !item.requiresAny || hasAnyPermission(granted, item.requiresAny));
  });

  /**
   * Поточний користувач (профіль із `GET /api/v1/users/me`) для аватара; `null`, доки не завантажено.
   * Під час невдалого перемикання лишається профілем попереднього акаунта (див. `CurrentUserService`).
   */
  protected readonly user = this.currentUser.user;

  /** Назва організації показаного акаунта (з його знімка): входить у колір аватара. */
  protected readonly ngoName: Signal<string> = computed(() => this.shownAccount()?.ngoName ?? '');

  /**
   * Акаунти у вікні аватара: усі збережені (`AccountsService`, за порядком
   * додавання), у тому числі застарілі. Дані плитки беруться зі знімка, що
   * лежить у записі акаунта, тож жодних запитів немає.
   */
  protected readonly accounts: Signal<readonly AccountTile[]> = computed(() =>
    toAccountTiles(this.accountsStore.accounts(), this.shownAccountId()),
  );

  /**
   * «+» у вікні акаунта: закриває вікно й відкриває «Додати акаунт». Нове вікно
   * відкривається, коли попереднє зникло остаточно (як при виході).
   */
  protected async openAddAccount(popover: PopoverComponent, dialog: AddAccountComponent): Promise<void> {
    await popover.close();
    dialog.open();
  }

  /**
   * Клацання по плитці акаунта (вікно закривається):
   * - активний — нічого;
   * - застарілий через відхилені токени (`outdated`, не `unavailable`) — вікно «Додати
   *   акаунт» із його сервером, поштою й ключем: увійти знову (збіглася пошта — акаунт
   *   перезапишеться);
   * - чинний або застарілий через недоступний сервер — перемикаємось (`activate`: перевірка,
   *   у тому числі повторне оновлення токенів) і відкриваємо `REDIRECT_URL`. Не вдалось —
   *   те саме вікно «Додати акаунт»; активним лишається попередній акаунт.
   */
  protected async selectAccount(popover: PopoverComponent, dialog: AddAccountComponent, tile: AccountTile): Promise<void> {
    await popover.close();
    if (tile.active) {
      return;
    }
    const account = this.accountsStore.accounts().find((candidate) => candidate.id === tile.id);
    if (!account) {
      return;
    }
    if (account.outdated && !account.unavailable) {
      dialog.open(account.email, account.id, account.server);
      return;
    }
    if ((await this.restore.activate(account.id)) === 'ok') {
      void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
      return;
    }
    dialog.open(account.email, account.id, account.server);
  }

  /** «Налаштування профілю»: закриває вікно й веде на сторінку налаштувань. */
  protected openSettings(popover: PopoverComponent): void {
    popover.close();
    void this.router.navigateByUrl('/settings');
  }

  /**
   * «Вийти»: закриває вікно й виходить з активного акаунта (він видаляється, його refresh-токен
   * відкликається на сервері; далі пошук іншого чинного акаунта: знайшовся — відкривається
   * `REDIRECT_URL`, ні — `/relogin` чи `/login`). Вихід чекає, поки вікно закриється остаточно,
   * бо перехід на /relogin чи /login прибирає меню, а недозакрите вікно лишилося б висіти без стилів.
   */
  protected async signOut(popover: PopoverComponent): Promise<void> {
    await popover.close();
    await this.auth.logout();
  }

  // Мобільний вордмарк-логотип (з текстом) є в окремих мовних варіантах
  // файлів — на відміну від іконки в десктопному рейлі (assets/logo.svg),
  // де тексту немає й перемикати мову не треба.
  protected readonly mobileLogoBase: Signal<string> = computed(
    () => mobileLogoBase(this.languageService.language()),
  );
}
