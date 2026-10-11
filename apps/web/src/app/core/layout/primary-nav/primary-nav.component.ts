import { Component, Signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';

import { PopoverComponent } from '../../../shared/popover/popover.component';
import { AccountTile } from '../account-menu/account-menu.component';
import { AccountsService } from '../../accounts/accounts.service';
import { AuthService } from '../../auth/auth.service';
import { LanguageService } from '../../i18n/language.service';
import { mobileLogoBase } from '../../i18n/mobile-logo';
import { CurrentUserService } from '../../users/current-user.service';

interface NavItem {
  readonly labelKey: string;
  readonly url: string;
  readonly icon: string;
  readonly iconActive: string;
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

  protected readonly navItems: readonly NavItem[] = [
    { labelKey: 'nav.calendar', url: '/calendar', icon: 'calendar-outline', iconActive: 'calendar' },
    { labelKey: 'nav.planner', url: '/planner', icon: 'checkbox-outline', iconActive: 'checkbox' },
    { labelKey: 'nav.knowledgeBase', url: '/knowledge-base', icon: 'bulb-outline', iconActive: 'bulb' },
    { labelKey: 'nav.colleagues', url: '/colleagues', icon: 'people-outline', iconActive: 'people' },
  ];

  /** Поточний користувач (профіль із `GET /api/v1/users/me`) для аватара; `null`, доки не завантажено. */
  protected readonly user = this.currentUser.user;

  /** Назва організації активного акаунта (з його знімка): входить у колір аватара. */
  protected readonly ngoName: Signal<string> = computed(() => this.accountsStore.active()?.ngoName ?? '');

  /**
   * Акаунти у вікні аватара: усі збережені (`AccountsService`, за порядком
   * додавання), у тому числі застарілі. Дані плитки беруться зі знімка, що
   * лежить у записі акаунта, тож жодних запитів немає.
   */
  protected readonly accounts: Signal<readonly AccountTile[]> = computed(() => {
    const activeId = this.accountsStore.activeId();
    return this.accountsStore.accounts().map((account) => ({
      id: account.id,
      firstName: account.firstName,
      lastName: account.lastName,
      // Відносні посилання (аватар і логотип ГО) — від адреси сервера саме цього акаунта.
      avatarUrl: absoluteUrl(account.avatarUrl, account.server),
      ngoName: account.ngoName,
      logoUrl: absoluteUrl(account.ngoLogoUrl, account.server),
      active: account.id === activeId,
      outdated: account.outdated,
      unavailable: account.unavailable,
    }));
  });

  /** «Налаштування профілю»: закриває вікно й веде на сторінку налаштувань. */
  protected openSettings(popover: PopoverComponent): void {
    popover.close();
    void this.router.navigateByUrl('/settings');
  }

  /**
   * «Вийти»: закриває вікно й виходить із системи (очищення сховища, запит на
   * сервер, сторінка входу). Вихід чекає, поки вікно закриється остаточно, бо
   * перехід на /login прибирає меню, а недозакрите вікно лишилося б висіти без стилів.
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

/** Посилання від адреси сервера; `null` — посилання нема чи воно некоректне. */
function absoluteUrl(url: string | null, server: string): string | null {
  const trimmed = url?.trim();
  if (!trimmed) {
    return null;
  }
  try {
    return new URL(trimmed, `${server}/`).toString();
  } catch {
    return null;
  }
}
